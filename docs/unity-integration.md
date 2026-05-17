# Unity Integration Notes

Unity should consume BattleSim.Core without requiring Unity-specific code inside
the core library.

The final Unity version must be checked before the target framework is treated as
final. `.NET Standard 2.1` is only the current candidate.

Unity presentation and input adapters belong in the client repo, not in
BattleSim.Core.
