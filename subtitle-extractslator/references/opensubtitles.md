# OpenSubtitles Reference

This file is skill-facing runtime contract only.
Implementation rationale and internal design notes are maintained in `docs/opensubtitles-auth-and-interface-design.md`.

## Credential Contract (Critical)

1. Credentials are managed by auth commands, not by per-call username/password parameters.
2. Auth command contract:
- `subtitle auth login`:
	- required: `api-key`, `username`, `password`
	- missing required fields prompt interactively in TTY
	- password input must be hidden (no echo)
	- writes auth cache
- `subtitle auth aquire`: no arguments, read-only acquire/validate
- `subtitle auth status`: no arguments, read-only status
- `subtitle auth clear`: no arguments, clears auth cache
3. Write policy:
- only `login` writes cache
- only `clear` deletes cache
- `aquire` and OpenSubtitles operational commands are read-only
4. Optional per-call non-secret overrides:
- `opensubtitlesEndpoint` (default `https://api.opensubtitles.com/api/v1`)
- `opensubtitlesUserAgent` (default `SubtitleExtractslator/0.1`)

## Search Input Contract (CLI + MCP)

1. Required fields:
- `input`
- `lang`
- `searchQueryPrimary`
- `searchQueryNormalized`
2. Optional fields:
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
- required: `fileId`, `output`
- optional: `opensubtitlesEndpoint`, `opensubtitlesUserAgent`
2. CLI ranked download:
- `candidateRank` (default `1`) uses mandatory fallback-aware search before rank selection
3. CLI direct download:
- `fileId` skips search
4. Download endpoint `POST /download` requires both headers:
- `Api-Key`
- `Authorization: Bearer <token>`
5. Current implementation obtains bearer token from auth cache (populated by `subtitle auth login`) and validates it via `subtitle auth aquire` before each OpenSubtitles operation.
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

## Candidate Review Strategy (Target Language Strict Mode)

When searching in target language and candidate count is high but filename confidence is low, apply strict review before adoption:
1. Filename similarity review:
- remove season/episode marker tokens (`SxxEyy`, `xxyy`) from both source and candidate names
- compare remaining title tokens; require clear similarity before continuing
2. Duration review by interface:
- download candidate to temp subtitle path
- run `subtitle_timing_check` (MCP) or `subtitle-timing-check` (CLI)
- require `abs(video_duration - subtitle_last_cue_end) < 600 seconds`
3. If either review fails, skip current candidate and continue with next candidate.

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

## Auth Failure Contract (Critical)

1. Every OpenSubtitles call runs `subtitle auth aquire` preflight semantics.
2. If sk auth is empty, login fails, or permission is denied, return:
- code: `auth_relogin_required`
- message text containing explicit guidance to run `subtitle auth login` and retry.
3. Returning only an error code without login guidance text is not acceptable.
