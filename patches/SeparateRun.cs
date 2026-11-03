using System.Reflection;
using RecompOne.Runtime;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Events;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Modding;

namespace Kf3;

/// <summary>KF3's held-use run check and interaction check are separate routines.
/// Scope the run routine's use mask to the otherwise unused L3 bit; leave the
/// interaction routine and the player's guest control table unchanged.</summary>
public static class SeparateRun
{
    public const string Key = "kf3.controls.separaterun";
    public const ushort RunMask = 0x0200;
    const uint UseMask = 0x80081876;
    const uint RunRoutine = 0x8002FE1C;
    const uint GameplayReadReturn = 0x80031128;
    const uint MenuReadReturn = 0x800279B4;
    const uint CancelMask = 0x8009C250;
    public static bool Enabled { get; set; }
    static bool _queued;
    static readonly ModInfo Self = new() { Id = "kf3.separaterun", Name = "Separate run input", Version = "1.0" };
    static readonly ushort[] Logical = [0x1000, 0x4000, 0x8000, 0x2000,
        0x0080, 0x0020, 0x0010, 0x0040, 0x0004, 0x0001, 0x0008, 0x0002, 0x0100, 0x0800];

    public static void Install()
    {
        Event.AddListener<RuntimeReadyEvent>(_ => Enabled = Environment.GetEnvironmentVariable("KF3_SEPARATERUN") is { } forced
            ? forced is "1" or "on" or "true" : RecompOne.Runtime.Runtime.View.GetInt(Key, 0) != 0);
        Event.AddListener<PadReadEvent>(OnPadRead);
        HookAttach.OnOverlayLoad("separate run", Attach);
    }

    static bool Attach()
    {
        SymbolRegistry.Build();
        var target = SymbolRegistry.Resolve("game", null, RunRoutine);
        if (target is null) return false;
        if (!_queued)
            _queued = HookManager.AddReplace(Self, target,
                typeof(SeparateRun).GetMethod(nameof(Run), BindingFlags.NonPublic | BindingFlags.Static)!);
        HookManager.Commit();
        return _queued && HookAttach.Installed(target);
    }

    static void Run(Action<CpuContext, IMemory> original, CpuContext c, IMemory memory)
    {
        if (!Enabled) { original(c, memory); return; }
        ushort before = memory.ReadU16(UseMask);
        memory.WriteU16(UseMask, RunMask);
        try { original(c, memory); }
        finally { if (memory.ReadU16(UseMask) == RunMask) memory.WriteU16(UseMask, before); }
    }

    public static void OnPadRead(PadReadEvent e)
    {
        if (!Enabled || e.Port != 0) return;
        ushort pressed = (ushort)~e.Buttons;
        if (e.Context.RA == GameplayReadReturn)
        {
            // Modern bindings name actions. Follow a loaded save's actual guest
            // action table instead of assuming its face/direction preset is 3/0.
            ushort actual = (ushort)(pressed & RunMask);
            for (int i = 0; i < Logical.Length; i++)
                if ((pressed & Logical[i]) != 0) actual |= e.Memory.ReadU16(0x80081868u + (uint)i * 2);
            e.Buttons = (ushort)~actual;
        }
        else
        {
            // The same shoulder actions turn pages in the game's menu loops.
            // Do this only at the measured menu reader, not item presentations.
            if (e.Context.RA == MenuReadReturn)
            {
                ushort menu = (ushort)(pressed & ~(0x0080 | 0x0010));
                if ((pressed & 0x0080) != 0) menu |= 0x0008;
                if ((pressed & 0x0010) != 0) menu |= 0x0004;
                e.Buttons = (ushort)~menu;
            }
            // Circle is Back while a chooser reads the pad, not Run.
            if ((pressed & RunMask) != 0)
            {
                ushort cancel = e.Memory.ReadU16(CancelMask);
                e.Buttons &= (ushort)~(cancel != 0 ? cancel : (ushort)0x0020);
            }
        }
    }
}
