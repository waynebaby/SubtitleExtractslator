#!/usr/bin/env python3
"""Resolve one monotonic package version shared by every runtime RID."""

from __future__ import annotations

import argparse
import json
import re
import urllib.error
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Iterable


VERSION_PATTERN = re.compile(r"^(\d+)\.(\d+)\.(\d+)(?:-beta)?$")
EXPECTED_RIDS = (
    "win-x64",
    "win-arm64",
    "linux-x64",
    "linux-arm64",
    "linux-musl-x64",
    "linux-musl-arm64",
    "osx-x64",
    "osx-arm64",
)
EXPECTED_RETIRED_PACKAGE_IDS = ("SubtitleExtractslator.Cli",)


@dataclass(frozen=True, order=True)
class NumericVersion:
    major: int
    minor: int
    patch: int

    @classmethod
    def parse(cls, value: str) -> NumericVersion | None:
        match = VERSION_PATTERN.fullmatch(value.strip())
        if match is None:
            return None
        return cls(*(int(part) for part in match.groups()))

    def as_text(self) -> str:
        return f"{self.major}.{self.minor}.{self.patch}"

    def next_patch(self) -> NumericVersion:
        return NumericVersion(self.major, self.minor, self.patch + 1)


def package_ids_from_release_set(manifest_path: Path) -> list[str]:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    runtime = manifest.get("packages", {}).get("runtime", {})
    rids = tuple(runtime.get("rids") or ())
    if rids != EXPECTED_RIDS:
        raise ValueError("Release-set RIDs do not match the supported NativeAOT package matrix.")

    retired_ids = tuple(manifest.get("retired_package_high_water", {}).get("package_ids") or ())
    if retired_ids != EXPECTED_RETIRED_PACKAGE_IDS:
        raise ValueError("Retired package high-water input does not match the previous CLI package family.")

    template = runtime.get("package_id_template")
    if template != "SubtitleExtractslator.Cli.{rid}":
        raise ValueError("Release-set package ID template is unexpected.")

    return [template.format(rid=rid) for rid in rids]


def retired_package_high_water_versions_from_release_set(manifest_path: Path) -> list[str]:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    high_water = manifest.get("retired_package_high_water", {})
    stable = high_water.get("stable")
    beta = high_water.get("beta")
    if NumericVersion.parse(stable or "") is None or "-" in stable:
        raise ValueError("Retired stable package high-water version is invalid.")
    if NumericVersion.parse(beta or "") is None or not beta.endswith("-beta"):
        raise ValueError("Retired beta package high-water version is invalid.")
    return [stable, beta]


def select_next_version(published_versions: Iterable[str], channel: str) -> str:
    if channel not in {"stable", "beta"}:
        raise ValueError(f"Unsupported release channel: {channel}")

    candidates = [
        parsed
        for parsed in (NumericVersion.parse(version) for version in published_versions)
        if parsed is not None
    ]
    if not candidates:
        raise ValueError("No valid stable or beta package high-water version was found.")

    next_version = max(candidates).next_patch().as_text()
    return f"{next_version}-beta" if channel == "beta" else next_version


def fetch_published_versions(package_id: str) -> list[str]:
    normalized_id = package_id.lower()
    url = f"https://api.nuget.org/v3-flatcontainer/{normalized_id}/index.json"
    request = urllib.request.Request(
        url,
        headers={"User-Agent": "SubtitleExtractslator-shared-version-resolver"},
    )
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            payload = json.load(response)
    except urllib.error.HTTPError as error:
        if error.code == 404:
            return []
        raise

    versions = payload.get("versions")
    if not isinstance(versions, list) or not all(isinstance(version, str) for version in versions):
        raise ValueError(f"NuGet index for {package_id} has no valid versions list.")
    return versions


def collect_published_versions(
    package_ids: Iterable[str],
    fetcher: Callable[[str], list[str]] | None = None,
) -> list[str]:
    fetch = fetcher or fetch_published_versions
    versions: list[str] = []
    for package_id in package_ids:
        package_versions = fetch(package_id)
        invalid_versions = [version for version in package_versions if NumericVersion.parse(version) is None]
        if invalid_versions:
            raise RuntimeError(
                f"NuGet index for {package_id} contains unsupported version suffixes: {invalid_versions}"
            )
        versions.extend(package_versions)
    return versions


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--release-set-file", type=Path, required=True)
    parser.add_argument("--channel", choices=("stable", "beta"), required=True)
    args = parser.parse_args()

    package_ids = package_ids_from_release_set(args.release_set_file)
    published_versions = collect_published_versions(package_ids)
    high_water_versions = retired_package_high_water_versions_from_release_set(args.release_set_file)
    print(select_next_version([*published_versions, *high_water_versions], args.channel))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())