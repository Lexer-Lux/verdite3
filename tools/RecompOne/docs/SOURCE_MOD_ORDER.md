# Source-mod order

The native loader reads `mods.<id>.enabled` as True/False in `interface.ini`.
These are loader flags, independent of any game's own boolean setting format.
`settings.json` does not activate source mods.

Optional `mods.<id>.priority` integer keys choose ascending order among mods whose
dependencies are already satisfied. The default is zero, with IDs compared
ordinally ignoring case as the deterministic tie break. Dependencies still load
first regardless of their numerical priority. Without priority keys this keeps
the previous alphabetical, dependency-aware behavior.

An external library manager writes these same native keys. It must preserve
unowned keys, refuse concurrent edits and restore any keys it takes ownership of
when removing a deployment. There is no separate runtime load-order file.
