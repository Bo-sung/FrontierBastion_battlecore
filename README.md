# BattleSim.Core

Shared deterministic battle simulation core for Frontier Bastion.

## Status

- Repository role: `SHARED_BATTLE_CORE`
- Build role: `BUILD_INFRA`
- Branch model: Git Flow (`main` + `develop`, feature/release/hotfix/support prefixes)
- Target framework: `.NET Standard 2.1` candidate, pending final Unity compatibility confirmation
- Package/versioning: undecided

## Boundary

`BattleSim.Core` owns deterministic battle state, commands, fixed tick progression,
fixed-point battle math, deterministic RNG contracts, and result calculation.

It must not depend on Unity APIs, ASP.NET, database access, network transport,
reward grants, or platform time/random APIs.

## Initial Layout

```text
src/BattleSim.Core/
tests/BattleSim.Core.Tests/
fixtures/
docs/
```

See `docs/architecture.md` and `docs/versioning-options.md` for current setup notes.
