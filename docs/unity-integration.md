# Unity Integration Notes

Unity should consume BattleSim.Core without requiring Unity-specific code inside
the core library.

The final Unity version must be checked before the target framework is treated as
final. `.NET Standard 2.1` is only the current candidate.

Unity presentation and input adapters belong in the client repo, not in
BattleSim.Core.

## Syncing the DLL to the Unity client (Phase 1)

`tools/sync-unity-plugin.ps1` copies the built DLL to the Unity Plugins folder.

Run from the battlecore repo root:

```powershell
# Check whether the client plugin is up to date (builds first)
powershell -ExecutionPolicy Bypass -File tools\sync-unity-plugin.ps1 -CheckOnly

# Same check without rebuilding
powershell -ExecutionPolicy Bypass -File tools\sync-unity-plugin.ps1 -CheckOnly -SkipBuild

# Copy to client if hashes differ
powershell -ExecutionPolicy Bypass -File tools\sync-unity-plugin.ps1 -Apply

# Release build
powershell -ExecutionPolicy Bypass -File tools\sync-unity-plugin.ps1 -Apply -Configuration Release
```

Exit codes: `0` = up to date or applied, `1` = error, `2` = stale (no `-Apply`).

The script never creates or modifies `.meta` files.
