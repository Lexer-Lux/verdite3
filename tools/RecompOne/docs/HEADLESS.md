# Isolated simulation checks

`Runtime.Headless = true` before boot retains configuration, game simulation,
memory cards and runtime events while skipping host-window, GL-context and audio
device creation. It uses the host's existing no-window pump path. Configure a
valid disc in the private working directory before boot; there is no disc picker.

`Gpu.NoRaster = true` also skips software triangles when pixel output is not
under test. It does not skip guest polygon assembly or simulation. Never use it
to claim rendering acceptance.

Without an ImGui context, SaveView preserves the existing interface file rather
than trying to collect a layout from a null native context. Storage tests may
create an ImGui context without opening any native window.

This is the game-agnostic headless pattern already present in the newer shared
runtime's 0111 amendment. No game addresses or game-specific behavior are reused.
Each port decides how to expose the flags, and test callers must provide private
settings and card paths; Headless does not redirect or protect a player's files.
