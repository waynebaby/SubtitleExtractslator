# SubtitleExtractslator Documentation Index

This folder contains maintainer-facing project design and implementation documentation.

## Documentation boundaries

- `docs/`: design decisions, architecture, implementation mapping, maintenance guidance.
- `subtitle-extractslator/references/`: runtime-facing skill contract and execution references used by agent workflows.
- `README.md` and `README.zh-CN.md`: repository overview and quick-start usage.

This separation keeps runtime contract docs stable while allowing implementation notes to evolve.

## Terminology Glossary

Use the canonical terms below across `README*.md`, `docs/*.md`, and `subtitle-extractslator/references/*.md`.

| Canonical term | Definition / usage rule | Avoid variants |
| --- | --- | --- |
| supervisor/worker contract | Mandatory batch-processing model. Applies to every batch-processing scenario. | optional topology, ad-hoc batch roles |
| subagent | Delegated agent unit for bounded worker batches. | worker thread, helper bot |
| If platform supports subagents | Mandatory condition phrase for delegation behavior. | if client supports subagents, if subagents are available |
| bounded batch | Small isolated execution unit for stability and retries. | micro batch, chunk (when used as contract term) |
| single-agent loop | Fallback execution path when subagents are unavailable. | single agent loop, one agent loop |
| centralized queue state | Queue tracking state stored under temp root, not beside media files. | local queue folder beside media |
| temp root (`<temp-root>`) | `SUBTITLEEXTRACTSLATOR_TEMPDIR` when set; otherwise OS temp + `SubtitleExtractslator`. | working folder temp, media-folder temp |
| run-id (`<run-id>`) | Deterministic identifier for one queue run scope. | random session id (for queue contract) |
| queue state files | `queue.txt`, `completed.txt`, `failed.txt`, `in-progress.txt`, `run-notes.md`. | custom filename set |
| run-to-completion | Continue processing until queue is empty or only hard blockers remain. | per-batch permission loop |
| hard blocker | Real unresolved item-level blocker recorded in `failed.txt`. | transient error treated as blocker |

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
