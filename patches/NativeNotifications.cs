using RecompOne.Runtime.Context;
using RecompOne.Runtime.Events;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Modding;
using Game = Recompiled.KingsField3_game;

namespace Kf3;

/// <summary>Owner-scoped bottom text through KF3's original font and packet allocator.
/// Holds last until cancelled; finite notices wait behind holds and native banners.</summary>
public static class NativeNotifications
{
    public const int MaximumGlyphs = 24, Capacity = 32;
    sealed record Line(string Owner, byte[] Glyphs);
    static readonly object Gate = new();
    static readonly Dictionary<string, Line> Holds = new(StringComparer.Ordinal);
    static readonly Queue<(Line Line, int Ticks)> Pending = new();
    static Line? _active;
    static int _remaining;
    static long _seen = -1;
    static bool _registered, _installed, _drawing;
    static readonly ModInfo Self = new() { Id = "kf3.native-notifications", Name = "Native bottom text", Version = "1.0" };

    static Line MakeLine(string owner, ReadOnlySpan<byte> glyphs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        if (owner.Length > 96 || owner.Any(char.IsControl)) throw new ArgumentException("Invalid text owner", nameof(owner));
        int end = glyphs.IndexOf((byte)255);
        if (end >= 0) glyphs = glyphs[..end];
        if (glyphs.Length is < 1 or > MaximumGlyphs) throw new ArgumentOutOfRangeException(nameof(glyphs));
        return new Line(owner, glyphs.ToArray());
    }

    public static void Hold(string owner, ReadOnlySpan<byte> glyphs)
    {
        var line = MakeLine(owner, glyphs);
        lock (Gate)
        {
            if (!Holds.ContainsKey(owner) && Holds.Count >= Capacity)
                throw new InvalidOperationException("Native bottom-text hold limit reached");
            Holds[owner] = line;
        }
    }

    /// <summary>Queue a finite notice, measured in world ticks. False means the bounded queue is full.</summary>
    public static bool Post(string owner, ReadOnlySpan<byte> glyphs, int ticks = 60)
    {
        var line = MakeLine(owner, glyphs);
        if (ticks is < 1 or > 900) throw new ArgumentOutOfRangeException(nameof(ticks));
        lock (Gate)
        {
            if (Pending.Count + (_active is null ? 0 : 1) >= Capacity) return false;
            Pending.Enqueue((line, ticks));
            return true;
        }
    }

    public static bool PostText(string owner, string text, int ticks = 60)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length is < 1 or > MaximumGlyphs) throw new ArgumentOutOfRangeException(nameof(text));
        byte[] glyphs = new byte[text.Length];
        for (int i = 0; i < text.Length; i++)
        {
            char letter = char.ToUpperInvariant(text[i]);
            glyphs[i] = letter == ' ' ? (byte)127 : letter is >= 'A' and <= 'Z'
                ? (byte)(letter - 'A') : throw new ArgumentException("Use native glyph bytes for nonletters", nameof(text));
        }
        return Post(owner, glyphs, ticks);
    }

    public static void Cancel(string owner)
    {
        lock (Gate)
        {
            Holds.Remove(owner);
            var keep = Pending.Where(p => p.Line.Owner != owner).ToArray();
            Pending.Clear();
            foreach (var line in keep) Pending.Enqueue(line);
            if (_active?.Owner == owner) { _active = null; _remaining = 0; }
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            Holds.Clear(); Pending.Clear(); _active = null; _remaining = 0; _seen = -1;
        }
    }

    public static void Install()
    {
        if (_installed) return;
        _installed = true;
        Event.AddListener<OverlayLoadedEvent>(_ => Clear());
        HookAttach.OnOverlayLoad("native bottom text", Attach);
    }

    static bool Attach()
    {
        uint[] addresses = [0x80041D9C, 0x8002860C, 0x80028750, 0x8005EA64, 0x8002B468];
        string[] methods = [nameof(Draw), nameof(Loaded), nameof(Loaded), nameof(NewPlayer), nameof(NewPlayer)];
        SymbolRegistry.Build();
        var targets = addresses.Select(a => SymbolRegistry.Resolve("game", null, a)).ToArray();
        if (targets.Any(t => t is null)) return false;
        if (!_registered)
        {
            for (int i = 0; i < targets.Length; i++)
                if (!HookManager.AddPost(Self, targets[i]!, typeof(NativeNotifications).GetMethod(methods[i])!))
                { HookManager.RemoveMod(Self); return false; }
            _registered = true;
        }
        HookManager.Commit();
        return targets.All(t => HookAttach.Installed(t!));
    }

    public static void Loaded(CpuContext c, IMemory m) { if (c.V0 == 0) Clear(); }
    public static void NewPlayer(CpuContext c, IMemory m) => Clear();

    // After the native banner emitter: Stage15 has already initialized this frame's OT.
    public static void Draw(CpuContext c, IMemory m)
    {
        lock (Gate)
        {
            bool tick = FramePacing.FirstWalkOfTick(ref _seen);
            uint head = m.ReadU8(0x801AEAF5) & 7u;
            if (_drawing || m.ReadU8(0x801AEAF7) != 0 || m.ReadU8(0x801AEAED + head) != 255) return;
            Line? line = Holds.Values.FirstOrDefault();
            bool held = line is not null;
            if (!held)
            {
                if (_active is null && tick && Pending.TryDequeue(out var pending))
                { _active = pending.Line; _remaining = pending.Ticks; }
                line = _active;
            }
            if (line is null) return;
            _drawing = true;
            try
            {
                // A full primitive arena defers the line, rather than losing its duration.
                if (DrawGlyphs(c, m, line.Glyphs) && !held && tick && _active == line && --_remaining <= 0)
                    _active = null;
            }
            finally { _drawing = false; }
        }
    }

    static bool Ram(uint address, uint bytes) => address >= 0x80010000 &&
        (ulong)address + bytes <= 0x80200000;

    static bool DrawGlyphs(CpuContext c, IMemory m, byte[] glyphs)
    {
        uint descriptor = m.ReadU32(0x80199170);
        if (!Ram(descriptor, 12) || c.SP < 0x80010200 || c.SP > 0x80200000 || !Ram(c.GP, 0x168)) return false;
        uint begin = m.ReadU32(descriptor + 8), end = m.ReadU32(descriptor + 4);
        uint bytes = (uint)(12 + 40 * glyphs.Length);
        if (begin > end || !Ram(begin, bytes) || bytes > end - begin) return false;
        uint ot = m.ReadU32(0x801A9174);
        if (!Ram(ot, 16)) return false;
        var saved = c.Snapshot();
        uint scratch = c.SP - 0x200, primitive = m.ReadU32(c.GP + 0x164);
        byte brightness = m.ReadU8(c.GP + 0x68);
        byte[] stack = new byte[0x200];
        for (uint i = 0; i < stack.Length; i++) stack[i] = m.ReadU8(scratch + i);
        c.SP -= 0x100;
        try
        {
            uint record = c.SP + 0x40;
            m.WriteU16(record, (ushort)(160 - glyphs.Length * 7 / 2));
            m.WriteU16(record + 2, 203);
            for (uint i = 0; i < glyphs.Length; i++) m.WriteU8(record + 4 + i, glyphs[i]);
            m.WriteU8(record + 4 + (uint)glyphs.Length, 255);
            m.WriteU8(c.GP + 0x68, 100);
            c.A0 = 0x8007E570; c.A1 = record;
            // KF3's native font advances descriptor+8 for every primitive. GP+164
            // is only its current primitive pointer, not a second allocator cursor.
            Game.func_800261DC(c, m);
            return true;
        }
        finally
        {
            for (uint i = 0; i < stack.Length; i++) m.WriteU8(scratch + i, stack[i]);
            m.WriteU8(saved.GP + 0x68, brightness);
            m.WriteU32(saved.GP + 0x164, primitive);
            c.Restore(saved);
        }
    }
}
