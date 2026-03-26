# Troubleshooting

## Skill does not run commands

Check:
1. Binary exists at `./assets/bin/win-x64/SubtitleExtractslator.Cli.exe`.
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
2. If testing candidate branch, set `OPENSUBTITLES_MOCK=1`.

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
