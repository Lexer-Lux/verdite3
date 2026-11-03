# Mods

The runtime-loaded mods under `mods/`: what each does, how it is built, and what
has been measured or judged. The addresses they rest on are in
`docs/GAME_INTERNALS.md`; this document is about the mods themselves.

A mod is a folder with a `mod.json` and C# that the runtime compiles with Roslyn
at boot (no implicit usings; every namespace named). Mods default to off: enable
one in the game's Mods panel, which writes `mods.<id>.enabled=True` under
`[RecompOne]` in `interface.ini`. A disabled mod loads silently, and so does an
enabled one that does not compile, apart from a `[Mods]` line on the console.

## Native bottom text for source mods

`Kf3.NativeNotifications` shares the game's bottom font between source mods.
`Hold(owner, glyphs)` displays a bounded native glyph string until `Cancel(owner)`;
`Post(owner, glyphs, ticks=60)` and `PostText(owner, text, ticks=60)` queue finite
notices. PostText accepts letters/spaces; use actual glyph bytes for item names.
Each owner cancels only its own holds and queued/active notices. Vanilla queued
or active banners take priority; held inspection text pauses finite notices.
Queues/holds are capped at 32 each, strings at 24 glyphs, and finite durations at
900 ticks. Successful load, new player and overlay transitions clear stale text.

The API draws after native `80041D9C`, using native font `800261DC` and template
`8007E570`. It reserves `12+40*glyphCount` bytes in the actual descriptor pointed
to by `80199170` (cursor +8, limit +4), deferring if the arena is full. It restores
CPU, scratch stack, brightness and GP current-primitive state; allocator and OT
advances are kept. No new native message IDs or screen overlay are introduced.
`tests/native_notifications` invokes that actual recompiled font and allocator
without a host, checking packets, bounds, stack/CPU/GTE canaries, priority,
owner cancellation, reset and world-tick duration. Gameplay behavior such as
inspection names or poison notifications remains in separate user-library mods.

## Native save identity for source mods

`Kf3.NativeSaves.LastSuccessful` observes successful native load/save operations,
including their actual BIOS filename and logical file1..15 on either card. It
runs independently of optional mods, so enabling a mod during play does not lose
the last observed save. Failed I/O preserves the previous successful identity;
an ordinary area change preserves it, and a new playthrough clears it.

The immutable identity contains the card index, logical file, native filename,
actual card object's file path and that card object. `IsCurrentCardAvailable`
requires the same enabled card object and path. A pending settings-path change
does not become the identity of an already-open card. The observer does not read
or write files or choose recovery behavior; source mods own those decisions.
Controller labels for their prompts come from public
`Verdite.Core.BindingTable.PadLabel`, the binding editor's existing label table.

`python tests/native_saves/test_native_saves.py --game-build <compiled-game-dir>
--dotnet <dotnet>` checks the compiled observer with actual native save/load and
BIOS routines on temporary synthetic cards. It covers the saver choosing file1
from a requested15, cardB file15, failed I/O, card replacement/disable, pending
settings changes, late consumers and resets. CD/resource pumps and the save-icon
readback are fixture boundaries. No host, audio, source mod or player card is
used; complete world recovery and any mod's prompt need separate checks.

## Debug Tools (`mods/kf3debug`, `kf3.debug`)

Built 2026-10-03 as a port of Verdite2's `mods/kf2debug`: the same structure,
class names and conventions, with every address re-found for this game (the
reverse engineering is in `docs/GAME_INTERNALS.md`, "The player" and the sections
under it, and "Changing area"). Everything is off until it is turned on; every
hook returns at once on a bool test when off, and nothing acts without a
character in an area (`GameState.IsInGame`, max HP non-zero), so the attract demo
is untouched.

| file | what |
|---|---|
| `GameState.cs` | the player block's addresses and typed accessors; stage 4 `func_80030FCC` is the player's tick, and every feature posts on it |
| `Cheats.cs` | invincibility, infinite MP, enemies that ignore you, a walk/turn speed multiplier |
| `Noclip.cs` | flight through walls and floors, camera-relative, with the cinematic camera (which holds instant mouse look off while it is on, through `ViewSmoothing.LeadSuppressed`: its look filter and the lead together judder), return-to-start and snap-to-floor |
| `Attributes.cs` | the character editor: EXP, level (through the game's level-up), HP/MP and their maxima, gold, the six base attributes, the five conditions, and the derived ratings with a lock that holds them across the recompute |
| `Items.cs` | the inventory (give, remove, set a count, give all) and the equipment slots, through the game's own add, remove and equip routines |
| `Magic.cs` | the spell book: learn, forget, learn all, and the selected spell (new; Verdite2's mod has no magic editor) |
| `Warp.cs` | four position bookmarks, persisted, and a warp to any of the 28 areas |
| `Hotkeys.cs` | F2 panel, F3 noclip, F4 invincibility, F5/F6 bookmark 1, F7 snap to floor, F8 return to the noclip start; V film mode (the cinematic camera, enemies ignoring you and noclip, all on, or all off when they already are; no toast); Page Up/Page Down and Left Shift fly (Space is attack in the port's layout); the pad's spare button (a DualSense's mute key) is film mode too |
| `DebugPanel.cs` | the dockable panel (Debug ▸ KF3 Debug): Cheats, Attributes, Items, Magic, Warp, State, Keys |

### The hooks

| address | kind | by | why |
|---|---|---|---|
| `0x80030FCC` stage 4 | post | all | the last player code before stage 10 copies the player into the camera: flight, restores, queued edits, hotkeys |
| `0x8002A6F4` take damage | pre | Cheats | zero the amount; the routine's own `amount == 0` return skips HP, knockback, flash and death |
| `0x8002A6A0`, `0x80030BE0` HP adders | pre | Cheats | zero a negative amount (poison, equipment drain); heals pass |
| `0x80030A6C` death latch | pre, skip | Cheats, Noclip | the falls and drowning call it at full HP; Noclip refuses it while flying, as Verdite2's does |
| `0x8002F9BC` walk, `0x8002F5C0` look | pre | Cheats | scale the walk and turn rates stage 4 has just written |
| `0x80029500` recompute | post | Attributes | re-apply locked ratings after the game re-sums them |
| `0x80047010` stage 3 | post | Warp | start an area change where the game's own exits run |
| `0x8004BF1C` behaviour picker | pre | Cheats | Enemies ignore you: the distance to the player in `a0` becomes `0x20000` |

The shared collision queries (`func_80033F38` and the floor queries) are never
hooked: creatures and objects call them from some thirty sites.

### Measured, 2026-10-03

From a run with a throwaway test entry that took commands from a file on the
stage-4 tick (not committed):

- **Load**: 15 hooks on 9 functions, all committed.
- **Invincibility**: a direct `func_8002A6F4(0, 10, 0)` took HP 50 to 40 with it
  off and left 50 with it on; `func_80030A6C(0)` set the dead state 0x11 with it
  off and was refused with it on.
- **Infinite MP**: MP written to 5 was back at 30 on the next tick.
- **Speed**: x3 moved the player about 3.8 times as far in the same hold
  (one comparison; the unscaled walk may have met a wall).
- **Items and equipment**: give, remove and equip changed the counts and slots
  as read back; the probe's names are the game's.
- **Magic**: learn-all marked 24 records known; the six unused placeholder
  records are skipped.
- **Level-up**: two levels through `func_8002A310`: level 1 to 3, max HP 50 to
  64, EXP set to the threshold, base attributes raised. HP is not refilled; that
  is the game's routine.
- **Bookmarks**: save, walk away, recall: back to the saved position exactly.
- **Area warp**: `fdat05`, `fdat17`, `fdat32`, `fdat62`, `fdat83` and back to
  `fdat02` load and play on. See "Changing area" in `docs/GAME_INTERNALS.md` for
  why the landing needed two fixes: the warp now moves the player to the nearest
  tile with a floor and calls the game's own placement `func_8002B760`, and by
  default switches noclip on at arrival ("Fly after an area warp"), because
  without it the first step cost 16 HP in `fdat32` and killed in `fdat62`. A
  warp to `fdat08` was sent straight back to `fdat02`.
- **Noclip**: flies forward and up under a held pad, and hands the player back to
  the game cleanly; the speed under real input was not measured (the test could
  not hold a button for every tick).

### Enemies ignore you, 2026-10-08

Verdite2's Peaceful switch, left out of the first port because the picker it hooks
had not been found here. It is now (`docs/GAME_INTERNALS.md`, "The creature AI is a
rule table scored on one number"): a pre-hook on `func_8004BF1C` tells it the player
is `0x20000` away, so only a creature's "player is far" rules can pass, and the
game's own scorer picks among them. The switch is in the Cheats tab, off by default
and unsaved, like invincibility; `KF3_DEBUG_PEACEFUL=1` starts with it on.

Measured in `fdat17` with the player held 5000 units from creature 14 (type 5) for
30 s: off, it alternated between its far rule `0x00` and its approach rule `0x05`;
on, it picked `0x00` and kept it. The load reports 17 hooks (15 before, plus the
picker and noclip's refusal of the death latch).

The same port brought over two smaller things Verdite2's mod had and this one did
not: noclip refuses the death latch while flying, so the below-the-floor check and
drowning cannot end a flight with invincibility off (by construction; no flight
was measured), and the cinematic
camera's two smoothing sliders have their tooltips. Verdite2's cinematic camera
also hides its mouse-capture glyph; this port has no such glyph, so there is
nothing to hide.

### To judge by eye

Nothing here has been looked at: the panel and its layout, noclip's feel and
speed (the KF2 default of 7000 units a second against this game's walk of 3000),
the cinematic camera, whether a creature switched to ignoring you mid-swing still lands the blow and whether its far behaviour still walks it towards you (both open in Verdite2 too), whether learned spells appear in the magic menu and cast,
whether the equipment editor's slot names match the game's equipment screen
(shield and gauntlets are the static reading), and where area warps land.
