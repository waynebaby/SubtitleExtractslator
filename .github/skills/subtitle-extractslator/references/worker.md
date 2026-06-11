# Subtitle Worker Playbook

This file is skill-facing runtime contract only.

Use this playbook for one bounded batch selected by the supervisor.

## Responsibility

Finish assigned batch safely and return exact state deltas.

Execution mode requirement:
1. worker contract is the standard execution unit for all batch-processing scenarios
2. If platform supports subagents, worker must run as a delegated subagent.
3. If subagents are unavailable, run the same bounded-batch contract inside the main agent loop.

## Batch Checklist

1. Read current `in-progress.txt` and relevant `run-notes.md` lines.
2. Process each assigned item using the skill workflow in strict order.
3. Respect deterministic output naming.
4. Verify final output exists before marking item as completed.
5. On failure, record short reason and continue remaining items.

## Failure Handling

1. Retry transient tool failures when reasonable.
2. For OpenSubtitles mismatch, try next candidate instead of stopping batch.
3. For rate limits, stop only affected lane, write cooldown note, and return control.
4. Do not fail whole batch because one item failed.

## Handoff Contract

Return exactly:
1. `Completed:` one absolute path per line
2. `Failed:` one `path | reason` per line
3. `Notes:` cooldowns, filename anomalies, and resume-relevant observations

## Continue Policy

1. Do not ask whether to continue.
2. Supervisor decides next batch and continues automatically when possible.
