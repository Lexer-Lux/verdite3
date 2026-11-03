using System.Reflection;
using System.Text.Json;
using ImGuiNET;
using Kf3;
using RecompOne.Runtime.Config;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Events;
using RecompOne.Runtime.Host.Window;
using RecompOne.Runtime.Memory;
using Verdite.Core;
using Rt = RecompOne.Runtime.Runtime;

if (args.Length != 1) throw new ArgumentException("Pass the source config/onboarding.json path.");
string config = Path.GetFullPath(args[0]);
string previous = Environment.CurrentDirectory;
string scratch = Path.Combine(Path.GetTempPath(), "kf3-controls-" + Guid.NewGuid());
Directory.CreateDirectory(Path.Combine(scratch, "config"));
File.Copy(config, Path.Combine(scratch, "config/onboarding.json"));
Environment.CurrentDirectory = scratch;
ImGui.CreateContext();
try
{
    void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    string Json(object value) => JsonSerializer.Serialize(value);
    ConfigManager.Load();
    ConfigManager.Game.Keys = KeyLayout.Layout();
    ConfigManager.Game.Pad = new();
    ConfigManager.Game.PadAnalog = GamepadBindings.DefaultAnalog();
    ConfigManager.Game.Keys2.Cross = "F12";
    ConfigManager.Game.Pad2.Cross = [42];
    ConfigManager.Game.CdPath = "unrelated-disc-path";
    ConfigManager.SaveGame();
    Rt.View.SetString("unrelated.setting", "keep");
    string baselineKeys = Json(ConfigManager.Game.Keys), baselinePad = Json(ConfigManager.Game.Pad), baselineAnalog = Json(ConfigManager.Game.PadAnalog);
    Setup.Install();
    Check(Setup.ControlProfile == 0, "actual upstream baseline detects Classic");
    ConfigManager.Game.Keys.Cross = "F11";
    ConfigManager.SaveGame();
    string custom = File.ReadAllText("settings.json");
    Check(Setup.ControlProfile == -1, "custom binding detects Custom");
    Setup.Install();
    Check(File.ReadAllText("settings.json") == custom && ConfigManager.Game.Keys.Cross == "F11", "rerun setup does not rewrite custom bindings");
    Setup.ApplyControlProfile(true);
    var game = ConfigManager.Game;
    Check(game.Keys.Cross == "E" && game.Keys.L3 == "ShiftLeft" && game.Keys.Circle == "Tab" && game.Keys.Start == "Enter", "Modern keyboard retains separate menus");
    Check(game.Pad.Square.SequenceEqual(new[] { 10, 101 }) && game.Pad.Triangle.SequenceEqual(new[] { 9, 100 }), "both shoulders per combat action");
    Check(game.Pad.Cross.SequenceEqual(new[] { 0 }) && game.Pad.L3.SequenceEqual(new[] { 1 }) && game.Pad.Circle.SequenceEqual(new[] { 3 }) && game.Pad.Start.SequenceEqual(new[] { 6 }), "authorized Modern face/menu actions");
    Check(SeparateRun.Enabled && Setup.ControlProfile == 1, "Modern native run mode");
    Check(!Rt.View.Values.Keys.Any(k => k.StartsWith("input.")), "bindings have one store");
    ConfigManager.Load();
    Check(Setup.ControlProfile == 1 && ConfigManager.Game.Keys.Cross == "E", "Modern binding disk reload");
    Setup.ApplyControlProfile(false);
    Check(Json(ConfigManager.Game.Keys) == baselineKeys && Json(ConfigManager.Game.Pad) == baselinePad && Json(ConfigManager.Game.PadAnalog) == baselineAnalog, "Classic restores the captured upstream tables");
    Check(!SeparateRun.Enabled && ConfigManager.Game.Keys2.Cross == "F12" && ConfigManager.Game.Pad2.Cross.SequenceEqual(new[] { 42 }) && ConfigManager.Game.CdPath == "unrelated-disc-path", "player two and paths untouched");

    var mem = new TestMemory();
    ushort[] logical = [0x1000, 0x4000, 0x8000, 0x2000, 0x80, 0x20, 0x10, 0x40, 4, 1, 8, 2, 0x100, 0x800];
    for (int i = 0; i < logical.Length; i++) mem.WriteU16(0x80081868u + (uint)i * 2, logical[i]);
    mem.WriteU16(0x80081870, 0x10); // alternate saved attack/magic and use/menu preset
    mem.WriteU16(0x80081874, 0x80);
    mem.WriteU16(0x80081872, 0x40);
    mem.WriteU16(0x80081876, 0x20);
    var read = new PadReadEvent { Context = new() { RA = 0x80031128 }, Memory = mem, Buttons = unchecked((ushort)~(0x80 | 0x40 | SeparateRun.RunMask)) };
    SeparateRun.Enabled = true;
    SeparateRun.OnPadRead(read);
    Check((ushort)~read.Buttons == (0x10 | 0x20 | SeparateRun.RunMask), "Modern action mapping follows saved guest preset");
    read.Context.RA = 0x800279B4;
    read.Buttons = unchecked((ushort)~SeparateRun.RunMask);
    mem.WriteU16(0x8009C250, 0x20);
    SeparateRun.OnPadRead(read);
    Check(((ushort)~read.Buttons & 0x20) != 0, "Run becomes Cancel in menu reads");
    read.Buttons = unchecked((ushort)~(0x80 | 0x10));
    SeparateRun.OnPadRead(read);
    Check((ushort)~read.Buttons == (0x4 | 0x8), "combat shoulders retain native menu page actions");
    var run = typeof(SeparateRun).GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)!;
    bool inside = false;
    Action<CpuContext, IMemory> original = (_, m) => { inside = m.ReadU16(0x80081876) == SeparateRun.RunMask; throw new InvalidOperationException("fixture"); };
    try { run.Invoke(null, [original, new CpuContext(), mem]); } catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { }
    Check(inside && mem.ReadU16(0x80081876) == 0x20, "run mask is scoped and restored after faults");
    SeparateRun.Enabled = false;
    bool classicUntouched = false;
    original = (_, m) => classicUntouched = m.ReadU16(0x80081876) == 0x20;
    run.Invoke(null, [original, new CpuContext(), mem]);
    Check(classicUntouched, "Classic run path is untouched");

    foreach (float tiles in new[] { 0f, 16f, 30f })
        foreach (float fade in new[] { 0f, 3f, 6f })
        {
            RenderDistance.SetTiles(tiles);
            RenderDistance.SetFade(fade);
            Check(RenderDistance.Tiles == tiles && RenderDistance.FadeTiles == fade, "distance and fade independence");
        }
    PortSettings.SmoothDistanceFade.Apply(1);
    Check(RenderDistance.FadeTiles == 6 && RenderDistance.Tiles == 30, "enabling fade preserves custom positive band");
    PortSettings.SmoothDistanceFade.Apply(0);
    Check(RenderDistance.FadeTiles == 0 && RenderDistance.Tiles == 30, "fade off does not change distance");
    Check(PortSettings.Menu.Contains(PortSettings.RenderDist) && PortSettings.Menu.Contains(PortSettings.SmoothDistanceFade), "independent native menu rows");
    Check(PortSettings.Menu.GroupBy(s => s.Page).All(g => g.Count() <= PortSettings.MaxRows), "native menu row capacity");
    RenderDistance.SetTiles(16);
    RenderDistance.SetFade(6);
    SettingsStore.Install();
    PanelManager.DrawPanels();
    PortSetting[] settings = [PortSettings.RenderDist, PortSettings.RenderFade];
    var session = SettingsSession.Open(settings);
    session.Step(PortSettings.SmoothDistanceFade, 1);
    session.Step(PortSettings.RenderDist, 1);
    session.Discard();
    Check(RenderDistance.Tiles == 16 && RenderDistance.FadeTiles == 6, "distance/fade discard independently restores raw values");
    session = SettingsSession.Open(settings);
    session.Step(PortSettings.SmoothDistanceFade, 1);
    session.Save();
    Check(Rt.View.GetFloat("kf3.renderdistance.fade", -1) == 0 && !Rt.View.Has("kf3.renderdistance"), "fade-only save writes only raw band key");
    ConfigManager.Load();
    Check(Rt.View.GetFloat("kf3.renderdistance.fade", -1) == 0 && Rt.View.GetString("unrelated.setting") == "keep", "disk reload preserves band and unrelated preferences");
    Console.WriteLine("PASS: Classic/Modern tables, Custom preservation, real binding storage/reload, authorized menus, run scope, distance independence, save/discard, menu capacity");
}
finally
{
    ImGui.DestroyContext();
    Environment.CurrentDirectory = previous;
    Directory.Delete(scratch, true);
}

sealed class TestMemory : IMemory
{
    readonly Dictionary<uint, ushort> words = [];
    public ushort ReadU16(uint address) => words.GetValueOrDefault(address);
    public void WriteU16(uint address, ushort value) => words[address] = value;
    public byte ReadU8(uint address) => throw new NotSupportedException();
    public uint ReadU32(uint address) => throw new NotSupportedException();
    public void WriteU8(uint address, byte value) => throw new NotSupportedException();
    public void WriteU32(uint address, uint value) => throw new NotSupportedException();
    public uint ReadWordLeft(uint current, uint address) => throw new NotSupportedException();
    public uint ReadWordRight(uint current, uint address) => throw new NotSupportedException();
    public void WriteWordLeft(uint address, uint value) => throw new NotSupportedException();
    public void WriteWordRight(uint address, uint value) => throw new NotSupportedException();
    public void LoadBytes(uint address, byte[] data) => throw new NotSupportedException();
    public void ZeroRange(uint address, uint length) => throw new NotSupportedException();
    public void SetCd(RecompOne.Runtime.Cdrom.CdController cd) => throw new NotSupportedException();
}
