# Subtitle Worker Playbook

This file is skill-facing runtime contract only.

Use this playbook for one bounded batch selected by the supervisor.

## Responsibility

Finish assigned batch safely and return exact state deltas.

Execution mode requirement:
1. worker contract is the standard execution unit for all batch-processing scenarios
2. If the platform supports subagents, the worker must run as a delegated subagent.
3. If the platform supports subagents but the exact named worker agent is not registered, apply the Named Agent Fallback below. A missing name is not the same as "no subagents".
4. Only when the platform has no subagent support at all, run the same bounded-batch contract inside the main agent loop.

## Named Agent Fallback

1. Invoke the worker subagent by its exact name first.
2. If the host reports that the exact name is not registered, do not switch to a similar role and do not treat that failure as a worker result.
3. Invoke an available registered generic subagent as the driver. Pass this exact file path (`references/worker.md`), its full contents, the bounded batch inputs (run id, centralized state paths, assigned items, output policy), and the reference context. The driver must perform this contract and return the Handoff Contract below.
4. A read-only or exploration-only role is not valid for the worker, because the worker writes subtitle outputs and centralized queue state.
5. If no capable driver can run, stop with a concrete blocker, keep the failed evidence, and do not mark the assigned items completed.
6. A main-agent (inline) run for a batch that could have been delegated requires explicit user approval.

## Batch Checklist

1. Read current `in-progress.txt` and relevant `run-notes.md` lines.
2. Process each assigned item using the skill workflow in strict order.
3. Respect deterministic output naming. Keep other artifacts on their normal output paths, but copy the final subtitle beside the source video as `<original_video_basename>.<lang>.srt` unless the user explicitly requested another subtitle destination or name.
4. Verify that copied subtitle path exists before marking item as completed.
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
