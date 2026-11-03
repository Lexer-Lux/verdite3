# Main-view fog range

`RetainedScene.SetFogDepthScale(scale)` applies to the current frame's retained
main view. `BeginFrame` resets `Frame.FogDepthScale` to one. Finite values above
one make atmospheric fog sample `physical depth / scale`; shorter or invalid
values retain the native range. The port chooses the scale and its policy.

The shared linear cue helper applies the division once. Corner, per-pixel,
radial and neighbour-blended fog, including retained models, use the same scale.
Authored near/far records remain unchanged, including the near >= 32000 disabled
sentinel and equal near/far handling. The CPU cue reference remains native.

Positions, projection, depth-buffer values, visibility gates, distance fade and
its physical depth limit are unchanged. Mirror/cubemap/shadow views use scale
one. Packet programs leave the uniform at zero, which the helper treats as one.
This API extends atmospheric fog; it does not create or load distant geometry.
