import base64
import hashlib
import io
import json
import sys
import tempfile
from pathlib import Path
import unittest
import zipfile
from unittest.mock import patch

sys.path.insert(
    0,
    str(Path(__file__).resolve().parents[1] / ".github" / "skills" / "subtitle-extractslator" / "assets" / "bootstrap"),
)
import restore_runtime


class RuntimeResolutionTests(unittest.TestCase):
    def test_detects_windows_arm64_under_x64_emulation(self):
        rid = restore_runtime.detect_rid(
            "Windows",
            "AMD64",
            {"PROCESSOR_ARCHITECTURE": "AMD64", "PROCESSOR_ARCHITEW6432": "ARM64"},
        )
        self.assertEqual("win-arm64", rid)

    def test_detects_linux_musl_arm64(self):
        self.assertEqual("linux-musl-arm64", restore_runtime.detect_rid("Linux", "aarch64", {}, "musl"))

    def test_detects_macos_x64(self):
        self.assertEqual("osx-x64", restore_runtime.detect_rid("Darwin", "x86_64", {}))

    def test_rejects_32_bit_linux_arm(self):
        with self.assertRaises(RuntimeError):
            restore_runtime.detect_rid("Linux", "armv7l", {}, "glibc")

    def test_selects_latest_stable_and_beta_versions(self):
        versions = ["1.2.0", "1.3.0-beta.1", "1.3.0-beta.2", "1.3.0"]
        self.assertEqual("1.3.0", restore_runtime.select_latest_version(versions, "stable"))
        self.assertEqual("1.3.0-beta.2", restore_runtime.select_latest_version(versions, "beta"))

    def test_reuses_a_valid_versioned_cache(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            runtime_dir = Path(temp_dir)
            executable = runtime_dir / "SubtitleExtractslator.Cli"
            executable.write_bytes(b"native binary")
            (runtime_dir / ".runtime-version").write_text("1.2.3\nchecksum\n", encoding="utf-8")
            self.assertEqual(
                executable,
                restore_runtime._cached_executable(runtime_dir, executable.name, "1.2.3"),
            )

    def test_downloads_verifies_extracts_and_reuses_runtime(self):
        package_stream = io.BytesIO()
        with zipfile.ZipFile(package_stream, "w") as package:
            package.writestr("tools/win-x64/SubtitleExtractslator.Cli.exe", b"native executable")
        package_bytes = package_stream.getvalue()
        package_id = "subtitleextractslator.cli.win-x64"
        package_base = f"{restore_runtime.NUGET_FLAT_CONTAINER}/{package_id}"
        package_url = f"{package_base}/1.2.3/{package_id}.1.2.3.nupkg"
        checksum = base64.b64encode(hashlib.sha512(package_bytes).digest())
        responses = {
            f"{package_base}/index.json": json.dumps({"versions": ["1.2.3"]}).encode(),
            f"{package_url}.sha512": checksum,
            package_url: package_bytes,
        }
        cache_root = Path(tempfile.mkdtemp())
        try:
            with patch.object(restore_runtime, "_request_bytes", side_effect=lambda url: responses[url]) as request:
                executable = restore_runtime.resolve_runtime("stable", "win-x64", cache_root)
                self.assertEqual(b"native executable", executable.read_bytes())
                self.assertEqual(3, request.call_count)

            with patch.object(
                restore_runtime,
                "_request_bytes",
                side_effect=lambda url: responses[url],
            ) as request:
                self.assertEqual(executable, restore_runtime.resolve_runtime("stable", "win-x64", cache_root))
                self.assertEqual([f"{package_base}/index.json"], [call.args[0] for call in request.call_args_list])
        finally:
            import shutil
            shutil.rmtree(cache_root)


if __name__ == "__main__":
    unittest.main()