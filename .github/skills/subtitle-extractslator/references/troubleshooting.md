# Troubleshooting

## Skill does not run commands

Check:

1. Runtime package was acquired from this repository's `packages.released.md` or `packages.beta.md` absolute URL.
2. The external package contains `lib/net9.0/SubtitleExtractslator.Cli.dll`.
3. Host environment allows `dotnet` invocation.
4. Use an absolute DLL path instead of looking under the skill folder.

## No subtitle tracks found

Possible causes:

1. Source media has no embedded subtitle stream.
2. ffprobe is unavailable in environment.
3. Input path is wrong.

Actions:

1. Validate file path.
2. Run ffprobe manually.
3. In MCP mode, after downloading FFmpeg call `ffmpeg_set_bin_dir` to hot-apply path to current MCP process (and persist to mcp.json by default).
4. If MCP is not used, set `FFMPEG_BIN_DIR` in environment and retry.
5. Record or refresh the same absolute path in `references/localpaths.md` for the next run.
6. If no usable embedded track exists, continue to local subtitle discovery and then OpenSubtitles fallback instead of stopping the whole batch.
7. For expected Chinese embedded subtitles, retry selection with `chi` if `zh` did not resolve a usable track.

## OpenSubtitles has no results

Current implementation behavior:

1. If no candidate is available, workflow falls back to local extraction.
2. Real API search/download requires valid auth cache from `subtitle auth login`.
3. For offline testing, set `OPENSUBTITLES_MOCK=1`.

Credential missing handling:

1. Run `subtitle auth login` to set `api-key`, `username`, and `password`.
2. Then run `subtitle auth aquire` to verify auth state.
3. OpenSubtitles operations run per-call auth aquire semantics and do not accept username/password parameters.
4. If operation fails with `auth_relogin_required`, the message must explicitly guide: run `subtitle auth login` and retry.

Candidate mismatch handling:

1. If filename confidence is low, do not adopt top candidate directly.
2. Run `subtitle-timing-check --input <mediaFile> --subtitle <subtitleFile.srt>` after candidate download.
3. Accept candidate only when absolute difference between video duration and subtitle last cue is less than 10 minutes.
4. If check fails, try next candidate.

## SO auth seam loops or unexpected relogin prompts

Checks:

1. Current `workflow.current.json` waiting node matches the seam you intend to resume.
2. Resume result file `id` matches the exact seam transition/result expected by the current waiting node.
3. Active context snapshot still matches the same output policy and auth-validation branch.

Actions:

1. Validate current node, result ID, and context/output-policy snapshot together before `dotnet so.dll resume`.
2. Do not reuse stale auth seam artifacts from an earlier waiting state.
3. If one of the three surfaces drifted, regenerate the resume result for the current waiting seam instead of patching only one file.

## Local proxy says READY but requests still fail

Checks:

1. Intended HTTP port is actually listening.
2. The endpoint returns a real HTTP response on the configured translation/OCR path.
3. Another local process is not already bound to the same port.

Actions:

1. Run a real health check before routing CLI translation or bitmap OCR traffic to the proxy.
2. If the port is occupied or the response is invalid, change the endpoint or stop the conflicting listener.
3. Do not treat startup logs alone as readiness evidence.

## Output structure changed unexpectedly

Expected invariant:

1. Cue count remains stable.
2. Cue index remains stable.
3. Start and end timestamps remain stable.
4. Per-cue line count remains stable.

If invariant fails:

1. Stop output.
2. Inspect provider output for line count mismatch.
3. Retry with conservative provider settings.

## MCP mode appears idle

Normal behavior:

1. MCP server waits for incoming stdio frames.
2. No human-readable prompt is expected after startup.
3. Use MCP client to call tools and inspect responses.

## MCP tool call failed

Expected behavior:

1. Tool returns structured payload with `ok=false` instead of unhandled process crash.
2. Error details are in `error.code`, `error.message`, optional `error.snapshotPath`, and `error.timeUtc`.

Actions:

1. Read `error.message` first.
2. If `snapshotPath` exists, inspect that file for detailed stack/context.
3. Retry with a known-good local path and minimal arguments.

## Batch translate command issues

Checks:

1. Batch command is CLI-only (`translate-batch` is not exposed in MCP mode).
2. `--input-list` file is UTF-8 and has one path per line.
3. Empty lines and lines starting with `#` are ignored.

Actions:

1. Run `--help` and verify command shape.
2. Test one failing path with single `translate` command.
3. Confirm output folder is writable.

## Queue tracking files are missing or in wrong location

Checks:

1. Queue state files should be under `<temp-root>/agent-runs/<run-id>/`, not near media files.
2. `temp-root` should resolve to `SUBTITLEEXTRACTSLATOR_TEMPDIR` when set, otherwise OS temp root + `SubtitleExtractslator`.
3. Required files are `queue.txt`, `completed.txt`, `failed.txt`, `in-progress.txt`, and `run-notes.md`.

Actions:

1. Move or rebuild queue state under centralized temp storage.
2. Reconcile already-generated deterministic outputs (`<basename>.<lang>.srt`) before rewriting `queue.txt`.
3. Resume from centralized state without rescanning the full library blindly.
4. For policy details, follow `references/batching.md`.
