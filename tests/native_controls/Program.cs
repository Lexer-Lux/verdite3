using System.Reflection;
using System.Text.Json;
using ImGuiNET;
using Kf3;
using RecompOne.Runtime.Config;
using RecompOne.Runtime.Context;
using RecompOne.Runtime.Events;
using RecompOne.Runtime.Host.Window;
using RecompOne.Runtime.Memory;
using RecompOne.Runtime.Hardware;
using RecompOne.Runtime.Dispatch;
using Recompiled;
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
    Verdite.Core.Game.Configure(tag: "KF3");
    Kept.BoolsAsInts = true;
    Mouse.Configure(MouseLook.Game);
    ConfigManager.Load();

    Check(Analog.TurnSens == 3 && Analog.PitchSens == 3 && EnemyTurn.Multiplier == 3 && !EnemyTurn.Track,
        "fresh defaults use compensated x3 look without adding standing tracking");
    Check(PortSettings.StickTurn.Default == 3 && PortSettings.StickLook.Default == 3 &&
          PortSettings.EnemyTurnSpeed.Default == 3 && PortSettings.MouseTurn.Default == 1,
        "native setting defaults agree; mouse sensitivity is unchanged");
    using (var specJson = JsonDocument.Parse(File.ReadAllText(config)))
    {
        var defaults = specJson.RootElement.GetProperty("defaults");
        Check(defaults.GetProperty(Analog.TurnSensKey).GetString() == "3" &&
              defaults.GetProperty(Analog.PitchSensKey).GetString() == "3" &&
              defaults.GetProperty(EnemyTurn.Key).GetString() == "3", "setup defaults match native defaults");
        var turning = specJson.RootElement.GetProperty("pages").EnumerateArray().Single(p => p.GetProperty("id").GetString() == "turning");
        Check(turning.GetProperty("presets").EnumerateArray().All(p => p.GetProperty("value").GetDouble() == 3),
            "setup presets retain proportional enemy compensation");
        Check(specJson.RootElement.GetProperty("pages").GetArrayLength() == 1,
            "the old standalone Controls choice page is deleted");
        var controls = specJson.RootElement.GetProperty("categories")[0];
        Check(controls.GetProperty("id").GetString() == "controls" && controls.GetProperty("groups").EnumerateArray()
            .Select(g => g.GetProperty("id").GetString()).SequenceEqual(new[] { "bindings", "movement", "haptics" }),
            "original Controls step owns bindings, original movement and haptics together");
        var bindingSets = controls.GetProperty("groups")[0].GetProperty("tiers").EnumerateArray().Select(t => t.GetProperty("set")).ToArray();
        Check(bindingSets.Length == 3 && bindingSets[0].GetRawText() == bindingSets[1].GetRawText(),
            "three-tier schema collapses identical Classic entries into one choice");
        Check(bindingSets.All(s => !s.TryGetProperty(Analog.ControlsKey, out _) && !s.TryGetProperty(Analog.OnKey, out _) && !s.TryGetProperty(Analog.AccelKey, out _) &&
            !s.TryGetProperty(Analog.TurnSensKey, out _) && !s.TryGetProperty(Analog.PitchSensKey, out _) && !s.TryGetProperty(Analog.EaseKey, out _)),
            "binding preset does not own movement, acceleration, sensitivity or ease");
    }
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
    Rt.View.SetFloat(Analog.TurnSensKey, 2.4f);
    Rt.View.SetFloat(Analog.PitchSensKey, 1.7f);
    Rt.View.SetInt(Analog.EaseKey, 1);
    Rt.View.SetInt(Analog.AccelKey, 1);
    Rt.View.SetInt(Analog.OnKey, 0);
    Rt.View.SetInt(Analog.ControlsKey, 1);
    string customLook = Json(new[] { Rt.View.GetFloat(Analog.TurnSensKey), Rt.View.GetFloat(Analog.PitchSensKey),
        Rt.View.GetFloat(Analog.EaseKey), Rt.View.GetFloat(Analog.AccelKey), Rt.View.GetFloat(Analog.OnKey), Rt.View.GetFloat(Analog.ControlsKey) });
    void CheckLook() => Check(customLook == Json(new[] { Rt.View.GetFloat(Analog.TurnSensKey), Rt.View.GetFloat(Analog.PitchSensKey),
        Rt.View.GetFloat(Analog.EaseKey), Rt.View.GetFloat(Analog.AccelKey), Rt.View.GetFloat(Analog.OnKey), Rt.View.GetFloat(Analog.ControlsKey) }), "binding choice preserves independent custom look/movement values");
    Setup.Install();
    Check(File.ReadAllText("settings.json") == custom && ConfigManager.Game.Keys.Cross == "F11", "rerun setup does not rewrite custom bindings");
    Setup.ApplyControlProfile(true);
    CheckLook();
    var game = ConfigManager.Game;
    Check(game.Keys.Cross == "E" && game.Keys.L3 == "ShiftLeft" && game.Keys.Circle == "Tab" && game.Keys.Start == "Enter", "Modern keyboard retains separate menus");
    Check(game.Pad.Square.SequenceEqual(new[] { 10, 101 }) && game.Pad.Triangle.SequenceEqual(new[] { 9, 100 }), "both shoulders per combat action");
    Check(game.Pad.Cross.SequenceEqual(new[] { 0 }) && game.Pad.L3.SequenceEqual(new[] { 1 }) && game.Pad.Circle.SequenceEqual(new[] { 3 }) && game.Pad.Start.SequenceEqual(new[] { 6 }), "authorized Modern face/menu actions");
    Check(SeparateRun.Enabled && Setup.ControlProfile == 1, "Modern native run mode");
    Check(!Rt.View.Values.Keys.Any(k => k.StartsWith("input.")), "bindings have one store");
    ConfigManager.Load();
    Check(Setup.ControlProfile == 1 && ConfigManager.Game.Keys.Cross == "E", "Modern binding disk reload");
    Setup.ApplyControlProfile(false);
    CheckLook();
    Check(Json(ConfigManager.Game.Keys) == baselineKeys && Json(ConfigManager.Game.Pad) == baselinePad && Json(ConfigManager.Game.PadAnalog) == baselineAnalog, "Classic restores the captured upstream tables");
    Check(!SeparateRun.Enabled && ConfigManager.Game.Keys2.Cross == "F12" && ConfigManager.Game.Pad2.Cross.SequenceEqual(new[] { 42 }) && ConfigManager.Game.CdPath == "unrelated-disc-path", "player two and paths untouched");

    // Exercise the actual shared popup, rather than reproducing its page math.
    var assembly = typeof(Setup).Assembly;
    var onboardingType = assembly.GetType("Verdite.Core.Onboarding")!;
    onboardingType.GetField("_monitorRead", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, true);
    var popupType = assembly.GetType("Verdite.Core.OnboardingPopup")!;
    var pageType = popupType.GetNestedType("Page", BindingFlags.NonPublic)!;
    object Spec() => typeof(Setup).GetField("_spec", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
    foreach (bool inGame in new[] { false, true })
    {
        ConfigManager.Game.Keys.Cross = "F11";
        ConfigManager.Game.Pad.Circle = [42];
        ConfigManager.SaveGame();
        string beforeWizard = File.ReadAllText("settings.json");
        var popup = (Popup)Activator.CreateInstance(popupType, Spec(), "KF3", inGame)!;
        popup.Open();
        popupType.GetField("_page", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(popup, Enum.Parse(pageType, "Display"));
        var order = new List<string>();
        while (popupType.GetField("_page", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(popup)!.ToString() != "Summary")
        {
            order.Add((string)popupType.GetMethod("StepText", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(popup, null)!);
            popupType.GetMethod("Forward", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(popup, null);
        }
        order.Add((string)popupType.GetMethod("StepText", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(popup, null)!);
        Check(order.Count == 6 && order.Select((text, index) => text.Contains($"{index + 1}") && text.Contains("6")).All(v => v),
            "actual first/rerun navigation has six ordered steps with Controls only at step 2");
        popupType.GetMethod("Finish", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(popup, null);
        Check(File.ReadAllText("settings.json") == beforeWizard && ConfigManager.Game.Keys.Cross == "F11" &&
            ConfigManager.Game.Pad.Circle.SequenceEqual(new[] { 42 }), "first/rerun Keep preserves custom bindings byte-for-byte");
        CheckLook();
        popup.Close();
    }
    Setup.ApplyControlProfile(false);

    var mem = new TestMemory();
    ushort[] logical = [0x1000, 0x4000, 0x8000, 0x2000, 0x80, 0x20, 0x10, 0x40, 4, 1, 8, 2, 0x100, 0x800];
    for (int i = 0; i < logical.Length; i++) mem.WriteU16(0x80081868u + (uint)i * 2, logical[i]);
    mem.WriteU16(0x80081870, 0x10); // alternate saved attack/magic and use/menu preset
    mem.WriteU16(0x80081874, 0x80);
    mem.WriteU16(0x80081872, 0x40);
    mem.WriteU16(0x80081876, 0x20);
    var read = new PadReadEvent { Context = new() { RA = PadReadContext.BiosReturn, SP = 0x801FFC48 }, Memory = mem, Buttons = unchecked((ushort)~(0x80 | 0x40 | SeparateRun.RunMask)) };
    mem.WriteU32(read.Context.SP + 0x10, 0x80031128);
    SeparateRun.Enabled = true;
    SeparateRun.OnPadRead(read);
    Check((ushort)~read.Buttons == (0x10 | 0x20 | SeparateRun.RunMask), "Modern action mapping follows saved guest preset");
    mem.WriteU32(read.Context.SP + 0x10, 0x800279B4);
    read.Buttons = unchecked((ushort)~SeparateRun.RunMask);
    mem.WriteU16(0x8009C250, 0x20);
    SeparateRun.OnPadRead(read);
    Check(((ushort)~read.Buttons & 0x20) != 0, "Run becomes Cancel in menu reads");
    read.Buttons = unchecked((ushort)~(0x80 | 0x10));
    SeparateRun.OnPadRead(read);
    Check((ushort)~read.Buttons == (0x4 | 0x8), "combat shoulders retain native menu page actions");
    var guideConfig = new GameConfig();
    guideConfig.Pad.Circle = [3]; guideConfig.Pad.Triangle = [9, 100];
    guideConfig.Keys.Circle = "Tab"; guideConfig.Keys.Triangle = "Q";
    var guidePad = new HashSet<int>(); var guideKeys = new HashSet<string>();
    ushort GuideButtons(ushort buttons, bool guide = true) => SeparateRun.MenuButtons(buttons, guide,
        guideConfig, guidePad.Contains, guideKeys.Contains, 0x20);
    mem.WriteU32(read.Context.SP + 0x28, 0x80022384);
    foreach (uint owner in new uint[] { 0x8001AC18, 0x8001C624, 0x8001CF34, 0x8001D308,
        0x8001D6AC, 0x8001E074, 0x8001E344, 0x8002149C, 0x800218D0 })
    {
        mem.WriteU32(read.Context.SP + 0x6C, owner);
        Check(PadReadContext.GuideMenu(read), "measured native list frame identifies Guide owner");
    }
    mem.WriteU32(read.Context.SP + 0x6C, 0x8001C95C);
    Check(!PadReadContext.GuideMenu(read), "equipment slot chooser without native Guide is excluded");
    mem.WriteU32(read.Context.SP + 0x28, 0x800221FC);
    Check(!PadReadContext.GuideMenu(read), "top menu inventory exit is not Guide");
    mem.WriteU32(read.Context.SP + 0x28, 0x80025154);
    Check(PadReadContext.GuideMenu(read), "native Guide viewer preserves Triangle instead of Cancel");
    guidePad.Add(3);
    for (int held = 0; held < 30; held++)
        Check(GuideButtons(0x20) == 0x10, "Modern Triangle opens Guide and its opening hold cannot close it");
    Check(GuideButtons(0x20, false) == 0x20, "Modern Triangle still exits top inventory menu");
    guidePad.Clear(); guideKeys.Add("Tab");
    Check(GuideButtons(0x20) == 0x20, "keyboard Tab remains Back, never Guide");
    guideKeys.Clear(); guideKeys.Add("Q");
    Check(GuideButtons(0x10) == 0x10, "configured keyboard Triangle opens native Guide");
    guideKeys.Clear(); guidePad.Add(9);
    Check(GuideButtons(0x10) == 4, "physical casting shoulder retains menu page action");
    guidePad.Clear(); guideConfig.Keys.Triangle = "R"; guideKeys.Add("R");
    Check(GuideButtons(0x10) == 0x10, "custom keyboard Guide binding is read live");
    guideKeys.Clear(); guideConfig.Pad.Circle = [4]; guidePad.Add(4);
    Check(GuideButtons(0x20) == 0x20, "custom inventory pad button stays Back rather than assumed Triangle");
    guideConfig.Pad.Triangle = [3]; guidePad.Clear(); guidePad.Add(3);
    Check(GuideButtons(0x10) == 0x10, "custom native Triangle Guide binding is not mistaken for casting shoulder");
    guideConfig.Pad.Circle = [3]; guideConfig.PadKind = PadKind.Analog;
    guideConfig.PadAnalog.Circle = [4]; guidePad.Clear(); guidePad.Add(3);
    Check(GuideButtons(0x20) == 0x20, "inactive digital bindings cannot override custom analog bindings");
    guideConfig.PadAnalog.Circle = [3];
    Check(GuideButtons(0x20) == 0x10, "active analog Triangle binding opens Guide");
    guidePad.Clear();
    Check((GuideButtons(SeparateRun.RunMask) & 0x20) != 0, "native Back closes Guide through Run binding");
    Check(GuideButtons(0) == 0, "release does not synthesize a second Guide action");
    SeparateRun.Enabled = false; read.Buttons = unchecked((ushort)~0x10);
    SeparateRun.OnPadRead(read);
    Check((ushort)~read.Buttons == 0x10, "Classic Triangle remains native Guide without Modern translation");
    SeparateRun.Enabled = true;
    mem.WriteU32(read.Context.SP + 0x10, 0x80020000);
    ushort unrelated = unchecked((ushort)~(0x80 | 0x10 | SeparateRun.RunMask));
    read.Buttons = unrelated;
    SeparateRun.OnPadRead(read);
    Check(read.Buttons == unrelated, "unrelated wrapped callers remain untouched");
    AutoStart.Configure("new");
    typeof(AutoStart).GetField("_overlay", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, "game");
    read.Buttons = 0xFFFF;
    mem.WriteU32(read.Context.SP + 0x10, 0x80027A80);
    AutoStart.OnPadRead(read);
    Check((ushort)~read.Buttons == 0x40, "opt-in startup wait-any is acknowledged");
    read.Buttons = 0xFFFF;
    mem.WriteU32(read.Context.SP + 0x10, 0x80027A54);
    AutoStart.OnPadRead(read);
    Check(read.Buttons == 0xFFFF, "startup release loop gets no acknowledgement");
    mem.WriteU32(read.Context.SP + 0x10, 0x80020000);
    AutoStart.OnPadRead(read);
    Check(read.Buttons == 0xFFFF, "unrelated startup reads get no acknowledgement");
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
    Dispatcher.Register("game", new GameDispatchTable());
    Dispatcher.Load("game");
    int Camera(float sensitivity, int rate, bool pitch)
    {
        var ram = new PSMemory();
        for (int i = 0; i < logical.Length; i++) ram.WriteU16(0x80081868u + (uint)i * 2, logical[i]);
        ram.WriteU16(0x801B2668, (ushort)rate);
        Controller.LeftX = Controller.LeftY = 128;
        Controller.RightX = pitch ? (byte)128 : (byte)255;
        Controller.RightY = pitch ? (byte)255 : (byte)128;
        Analog.Tank = Analog.StickEase = Analog.LookAccel = false;
        Analog.TurnSens = Analog.PitchSens = sensitivity;
        ushort held = Analog.BeforeLook(ram, 0, 0, 0, 0, 0, false);
        ram.WriteU16(0x801B265C, held);
        KingsField3_game.func_8002F5C0(new CpuContext { SP = 0x801FF000 }, ram);
        int angle = ram.ReadU16(pitch ? 0x801B2610u : 0x801B2612u);
        return Math.Abs(((angle + 2048) & 4095) - 2048);
    }
    Check(Camera(1, 40, false) == 40 && Camera(3, 40, false) == 120,
        "actual native standing yaw proves linear x3, below the x4 cap");
    Check(Camera(1, 32, false) == 32 && Camera(3, 32, false) == 96 &&
          Camera(1, 40, true) == 32 && Camera(3, 40, true) == 96,
        "actual native walking yaw and pitch prove x3 without movement changes");
    Controller.RightX = Controller.RightY = 128;
    var enemyRam = new PSMemory();
    const uint creature = 0x80190000;
    enemyRam.WriteU32(0x801B25F0, unchecked((uint)-2048));
    enemyRam.WriteU32(0x801B25F8, 0);
    enemyRam.WriteU32(creature + 0x50, 123);
    enemyRam.WriteU32(creature + 0x54, 456);
    int TurnTicks(float multiplier)
    {
        EnemyTurn.SetMultiplier(multiplier);
        enemyRam.WriteU16(creature + 0x42, 0);
        enemyRam.WriteU16(creature + 0x58, 0);
        for (int tick = 1; tick <= 200; tick++)
        {
            var cpu = new CpuContext { SP = 0x801FF000, A0 = creature, A1 = 1024, A2 = 16, A3 = 2 };
            EnemyTurn.BeforeTurn(cpu, enemyRam);
            Check(cpu.A2 == (multiplier == 1 ? 16 : 48) && cpu.A3 == (multiplier == 1 ? 2 : 18),
                "enemy top speed scales x3 and acceleration x9");
            KingsField3_game.func_8004E928(cpu, enemyRam);
            if (enemyRam.ReadU16(creature + 0x42) == 1024) return tick;
        }
        throw new Exception("native creature did not reach target");
    }
    int nativeTicks = TurnTicks(1), compensatedTicks = TurnTicks(3);
    Check(compensatedTicks < nativeTicks && Math.Abs(nativeTicks / 3.0 - compensatedTicks) <= 2,
        "actual native quarter-turn time tracks the camera speed ratio within tick rounding");
    var wander = new CpuContext { A0 = creature, A1 = 2048, A2 = 16, A3 = 2 };
    EnemyTurn.BeforeTurn(wander, enemyRam);
    Check(wander.A2 == 16 && wander.A3 == 2 && enemyRam.ReadU32(creature + 0x50) == 123 &&
          enemyRam.ReadU32(creature + 0x54) == 456, "wandering turns and movement remain native");
    Console.WriteLine($"Measured native look: 40/32 -> 120/96 units/tick; creature quarter-turn fixture: {nativeTicks} -> {compensatedTicks} ticks");

    // Even a pre-migration config is a custom choice, not permission to reset it.
    Rt.View.SetInt("kf3.analog.defaults", 0);
    Rt.View.SetFloat(Analog.TurnSensKey, 2.4f);
    Rt.View.SetFloat(Analog.PitchSensKey, 1.7f);
    Rt.View.SetInt(Analog.EaseKey, 1);
    Rt.View.SetFloat(EnemyTurn.Key, 1.5f);
    Rt.View.SetInt(EnemyTurn.TrackKey, 1);
    string choices = Json(Rt.View.Values), bindings = File.ReadAllText("settings.json");
    Analog.Install();
    EnemyTurn.Install();
    Event.Dispatch(new RuntimeReadyEvent());
    Check(Analog.TurnSens == 2.4f && Analog.PitchSens == 1.7f && Analog.StickEase &&
          EnemyTurn.Multiplier == 1.5f && EnemyTurn.Track, "custom sensitivities and enemy preferences survive startup");
    Check(Json(Rt.View.Values) == choices && File.ReadAllText("settings.json") == bindings,
        "startup does not rewrite saved choices or custom bindings");
    Console.WriteLine("PASS: Classic/Modern tables, Custom preservation, binding storage/reload, authorized menus, run scope, distance independence, compensated look and native enemy turns");
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
    readonly Dictionary<uint, uint> longs = [];
    public ushort ReadU16(uint address) => words.GetValueOrDefault(address);
    public void WriteU16(uint address, ushort value) => words[address] = value;
    public byte ReadU8(uint address) => throw new NotSupportedException();
    public uint ReadU32(uint address) => longs.GetValueOrDefault(address);
    public void WriteU8(uint address, byte value) => throw new NotSupportedException();
    public void WriteU32(uint address, uint value) => longs[address] = value;
    public uint ReadWordLeft(uint current, uint address) => throw new NotSupportedException();
    public uint ReadWordRight(uint current, uint address) => throw new NotSupportedException();
    public void WriteWordLeft(uint address, uint value) => throw new NotSupportedException();
    public void WriteWordRight(uint address, uint value) => throw new NotSupportedException();
    public void LoadBytes(uint address, byte[] data) => throw new NotSupportedException();
    public void ZeroRange(uint address, uint length) => throw new NotSupportedException();
    public void SetCd(RecompOne.Runtime.Cdrom.CdController cd) => throw new NotSupportedException();
}
