# Troubleshooting

## Skill does not run commands

Check:
1. Binary exists at the platform-matching path under `./assets/bin/` (for example `win-x64`, `win-arm64`, `linux-x64`, `linux-musl-x64`, `linux-arm64`, `linux-musl-arm64`, `linux-arm`, `osx-arm64`, `osx-x64`).
2. Host environment allows local executable invocation.
3. Current working directory is the skill root when using relative paths.

## No subtitle tracks found

Possible causes:
1. Source media has no embedded subtitle stream.
2. ffprobe is unavailable in environment.
3. Input path is wrong.

Actions:
1. Validate file path.
2. Run ffprobe manually.
3. Use external subtitle file as direct input.

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
