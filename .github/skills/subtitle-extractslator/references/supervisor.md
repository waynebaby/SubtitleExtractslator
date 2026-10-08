# Subtitle Supervisor Playbook

This file is skill-facing runtime contract only.

Use this playbook when user asks to process a folder, season, series, or library until done.

## Responsibility

Act as persistent coordinator for:
1. scope discovery
2. queue lifecycle
3. batch selection
4. cooldown handling
5. resume behavior

## Required Behavior

1. Interpret "continue", "finish all", and equivalent wording as permission to keep going.
2. Initialize or resume centralized queue state under `<temp-root>/agent-runs/<run-id>/`.
3. Choose small bounded batches based on actual work type.
4. Use supervisor/worker contract for every batch-processing scenario.
5. Dispatch only independent work in parallel; keep tool-level serial constraints.
6. After each batch, update state files first, then continue automatically.
7. Send concise status updates, but do not ask whether to continue.

## Folder Strategy

1. Prefer one folder at a time for cleaner resume and easier diagnostics.
2. If one folder is blocked (rate limit or corrupted file), switch to another eligible folder.
3. Record switch reason in `run-notes.md`.
4. Revisit blocked folders after cooldown.

## Subagent Strategy

Supervisor/worker model applies to all batch-processing scenarios.

Subagent requirement:
1. If the platform supports subagents, the supervisor delegates bounded batches to worker subagents.
2. If the exact named worker agent is not registered, apply the Named Agent Fallback in `references/worker.md`: invoke a registered generic subagent as the driver with the full worker contract and the batch inputs. Never substitute a similar role.
3. If the platform has no subagent support at all, keep the same supervisor/worker contract in a single-agent loop.
4. A main-agent run for a batch that could have been delegated requires explicit user approval.

Parallel safety constraints:
1. Parallelize only independent folders or lanes.
2. Do not parallelize OpenSubtitles `search -> download` on the same lane.
3. Each worker must write deterministic state changes back to centralized temp files.

## Completion Rule

Stop only when:
1. `queue.txt` is empty after reconciliation, or
2. every remaining item has a real blocker in `failed.txt`, or
3. user interrupts or redirects.

## Summary Format

When reporting progress, include:
1. completed count and current folder
2. newly blocked items with short reason
3. next batch decision
