#!/usr/bin/env python3
"""Update one package index channel's exact shared package-version block."""

from __future__ import annotations

import argparse
import re
from pathlib import Path


START_MARKER = "<!-- package-version-block:start -->"
END_MARKER = "<!-- package-version-block:end -->"


def render_block(channel: str, version: str, language: str) -> str:
    if channel == "stable":
        if language == "zh-CN":
            text = (
                f"- 当前最新已发布的 stable NativeAOT runtime 版本是 `{version}`。\n"
                "- Stable 使用全部 stable/Beta RID 包中的最高数值版本递增 patch；`main` 输出纯数字版本，不加预发布后缀。"
            )
        else:
            text = (
                f"- Current latest published stable NativeAOT runtime version: `{version}`.\n"
                "- Stable selects the next patch after the highest numeric version across stable/Beta RID packages; `main` has no prerelease suffix."
            )
    elif language == "zh-CN":
        text = (
            f"- 当前最新已发布的 Beta NativeAOT runtime 版本是 `{version}`。\n"
            "- Beta 使用全部 stable/Beta RID 包中的最高数值版本递增 patch，并且只追加精确后缀 `-beta`；不使用 alpha、preview 或 RC。"
        )
    else:
        text = (
            f"- Current latest published Beta NativeAOT runtime version: `{version}`.\n"
            "- Beta selects the next patch after the highest numeric version across stable/Beta RID packages and appends exactly `-beta`; alpha, preview, and RC suffixes are not used."
        )
    return f"{START_MARKER}\n{text}\n{END_MARKER}"


def update_package_index(path: Path, channel: str, version: str, language: str = "en") -> None:
    text = path.read_text(encoding="utf-8")
    if text.count(START_MARKER) != 1 or text.count(END_MARKER) != 1:
        raise ValueError(f"Package index must contain exactly one version marker pair: {path}")
    start = text.index(START_MARKER)
    end = text.index(END_MARKER, start) + len(END_MARKER)
    text = text[:start] + render_block(channel, version, language) + text[end:]
    path.write_text(text, encoding="utf-8", newline="\n")


def update_skill_metadata(path: Path, channel: str, version: str) -> None:
    text = path.read_text(encoding="utf-8")
    frontmatter = re.match(r"\A---\r?\n(?P<body>.*?)\r?\n---(?P<tail>.*)\Z", text, re.DOTALL)
    if frontmatter is None:
        raise ValueError(f"Skill file has no YAML frontmatter: {path}")

    body = frontmatter.group("body")
    version_pattern = re.compile(r"(?m)^  version:\s*[^\r\n]+$")
    channel_pattern = re.compile(r"(?m)^  channel:\s*(?:stable|beta)\s*$")
    if len(version_pattern.findall(body)) != 1 or len(channel_pattern.findall(body)) != 1:
        raise ValueError(f"Skill metadata must contain exactly one version and one stable/beta channel: {path}")
    body = version_pattern.sub(f"  version: {version}", body, count=1)
    body = channel_pattern.sub(f"  channel: {channel}", body, count=1)
    updated = f"---\n{body}\n---{frontmatter.group('tail')}"
    path.write_text(updated, encoding="utf-8", newline="\n")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repository-root", type=Path, default=Path.cwd())
    parser.add_argument("--channel", choices=("stable", "beta"), required=True)
    parser.add_argument("--version", required=True)
    args = parser.parse_args()

    filenames = (
        ("packages.released.md", "packages.released.zh-CN.md")
        if args.channel == "stable"
        else ("packages.beta.md", "packages.beta.zh-CN.md")
    )
    update_package_index(args.repository_root / filenames[0], args.channel, args.version)
    update_package_index(args.repository_root / filenames[1], args.channel, args.version, "zh-CN")
    update_skill_metadata(
        args.repository_root / ".github/skills/subtitle-extractslator/SKILL.md",
        args.channel,
        args.version,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())