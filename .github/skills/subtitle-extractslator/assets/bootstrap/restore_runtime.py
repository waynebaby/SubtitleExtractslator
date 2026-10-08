#!/usr/bin/env python3
"""Resolve and cache the latest channel-specific NativeAOT NuGet runtime."""

from __future__ import annotations

import argparse
import base64
import hashlib
import hmac
import io
import json
import os
import platform
import re
import shutil
import stat
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import zipfile
from pathlib import Path, PurePosixPath


NUGET_FLAT_CONTAINER = "https://api.nuget.org/v3-flatcontainer"
USER_AGENT = "SubtitleExtractslator-Runtime-Bootstrap/1.0"
PACKAGE_VERSION_PATTERN = re.compile(r"^(\d+)\.(\d+)\.(\d+)(-beta)?$")


def _detect_linux_libc() -> str:
    if any(Path(path).exists() for path in ("/lib/ld-musl-x86_64.so.1", "/lib/ld-musl-aarch64.so.1")):
        return "musl"
    try:
        result = subprocess.run(
            ["ldd", "--version"], capture_output=True, text=True, check=False, timeout=5
        )
        if "musl" in (result.stdout + result.stderr).lower():
            return "musl"
    except (OSError, subprocess.SubprocessError):
        pass
    return "glibc"


def detect_rid(system: str | None = None, machine: str | None = None, environ=None, libc: str | None = None) -> str:
    system = (system or platform.system()).lower()
    environ = os.environ if environ is None else environ
    machine = (machine or platform.machine()).lower()
    if system == "windows":
        machine = (environ.get("PROCESSOR_ARCHITEW6432") or environ.get("PROCESSOR_ARCHITECTURE") or machine).lower()
        arch = "arm64" if machine in ("arm64", "aarch64") else "x64" if machine in ("amd64", "x86_64", "x64") else None
        rid = f"win-{arch}" if arch else None
    elif system == "darwin":
        arch = "arm64" if machine in ("arm64", "aarch64") else "x64" if machine in ("amd64", "x86_64", "x64") else None
        rid = f"osx-{arch}" if arch else None
    elif system == "linux":
        arch = "arm64" if machine in ("arm64", "aarch64") else "x64" if machine in ("amd64", "x86_64", "x64") else None
        selected_libc = (libc or _detect_linux_libc()).lower()
        libc_suffix = "musl-" if selected_libc == "musl" else ""
        rid = f"linux-{libc_suffix}{arch}" if arch else None
    else:
        rid = None

    supported = {
        "win-x64", "win-arm64", "linux-x64", "linux-arm64",
        "linux-musl-x64", "linux-musl-arm64", "osx-x64", "osx-arm64",
    }
    if rid not in supported:
        raise RuntimeError(f"This operating system/architecture is not supported for NativeAOT runtime packages: {system}/{machine}.")
    return rid


def select_latest_version(versions: list[str], channel: str) -> str:
    if channel not in {"stable", "beta"}:
        raise ValueError(f"Unsupported release channel: {channel}")

    eligible = []
    for version in versions:
        match = PACKAGE_VERSION_PATTERN.fullmatch(version)
        if match is None or bool(match.group(4)) != (channel == "beta"):
            continue
        version_key = tuple(int(match.group(index)) for index in (1, 2, 3))
        eligible.append((version_key, version))
    if not eligible:
        raise RuntimeError(f"NuGet contains no {channel} versions for this runtime package.")
    return max(eligible)[1]


def _request_bytes(url: str) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=45) as response:
        return response.read()


def _cache_root() -> Path:
    if os.name == "nt":
        base = os.environ.get("LOCALAPPDATA") or str(Path.home() / "AppData" / "Local")
    else:
        base = os.environ.get("XDG_CACHE_HOME") or str(Path.home() / ".cache")
    return Path(base) / "SubtitleExtractslator" / "runtime"


def _cached_executable(runtime_dir: Path, executable_name: str, version: str) -> Path | None:
    executable = runtime_dir / executable_name
    marker = runtime_dir / ".runtime-version"
    if executable.is_file() and marker.is_file():
        marker_lines = marker.read_text(encoding="utf-8").splitlines()
        if marker_lines and marker_lines[0] == version:
            return executable
    return None


def _extract_runtime(package_bytes: bytes, rid: str, destination: Path, version: str, checksum: str) -> Path:
    expected_prefix = PurePosixPath("tools") / rid
    expected_executable = "SubtitleExtractslator.Cli.exe" if rid.startswith("win-") else "SubtitleExtractslator.Cli"
    staging = destination / "runtime"
    staging.mkdir(parents=True)

    with zipfile.ZipFile(io.BytesIO(package_bytes)) as package:
        for entry in package.infolist():
            entry_path = PurePosixPath(entry.filename)
            if entry_path.parts[:2] != expected_prefix.parts or len(entry_path.parts) <= 2:
                continue
            relative_path = entry_path.relative_to(expected_prefix)
            output_path = staging.joinpath(*relative_path.parts)
            try:
                output_path.resolve().relative_to(staging.resolve())
            except ValueError as exc:
                raise RuntimeError(f"Unsafe path in NuGet package: {entry.filename}") from exc
            if entry.is_dir():
                output_path.mkdir(parents=True, exist_ok=True)
                continue
            output_path.parent.mkdir(parents=True, exist_ok=True)
            with package.open(entry) as source, output_path.open("wb") as target:
                shutil.copyfileobj(source, target)

    executable = staging / expected_executable
    if not executable.is_file():
        raise RuntimeError(f"NuGet package did not contain tools/{rid}/{expected_executable}.")
    if os.name != "nt":
        executable.chmod(executable.stat().st_mode | stat.S_IXUSR | stat.S_IXGRP | stat.S_IXOTH)
    (staging / ".runtime-version").write_text(f"{version}\n{checksum}\n", encoding="utf-8")
    return executable


def resolve_runtime(channel: str, rid: str | None = None, cache_root: Path | None = None) -> Path:
    rid = rid or detect_rid()
    package_id = f"subtitleextractslator.cli.{rid}"
    package_base = f"{NUGET_FLAT_CONTAINER}/{package_id}"
    index = json.loads(_request_bytes(f"{package_base}/index.json"))
    version = select_latest_version(index.get("versions", []), channel)
    version_lower = version.lower()
    package_name = f"{package_id}.{version_lower}.nupkg"
    package_url = f"{package_base}/{version_lower}/{package_name}"
    checksum_url = f"{package_url}.sha512"

    root = cache_root or _cache_root()
    runtime_dir = root / channel / rid / version
    executable_name = "SubtitleExtractslator.Cli.exe" if rid.startswith("win-") else "SubtitleExtractslator.Cli"
    cached = _cached_executable(runtime_dir, executable_name, version)
    if cached:
        return cached

    checksum_bytes = _request_bytes(checksum_url)
    expected_checksum = checksum_bytes.decode("ascii").strip()
    if not expected_checksum:
        raise RuntimeError(f"NuGet returned an empty SHA-512 sidecar for {package_name}.")

    runtime_dir.parent.mkdir(parents=True, exist_ok=True)
    lock_path = runtime_dir.parent / f".{version}.lock"
    lock_fd = None
    deadline = time.monotonic() + 120
    while lock_fd is None:
        try:
            lock_fd = os.open(lock_path, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
        except FileExistsError:
            cached = _cached_executable(runtime_dir, executable_name, version)
            if cached:
                return cached
            try:
                if time.time() - lock_path.stat().st_mtime > 300:
                    lock_path.unlink()
                    continue
            except FileNotFoundError:
                continue
            if time.monotonic() >= deadline:
                raise RuntimeError(f"Timed out waiting for runtime restore lock: {lock_path}")
            time.sleep(0.25)

    try:
        cached = _cached_executable(runtime_dir, executable_name, version)
        if cached:
            return cached

        package_bytes = _request_bytes(package_url)
        actual_checksum = base64.b64encode(hashlib.sha512(package_bytes).digest()).decode("ascii")
        if not hmac.compare_digest(actual_checksum, expected_checksum):
            raise RuntimeError(f"SHA-512 verification failed for downloaded package {package_name}.")

        with tempfile.TemporaryDirectory(prefix=f"{rid}-{version}-", dir=runtime_dir.parent) as temp_dir:
            staging_root = Path(temp_dir)
            staged_executable = _extract_runtime(package_bytes, rid, staging_root, version, actual_checksum)
            if runtime_dir.exists():
                shutil.rmtree(runtime_dir)
            os.replace(staged_executable.parent, runtime_dir)
    finally:
        os.close(lock_fd)
        try:
            lock_path.unlink()
        except FileNotFoundError:
            pass

    resolved = runtime_dir / executable_name
    if not resolved.is_file():
        raise RuntimeError(f"Runtime restore completed without executable: {resolved}")
    return resolved


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--channel", required=True, choices=("stable", "beta"))
    parser.add_argument("--rid", help="Override automatic host RID detection (for testing).")
    args = parser.parse_args()
    try:
        print(resolve_runtime(args.channel, args.rid))
        return 0
    except (OSError, urllib.error.URLError, zipfile.BadZipFile, RuntimeError, ValueError, KeyError) as exc:
        parser.exit(1, f"Runtime restore failed: {exc}\n")


if __name__ == "__main__":
    raise SystemExit(main())