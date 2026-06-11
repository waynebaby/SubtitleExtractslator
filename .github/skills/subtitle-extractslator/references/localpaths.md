# Local Paths

This file is skill-side local path memory for this machine.

Rules:
1. Keep absolute paths only.
2. Update this file when local runtime dependencies are installed or moved.
3. On each skill run, read this file first and reuse valid paths.
4. Runtime code must not parse this file. Agent workflow uses it as operational memory.

## FFmpeg

Use this field when probe/extract needs ffmpeg/ffprobe.

- env key: FFMPEG_BIN_DIR
- current value:
- verified on:
- platform:
- notes:

## History

- 2026-03-29: initialized localpaths.md template.
