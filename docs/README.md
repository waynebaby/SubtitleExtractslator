# SubtitleExtractslator Documentation Index

This folder contains maintainer-facing project design and implementation documentation.

## Documentation boundaries

- `docs/`: design decisions, architecture, implementation mapping, maintenance guidance.
- `subtitle-extractslator/references/`: runtime-facing skill contract and execution references used by agent workflows.
- `README.md` and `README.zh-CN.md`: repository overview and quick-start usage.

This separation keeps runtime contract docs stable while allowing implementation notes to evolve.

## Suggested reading order

1. [System Architecture](./system-architecture.md)
2. [Implementation Map](./implementation-map.md)
3. [Development and Operations](./development-and-operations.md)

## Existing deep-dive documents in this folder

- [OpenSubtitles Auth and Interface Design](./opensubtitles-auth-and-interface-design.md)
- [Skill Runtime Maintainer Notes](./skill-runtime-maintainer-notes.md)
- [Skill Installation and Build](./skill-installation-and-build.md)

## Source code landmarks

- `SubtitleExtractslator.Cli/`: runtime host and core workflow implementation.
- `SubtitleExtractslator.Tests/`: test suite.
- `scripts/`: helper scripts, including multi-platform binary publishing.
- `.github/workflows/`: CI and release automation.
