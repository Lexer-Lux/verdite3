# Per-frame map-half appearance

`RetainedScene.SetHalfGlow(x, z, upper, red, green, blue)` lets a port or source
mod add colour to an existing visible main-view map half. This is renderer
support for colour on geometry that the game represents as tiles instead of
model instances. Game-specific selection and interaction state belong to mods.

Coordinates name the existing 80 by 80 map and its two halves. Invalid coordinates
or a call before a frame return false. RGB is additive 8-bit colour; zero leaves
the original material. A new frame clears the bounded 38,400-byte colour array.
Publish after BeginFrame and before presentation. Nothing persists to the next
frame unless the caller publishes it again.

The existing half texture now carries RGBA8 unsigned data: R is the original
visibility/fade weight, GBA is optional RGB glow. No new texture unit is used.
The map vertex shader carries that colour into the existing fragment material
glow path, attenuated by the atmospheric fog. Texture holes, normal depth tests,
the visibility gate and distance fade retain their original decisions. The
normal/depth pass reads the same R weights. Models, packets, sky, arm and
reflection captures get no map glow.

This is renderer work: the game has no routine for adding a per-tile material
glow to the retained GPU renderer. No game executable or guest game state is
patched by this API, and no gameplay rule is built into the runtime.

Verification: runtime shader extraction, static rules, all affected program
links and the official glslang compiler report no findings. This does not
establish visual acceptance in a real scene.
