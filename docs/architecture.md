# BattleSim.Core Architecture

`BattleSim.Core` is a pure C# deterministic core shared by the Unity client and
the ASP.NET server.

## In Scope

- Battle command model
- Battle config model
- Deterministic fixed tick progression
- Fixed-point battle math
- Deterministic RNG contract and implementation, once selected
- Battle state transitions
- Battle result calculation
- Golden replay fixtures for deterministic regression checks

## Out Of Scope

- Unity `GameObject`, `MonoBehaviour`, `Physics`, `Time.deltaTime`, and Unity random APIs
- ASP.NET controllers, database access, network transport, and reward grants
- Client presentation, animation, particles, and input UI
- Server transaction handling, account state, and anti-cheat policy beyond core replay data

## Current Decisions

- Separate repo is adopted for the shared deterministic core.
- Git Flow branch model is used.
- Phase 1 simulation defaults are 20 TPS, 50 ms per tick, and fixed-point scale 10000.
- Target framework is currently `.NET Standard 2.1` candidate, not final.
- Package/versioning and server/client reference style are not final.
