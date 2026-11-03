# Classic and Modern controls

This fork targets Japanese King's Field III, SLUS-00255. Classic is the
unmodified upstream port at `ebc307bc42669519af736c6d29cd5d1cba06881a`, not a
reconstruction of the PlayStation control defaults.

The request is for first-run setup and RUN SETUP to offer **Classic Controls**
and **Modern Controls**, and to preserve custom bindings until a preset is
explicitly chosen. The same choices appear above the native Input binding tabs.

## Classic baseline

The baseline is `KeyLayout.Layout()` and the runtime's `GamepadBindings` and
`DefaultAnalog()` from that commit. Keyboard: W/S walk, A/D strafe, arrows walk
and turn, Space attacks, F interacts, Q casts, Tab/Escape opens inventory, Enter
opens card/options, Right Shift is Select. M and the controller touchpad retain
the existing map shortcut. The native twin-stick implementation remains on at
its upstream defaults, without accelerated turning.

Controller Classic preserves every upstream physical mapping, including the
default digital-pad and analog-pad tables. The face actions are whatever the
loaded save's guest control configuration assigns to those buttons, as upstream.

## Modern

- Both RT and RB attack. Both LT and LB cast.
- Cross interacts. Hold Circle runs. Circle also cancels in native menus.
- Triangle opens inventory. Options/Start retains card/options. This allocation
  was explicitly chosen by Lexer after the two separate menus were identified.
- Keyboard E interacts and left Shift runs. Tab/Escape remains inventory, Enter
  remains card/options, and Right Shift remains Select.
- M/touchpad map, item turning and analog sensitivity controls retain their
  existing paths. The shoulder actions turn native menu pages left/right while
  that menu reader runs, rather than leaving its L1/R1 navigation unreachable.

No Remaster Editor exists in this upstream KF3 port, so there is no supported
Shift+E editor action to move to F2. This change does not add an editor or change
the optional debug mod's shortcut.

## Native implementation

`SeparateRun` changes the guest use-mask word at `0x80081876` to the otherwise
unused guest L3 bit `0x0200` only around `func_8002FE1C`, restoring it in a
`finally` block. The independent interaction handler `func_800305D8` sees the
original table. This is an in-memory hook, not an executable patch.

The KF3 held-use block begins at `0x80030010`: holding the use action decrements
`0x801B24F5` to zero. The next stage-4 `func_80030FCC` grows signed
`0x801B24F6` by 800, capped at 2,752, while that byte is zero. At `0x800312E4`
the game adds `(ramp * baseWalk) >> 12` to `0x801B2664`, whose base is 200.
At full ramp this is 334. Release sets `0x801B24F5` to one and the ramp decays
by 100. The original power costs and movement/collision path stay in the game.
These facts were derived from this disc's private generated source, not KF2.

At the gameplay PadRead return `0x80031128`, the Modern logical inputs are
translated through the loaded save's actual action-mask table. Changing a
guest face/direction preset therefore does not silently swap the Modern attack,
cast, use and menu actions. At native menu return `0x800279B4`, shoulders retain
page navigation. Other non-gameplay reads translate the Run action to Cancel.

`kf3.controls.separaterun` is an ordinary native setting, zero by default and
one in Modern. The diagnostic `KF3_SEPARATERUN=0|1` overrides it for a run.

## One binding store

Setup version 2 reads and writes binding choices directly through the existing
`settings.json` tables. Its `input.keys.*`, `input.pad.*` and
`input.padanalog.*` keys are specification paths, never mirrored INI values.
Detection compares actual tables; a custom mapping reads as Custom and opening
or reloading setup does not replace it. Explicitly choosing Classic or Modern
writes the chosen tables. Player-two bindings, disc/card paths and unrelated
settings are outside the profile.

The binding page owns the setup's controls choices. The old movement/turning
preset writes were removed from setup so another setup page cannot overwrite
the same controls. Enemy turn speed remains its own slider page. Advanced
native Input controls are still available.

The shared onboarding extension is in the Lexer-owned `verdite-core` fork,
branch `codex/kf3-binding-setup`, with shared-tree commits kept separate from
port changes. Nothing was submitted upstream.

## Verification

`tests/native_controls` runs against a privately built game assembly without a
disc, game loop or native window. It checks captured Classic restoration,
Modern tables, Custom preservation, binding disk reload, no duplicate binding
store, player-two/path isolation, alternate guest action masks, menu cancel/page
actions, and run-mask restoration even on faults. It also covers independent
distance/fade settings, raw fade-band persistence, discard and native row limits.

The focused check passed after the native rebuild, including the menu-page
assertion. The actual setup binding page and Input profile controls were rendered
offscreen from ImGui draw lists, using the runtime's Noto Sans font and theme.
Inspected: first-run Classic at 1200x800, first-run Modern at 760x500 with 1.5 UI
scale, rerun Custom at both sizes, and the Input buttons at 800x240 and 420x260
with 1.5 scale. Controls and text fit without clipping in these views.

Private in-game run-speed and interaction measurements, the complete installed
Settings window and physical-pad feel remain separate checks. No player
installation or save was changed here.
