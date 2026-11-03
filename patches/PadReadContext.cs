using RecompOne.Runtime.Events;

namespace Kf3;

/// <summary>The KF3 PadRead SDK wrapper saves its guest caller before BIOS PAD_dr.
/// The event observes the SDK's return address, not the gameplay/menu call site.</summary>
public static class PadReadContext
{
    public const uint BiosReturn = 0x800785BC;

    public static uint Caller(PadReadEvent e) => e.Context.RA == BiosReturn
        ? e.Memory.ReadU32(e.Context.SP + 0x10u) : e.Context.RA;
}
