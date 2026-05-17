# Deterministic Contract

This document records the contract that must remain true across Unity client and
ASP.NET server execution.

- Simulation advances by fixed ticks only.
- Battle-affecting values use integer/fixed-point math.
- Server-provided `rng_seed` is the only battle RNG seed source.
- RNG algorithm is undecided and must be selected before gameplay logic depends on randomness.
- Platform time, floating-point physics, Unity random APIs, and server-local random APIs are prohibited for battle decisions.
- Fixtures under `fixtures/` should become the regression source for identical input/output checks.
