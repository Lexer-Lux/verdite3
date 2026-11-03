# Physical framebuffer HUD callbacks

`ScreenHud.Register(owner, callback)` returns an `IDisposable` registration.
Dispose it when the owning source mod unloads. Owners are unique, printable names
of at most 128 characters; at most 32 callbacks can be registered. Registration
and disposal publish an immutable callback snapshot. A failing callback is removed
and logged once so it cannot fail every following frame.

The host calls the registry after `_imgui.Render()`, with the actual window
framebuffer size. `IScreenHudCanvas` uses physical pixels, a top-left origin, and
the whole framebuffer. It is independent of the game's 4:3 canvas, the output
panel rectangle, render scale and high-DPI logical window size. In fullscreen the
window's bottom is the display's bottom. In a window it is the window's bottom.

`FillRect(x, y, width, height, rgba)` replaces colour in a clipped rectangle.
`rgba` is `0xRRGGBBAA`; it does not alpha-blend. Negative or zero dimensions and
rectangles entirely offscreen draw nothing. Coordinate additions use 64-bit
arithmetic before clipping. A retained canvas cannot draw after its callback
frame, or from another thread.

The primitive uses a colour-only `glClear` under a scissor rectangle. It does not
modify the game's packets, meshes, materials, depth or stencil. It saves and
restores the draw framebuffer, scissor box and enable, colour write mask for
buffer 0, and clear colour. On contexts with indexed masks only buffer 0 is
changed; other draw-buffer masks are untouched. Legacy contexts use their single
framebuffer/mask state. No shader or game-specific HUD logic is added here.

This extension exists because guest game drawing routines cannot place new HUD
elements outside their game canvas. Mods must capture their own immutable state
on the game thread and use it from the host callback. This is a native drawing
extension, not a gameplay patch or a replacement for any in-game message box.
