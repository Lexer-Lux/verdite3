using System;
using System.IO;
using System.Linq;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Events;
using RecompOne.Runtime.Hardware;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Modding;
using Verdite.Core;
using Rt = RecompOne.Runtime.Runtime;

namespace Kf3;

/// <summary>The successful native I/O that last established the current save.
/// CardPath is the active card object's actual path, never a pending setting.</summary>
public sealed record NativeSaveIdentity(int Card, int Slot, string NativeName,
    string CardPath, MemoryCard CardObject);

/// <summary>Always-installed observation of KF3's successful native save/load.
/// No UI, game-state edits, file I/O, or recovery policy lives in this observer.</summary>
public static class NativeSaves
{
    sealed class Operation
    {
        internal string? Pending;
        internal NativeSaveIdentity? Opened;
    }
    static volatile NativeSaveIdentity? _last;
    static Operation? _operation;
    static bool _installed, _registered;
    static readonly ModInfo Self = new() { Id = "kf3.native-saves", Name = "Native save identity", Version = "1.0" };
    public static NativeSaveIdentity? LastSuccessful => _last;

    public static bool IsCurrentCardAvailable(NativeSaveIdentity? identity)
    {
        if (identity == null || identity.Card is < 0 or > 1) return false;
        var card = identity.Card == 0 ? Rt.CardA : Rt.CardB;
        return ReferenceEquals(card, identity.CardObject) && card.Enabled &&
            string.Equals(Path.GetFullPath(card.FilePath), identity.CardPath, StringComparison.OrdinalIgnoreCase);
    }

    public static void Install()
    {
        if (_installed) return;
        _installed = true;
        Event.AddListener<OverlayLoadedEvent>(e => { if (e.Name == "game") Clear(); });
        HookAttach.OnOverlayLoad("native save identity", Attach);
    }

    public static bool Attach()
    {
        uint[] addresses = { 0x8002860C, 0x80028750, 0x800795E0, 0x8005EA64, 0x8002B468 };
        SymbolRegistry.Build();
        var targets = addresses.Select(a => SymbolRegistry.Resolve("game", null, a)).ToArray();
        if (targets.Any(t => t == null)) return false;
        if (!_registered)
        {
            var type = typeof(NativeSaves);
            bool ok = true;
            for (int i = 0; i < 2; i++)
            {
                ok &= HookManager.AddPre(Self, targets[i]!, type.GetMethod(nameof(Begin))!);
                ok &= HookManager.AddPost(Self, targets[i]!, type.GetMethod(nameof(Complete))!);
            }
            // Observe after ordinary source-mod argument adjustments, just
            // before the BIOS wrapper runs. The observer never changes args.
            ok &= HookManager.AddPre(Self, targets[2]!, type.GetMethod(nameof(BeforeOpen))!, int.MaxValue);
            ok &= HookManager.AddPost(Self, targets[2]!, type.GetMethod(nameof(AfterOpen))!);
            for (int i = 3; i < targets.Length; i++)
                ok &= HookManager.AddPost(Self, targets[i]!, type.GetMethod(nameof(NewPlayer))!);
            if (!ok) { HookManager.RemoveMod(Self); return false; }
            _registered = true;
        }
        HookManager.Commit();
        return targets.All(t => HookManager.IsCommitted(t!));
    }

    public static void Clear() { _last = null; _operation = null; }
    public static void NewPlayer(CpuContext c, IMemory m) => Clear();
    public static void Begin(CpuContext c, IMemory m) => _operation = new Operation();
    public static void BeforeOpen(CpuContext c, IMemory m)
    {
        var operation = _operation;
        if (operation == null) return;
        operation.Pending = null;
        if (c.A0 < 0x80010000 || c.A0 > 0x801FFFE0) return;
        char[] name = new char[32];
        for (uint i = 0; i < name.Length; i++)
        {
            byte b = m.ReadU8(c.A0 + i);
            if (b == 0) { operation.Pending = new string(name, 0, (int)i); return; }
            name[i] = (char)b;
        }
    }
    public static void AfterOpen(CpuContext c, IMemory m)
    {
        var operation = _operation;
        string? name = operation?.Pending;
        if (operation == null || c.V0 == uint.MaxValue || name == null ||
            name.Length != 18 || name.Substring(5, 12) != "BASLUS-00255") return;
        int card = name.StartsWith("bu00:", StringComparison.OrdinalIgnoreCase) ? 0
            : name.StartsWith("bu10:", StringComparison.OrdinalIgnoreCase) ? 1 : -1;
        int slot = name[17] - '0';
        if (card < 0 || slot is < 1 or > 15) return;
        var actual = card == 0 ? Rt.CardA : Rt.CardB;
        operation.Opened = new NativeSaveIdentity(card, slot, name, Path.GetFullPath(actual.FilePath), actual);
    }
    public static void Complete(CpuContext c, IMemory m)
    {
        var operation = _operation;
        _operation = null;
        var identity = operation?.Opened;
        if (c.V0 == 0 && identity != null && m.ReadU8(c.GP + 0xAC) == identity.Slot)
            _last = identity;
    }
}
