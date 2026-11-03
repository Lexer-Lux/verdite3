using Verdite.Launcher;

// Verdite3 -- the shipped entry point.
//
// The launcher is Verdite Core's (tools/verdite-core/launcher): it settles where
// files live, asks for a disc, builds the game from it on first run and hands
// over. This is what it needs to know about this port. See docs/PACKAGING.md.

return Launcher.Run(new LauncherGame
{
    Name = "Verdite3",
    AppId = "verdite3",
    GameTitle = "King's Field III",
    Serial = "SLUS-00255",

    // King's Field (SLUS-00158) is the other US King's Field, and the series was
    // renumbered for the West: it is the Japanese King's Field II, Verdite2's
    // game. Every address in config/ is wrong for it and it would build.
    WrongDiscs = new Dictionary<string, string>
    {
        ["SLUS-00158"] =
            "This is the US disc for King's Field II (SLUS-00158); Verdite2 plays it. " +
            "This port targets King's Field III (SLUS-00255), released in the US " +
            "as King's Field II.",
    },

    RecompilerConfig = "kf3.json",
    GameAssembly = "KingsField3",
    UpdateRepository = "Lexer-Lux/verdite3",

    // OPEN.EXE is the title, and GAME.EXE arriving afresh is the memory card
    // screen (its start menu) before any area; an area module or END.EXE is play.
    // See "The session and the main loop" in docs/GAME_INTERNALS.md.
    PlayAfter = overlay => overlay switch
    {
        "open" or "game" => false,
        "end" => true,
        _ when overlay.StartsWith("fdat", StringComparison.Ordinal) => true,
        _ => null,
    },
}, args);
