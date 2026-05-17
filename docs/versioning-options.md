# Versioning Options

Package and versioning are not final.

## Options

- NuGet package consumed by server, with Unity consuming a copied package artifact or package-compatible output.
- Unity Package Manager Git dependency plus server source/package reference.
- Git submodule or subtree in server/client repos.
- Local path references during early development, replaced by tagged package versions later.

## Pending Decision

Choose one reference strategy before server/client integration work begins.
