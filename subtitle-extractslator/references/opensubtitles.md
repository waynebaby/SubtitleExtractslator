# OpenSubtitles Reference

This file is the detailed source of truth for OpenSubtitles behavior.
`SKILL.md` keeps only mandatory decision points and links here for detailed contract.

## Credential Contract (Critical)

1. OpenSubtitles real API calls must use explicit command/tool parameters.
2. Do not rely on process environment variables for OpenSubtitles credentials.
3. Required credential parameter:
- `opensubtitlesApiKey` (CLI flag: `--opensubtitles-api-key`)
4. Optional parameters:
- `opensubtitlesUsername` (`--opensubtitles-username`)
- `opensubtitlesPassword` (`--opensubtitles-password`)
- `opensubtitlesEndpoint` (`--opensubtitles-endpoint`, default `https://api.opensubtitles.com/api/v1`)
- `opensubtitlesUserAgent` (`--opensubtitles-user-agent`, default `SubtitleExtractslator/0.1`)

## Search Strategy (Mandatory)

For every OpenSubtitles search entry (standalone search, workflow-internal search, ranked download pre-search):
1. The search request must provide two query parameters together:
- primary query: current video title/base filename
- normalized query: normalized episode-style query from full path, for example `<series_or_title> s00e00`
2. Fallback order is executed internally by MCP/CLI C# implementation:
- run primary query first
- if no candidates, retry with normalized query
3. Skill layer must not split fallback into parallel jobs.
4. If both queries still return no candidates, return not found and continue fallback branch.

## Download Modes

1. MCP download (download-only):
- Provide `fileId` from a previous `opensubtitles_search` result.
- MCP `opensubtitles_download` does not run search and does not accept rank-based selection.
2. CLI ranked download:
- Use `candidateRank` (CLI: `--candidate-rank`, default `1`).
- Must reuse the mandatory fallback-aware search strategy before rank selection.
3. CLI direct file download:
- Provide `fileId` (CLI: `--file-id`) to skip search.

## Skill Orchestration Rule (Mandatory)

1. Skill-level OpenSubtitles calls are strictly linear.
2. `opensubtitles_search` and `opensubtitles_download` must run one-by-one in sequence.
3. Parallel search/download fan-out is forbidden, even before rate-limit is observed.

## Rate-Limit Handling (Critical)

If OpenSubtitles responds with rate-limit signals (for example HTTP `429` or message `rate limit exceeded`):
1. Stop parallel OpenSubtitles calls immediately.
2. Switch to strict serial mode: one request at a time.
3. Insert an explicit wait interval between requests in serial mode.
4. Continue in serial delayed mode for the rest of the current task/session.
5. Retry per request up to 20 times when rate-limited; each retry must increase wait time.
6. If retries are exhausted and still rate-limited, stop OpenSubtitles branch and return rate-limit error.

## Header Behavior

1. Every OpenSubtitles HTTP request sends:
- `Api-Key: <opensubtitlesApiKey>`
- `User-Agent: <opensubtitlesUserAgent or default>`
2. If username/password are provided and login succeeds, authenticated requests also send bearer authorization.

## MCP Tool Parameters

1. `opensubtitles_search`:
- required: `input`, `lang`, `opensubtitlesApiKey`
- required query fields: `searchQueryPrimary`, `searchQueryNormalized`
- optional: `opensubtitlesUsername`, `opensubtitlesPassword`, `opensubtitlesEndpoint`, `opensubtitlesUserAgent`
2. `opensubtitles_download`:
- required: `fileId`, `output`, `opensubtitlesApiKey`
- optional: `opensubtitlesUsername`, `opensubtitlesPassword`, `opensubtitlesEndpoint`, `opensubtitlesUserAgent`
3. `run_workflow`:
- optional OpenSubtitles params: `opensubtitlesApiKey`, `opensubtitlesUsername`, `opensubtitlesPassword`, `opensubtitlesEndpoint`, `opensubtitlesUserAgent`
- if omitted, workflow skips OpenSubtitles branch and continues local extraction fallback.
