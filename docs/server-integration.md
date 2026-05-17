# Server Integration Notes

The ASP.NET server should reference the same BattleSim.Core version as the Unity
client.

Phase 1 server validation remains B-lite:

- Verify battle attempt
- Verify deck/config/rng seed
- Verify clear time and reward range
- Verify input log shape and command timing

Full deterministic replay validation remains a future option.
