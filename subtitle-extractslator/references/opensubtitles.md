# OpenSubtitles Reference

This file is the source of truth for OpenSubtitles behavior.
`SKILL.md` keeps only mandatory decisions and links here for detailed contracts.

## Credential Contract (Critical)

1. OpenSubtitles API calls must use explicit command/tool parameters.
2. Do not rely on process environment variables for credentials.
3. Required credential parameter:
- `opensubtitlesApiKey` (CLI flag: `--opensubtitles-api-key`)
4. Optional parameters:
- `opensubtitlesUsername` (`--opensubtitles-username`)
- `opensubtitlesPassword` (`--opensubtitles-password`)
- `opensubtitlesEndpoint` (`--opensubtitles-endpoint`, default `https://api.opensubtitles.com/api/v1`)
- `opensubtitlesUserAgent` (`--opensubtitles-user-agent`, default `SubtitleExtractslator/0.1`)

## Search Input Contract (CLI + MCP)

1. Required fields:
- `input`
- `lang`
- `searchQueryPrimary`
- `searchQueryNormalized`
- `opensubtitlesApiKey`
2. Optional fields:
- `opensubtitlesUsername`
- `opensubtitlesPassword`
- `opensubtitlesEndpoint`
- `opensubtitlesUserAgent`
3. Current implementation exposes query-based search flow with language filter and any-language fallback.
4. Advanced upstream search filters (for example `imdb_id`, `tmdb_id`, `moviehash`, `season_number`) are not exposed as tool parameters yet.

## Search Output Contract

1. Search returns `OpenSubtitlesResult` with:
- `input`
- `targetLanguage`
- `candidates[]`
2. Candidate fields:
- `rank`
- `language`
- `score`
- `name`
- `source`
- `fileId`
- `downloadUrl`
3. `fileId` is extracted from OpenSubtitles response path `data[].attributes.files[].file_id` when present.
4. `fileId` can be null for some results; when null, `opensubtitles_download` by file id cannot be used for that candidate.

## Search Strategy (Mandatory)

For every OpenSubtitles search entry (standalone search, workflow-internal search, ranked download pre-search), internal C# must run this strict sequence:
1. `searchQueryPrimary` with target language
2. `searchQueryNormalized` with target language
3. `searchQueryPrimary` with any language
4. `searchQueryNormalized` with any language
5. If all four stages are empty, return not found and continue fallback branch.

## Download Input Contract

1. MCP `opensubtitles_download` (download-only):
- required: `fileId`, `output`, `opensubtitlesApiKey`
- optional: `opensubtitlesUsername`, `opensubtitlesPassword`, `opensubtitlesEndpoint`, `opensubtitlesUserAgent`
2. CLI ranked download:
- `candidateRank` (default `1`) uses mandatory fallback-aware search before rank selection
3. CLI direct download:
- `fileId` skips search
4. Download endpoint `POST /download` requires both headers:
- `Api-Key`
- `Authorization: Bearer <token>`
5. Current implementation obtains bearer token from `/login` using `opensubtitlesUsername` + `opensubtitlesPassword`.
6. If token is unavailable, `/download` branch is rejected with explicit error.
7. Optional upstream `/download` conversion fields (`sub_format`, `in_fps`, `out_fps`, `timeshift`, `force_download`) are not exposed in tool parameters yet.

## Download Output Contract

1. OpenSubtitles `/download` response includes temporary `link` and quota metadata.
2. Runtime download result returns `OpenSubtitlesDownloadResult` with:
- `input`
- `targetLanguage`
- `outputPath`
- `strategy`
- `candidateRank`
- `fileId`
- `candidateName`

## Skill Orchestration Rule (Mandatory)

1. Skill-level OpenSubtitles calls are strictly linear.
2. `opensubtitles_search` and `opensubtitles_download` must run one-by-one.
3. Parallel search/download fan-out is forbidden.
4. After `opensubtitles_download`:
- if downloaded subtitle already matches target language, finish with deterministic output naming (no translation)
- if downloaded subtitle is non-target language, continue grouped rolling-context translation to target language

## Rate-Limit Handling (Critical)

If OpenSubtitles signals rate limiting (for example HTTP `429` or explicit rate-limit text):
1. Stop parallel OpenSubtitles calls immediately.
2. Switch to strict serial mode with delay between requests.
3. Retry per request up to 20 times with increasing wait time.
4. Keep serial delayed mode for the rest of current task/session.
5. If retries are exhausted, stop OpenSubtitles branch and return rate-limit error.
