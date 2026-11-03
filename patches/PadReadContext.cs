using RecompOne.Runtime.Events;

namespace Kf3;

/// <summary>The KF3 PadRead SDK wrapper saves its guest caller before BIOS PAD_dr.
/// The event observes the SDK's return address, not the gameplay/menu call site.</summary>
public static class PadReadContext
{
    public const uint BiosReturn = 0x800785BC;

    public static uint Caller(PadReadEvent e) => e.Context.RA == BiosReturn
        ? e.Memory.ReadU32(e.Context.SP + 0x10u) : e.Context.RA;

    /// <summary>Guide exists only in these native scrolling lists and its viewer.
    /// SLUS-00255 frames: PadRead -0x18/RA+0x10; menu reader 279A4
    /// -0x18/RA+0x10; list stepper 222FC -0x40/RA+0x3C. Thus the BIOS
    /// event sees the reader's caller at SP+0x28, and the list's at SP+0x6C.</summary>
    public static bool GuideMenu(PadReadEvent e)
    {
        if (e.Context.RA != BiosReturn || Caller(e) != 0x800279B4) return false;
        uint reader = e.Memory.ReadU32(e.Context.SP + 0x28u);
        if (reader == 0x80025154) return true; // Native Guide 24F88; Triangle is not Cancel.
        if (reader != 0x80022384) return false;
        return e.Memory.ReadU32(e.Context.SP + 0x6Cu) is
            0x8001AC18 or 0x8001C624 or 0x8001CF34 or 0x8001D308 or 0x8001D6AC or
            0x8001E074 or 0x8001E344 or 0x8002149C or 0x800218D0;
    }
}
