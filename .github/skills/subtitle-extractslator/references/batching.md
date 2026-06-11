# Batching and Resume Reference

This file is skill-facing runtime contract only.

## Goal

Keep long-running subtitle jobs autonomous, restartable, and stable under rate limits or partial failures.

Use with:
1. `references/supervisor.md`
2. `references/worker.md`

## Supervisor/Worker Batch Model

1. Use supervisor/worker roles for all batch-processing scenarios.
2. If platform supports subagents, supervisor must delegate each bounded batch to worker subagents.
3. If subagents are unavailable, run the same worker contract inline in the main agent loop.
4. This model applies to recursive folder runs, resume runs, and mixed workload runs.

## Centralized Temp State Root

1. Do not create tracking folders next to media files.
2. Do not create `.subtitle-extractslator-temp` inside target folders.
3. Resolve temp root in this order:
- if `SUBTITLEEXTRACTSLATOR_TEMPDIR` is set, use that path
- otherwise use OS temp root + `SubtitleExtractslator`
4. State workspace path is `<temp-root>/agent-runs/<run-id>/`.
5. `run-id` should be deterministic for the requested scope (for example based on normalized common-root path + target language).

## Required State Files

Each run workspace must contain:
1. `queue.txt`
2. `completed.txt`
3. `failed.txt`
4. `in-progress.txt`
5. `run-notes.md`

## Queue Lifecycle

1. Scan requested scope.
2. Keep only items whose deterministic output `<basename>.<lang>.srt` does not exist.
3. Write one absolute input path per line to `queue.txt`.
4. Before each batch, rewrite `in-progress.txt` with current batch only.
5. On success, append the input path to `completed.txt`.
6. On blocked item, append `path | short reason` to `failed.txt`.
7. After each batch, rewrite `queue.txt` with remaining pending items only.

## Batch Size Defaults

Choose by workload type:
1. Embedded target-language already exists: `5-20` files per batch.
2. Embedded/local source then translate: `3-8` files per batch.
3. Existing SRT translate-only: `8-20` files per batch.
4. OpenSubtitles-dependent lane: `1` file at a time (strict serial).
5. Mixed scopes: split by folder first, then by work type.

## Progress Behavior

1. Progress updates are status-only.
2. Do not ask for permission after each batch when user intent is "continue" or "finish all".
3. Continue automatically until queue is empty or only hard blockers remain.

## Retry and Blockers

1. Retry transient tool errors a small bounded number of times.
2. On OpenSubtitles rate limit, switch that lane to delayed serial mode immediately.
3. If one candidate mismatches, try the next candidate; do not stop entire batch.
4. One-file failure must not stop overall queue processing.

## Resume Policy

On resume:
1. Read state files from the centralized run workspace first.
2. Trust `queue.txt` as starting pending set.
3. Reconcile outputs that were completed before interruption.
4. Continue from remaining queue without rescanning blindly.

## Completion Rule

Run ends only when:
1. `queue.txt` is empty after reconciliation, or
2. every remaining item is recorded in `failed.txt` with a real blocker.

## Platform-Neutral Adapter Policy

1. This repository defines runtime behavior in skill reference files.
2. Platform-specific agent entry files are optional adapters and must not redefine queue semantics.
3. Any adapter should map to this same centralized temp-state contract.
