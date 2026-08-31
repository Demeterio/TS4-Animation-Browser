# Archived Modding Resources

This directory contains dated ZIP snapshots of the public TS4 Animation Browser modding resources.

For the latest maintained version, use the files directly under `modding-resources/`. The files in this directory are historical publication snapshots kept for reproducibility and convenient download.

## Mirror

Archived snapshots are also mirrored on Sim File Share for convenient download:

[Sim File Share — TS4 Animation Browser Modding Resources](http://simfileshare.net/folder/274361/)

The GitHub repository remains the primary source for the latest maintained files, documentation history and exact raw contents.

## Naming

Archives use the publication date:

```text
Demeterio_TS4AnimationBrowser_ModdingResources_YYYY-MM-DD.zip
```

The date identifies the **modding-resources snapshot**, not the version or patch date of The Sims 4 used for research.

The exact game fixture for each snapshot remains documented inside that snapshot's `GAME_VERSION.md`.

## Archive contents

Each ZIP is self-contained and contains one top-level directory with the same dated name:

```text
Demeterio_TS4AnimationBrowser_ModdingResources_YYYY-MM-DD/
├─ _HOW_TO_OPEN_MARKDOWN_ON_WINDOWS.txt
├─ README.md
├─ BEGINNER_GUIDE.md
├─ GAME_VERSION.md
├─ CHANGELOG.md
├─ precomp/
└─ vfx/
```

The `archives/` directory itself is deliberately excluded. Older ZIP files are therefore never nested inside newer ZIP files.

## Tags

A published snapshot may also be marked with a Git tag using the same date:

```text
modding-resources-YYYY-MM-DD
```

This keeps modding-resource snapshots separate from TS4 Animation Browser application version tags and releases.

Tags are useful for browsing the exact raw repository files corresponding to a dated snapshot, while the ZIP provides a convenient standalone download.

## Snapshot index

| Snapshot | The Sims 4 fixture | Notes |
|---|---|---|
| 2026-08-31 | The Sims 4 - 1.126.73.1030 | Expanded VFX/SWARM and PRECOMP documentation with validated runtime-to-renderer/PRECOMP routes, stronger fixture coverage and clarified evidence scopes. |
| 2026-08-24 | The Sims 4 - 1.126.73.1030 | Initial public snapshot of the VFX, SWARM and PRECOMP research documentation and binary templates. |