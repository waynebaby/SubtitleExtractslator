import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

import ResolveSharedPackageVersion as resolver
import update_package_index_version


class SharedVersionTests(unittest.TestCase):
    def test_next_patch_is_monotonic_across_stable_and_beta(self):
        versions = ["0.1.20", "0.2.0-beta", "0.2.0-alpha.6", "0.1.99"]
        self.assertEqual("0.2.1", resolver.select_next_version(versions, "stable"))
        self.assertEqual("0.2.1-beta", resolver.select_next_version(versions, "beta"))

    def test_only_stable_and_exact_beta_versions_are_valid(self):
        self.assertEqual(resolver.NumericVersion(1, 2, 3), resolver.NumericVersion.parse("1.2.3"))
        self.assertEqual(resolver.NumericVersion(1, 2, 3), resolver.NumericVersion.parse("1.2.3-beta"))
        self.assertIsNone(resolver.NumericVersion.parse("1.2.3-alpha.6"))
        self.assertIsNone(resolver.NumericVersion.parse("1.2.3-rc.1"))

    def test_version_resolver_reads_release_set_and_legacy_high_water(self):
        release_set = Path(__file__).resolve().parents[1] / "release-set.json"
        package_ids = resolver.package_ids_from_release_set(release_set)
        self.assertEqual(8, len(package_ids))
        self.assertNotIn("SubtitleExtractslator.Cli", package_ids)
        self.assertEqual("0.1.20", resolver.retired_package_high_water_versions_from_release_set(release_set)[0])
        self.assertEqual("0.2.0-beta", resolver.retired_package_high_water_versions_from_release_set(release_set)[1])

    def test_shared_package_ids_and_missing_initial_nuget_indexes_use_high_water(self):
        release_set = Path(__file__).resolve().parents[1] / "release-set.json"
        package_ids = resolver.package_ids_from_release_set(release_set)
        versions = resolver.collect_published_versions(package_ids, lambda _: [])
        high_water = resolver.retired_package_high_water_versions_from_release_set(release_set)
        self.assertEqual("0.2.1-beta", resolver.select_next_version([*versions, *high_water], "beta"))

    def test_active_package_index_rejects_non_beta_prerelease_suffix(self):
        with self.assertRaisesRegex(RuntimeError, "unsupported version suffixes"):
            resolver.collect_published_versions(
                ["SubtitleExtractslator.Cli.win-x64"],
                lambda _: ["0.2.1-alpha.1"],
            )

    def test_package_index_version_block_is_exactly_channel_specific(self):
        stable = update_package_index_version.render_block("stable", "0.2.2", "en")
        beta = update_package_index_version.render_block("beta", "0.2.1-beta", "en")
        self.assertIn("`0.2.2`", stable)
        self.assertIn("no prerelease suffix", stable)
        self.assertIn("`0.2.1-beta`", beta)
        self.assertIn("exactly `-beta`", beta)

    def test_package_index_updater_replaces_only_the_marked_version_block(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            index_path = Path(temp_dir) / "packages.beta.md"
            index_path.write_text(
                "# Index\n\n"
                + update_package_index_version.START_MARKER
                + "\nold version\n"
                + update_package_index_version.END_MARKER
                + "\n\n## Install\n",
                encoding="utf-8",
            )
            update_package_index_version.update_package_index(index_path, "beta", "0.2.1-beta")
            updated = index_path.read_text(encoding="utf-8")
            self.assertIn("`0.2.1-beta`", updated)
            self.assertIn("## Install", updated)
            self.assertNotIn("old version", updated)

    def test_skill_metadata_sync_updates_package_version_and_channel(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            skill_path = Path(temp_dir) / "SKILL.md"
            skill_path.write_text(
                "---\nmetadata:\n  version: 0.1.19\n  channel: beta\n---\n\n# Skill\n",
                encoding="utf-8",
            )
            update_package_index_version.update_skill_metadata(skill_path, "stable", "0.2.2")
            updated = skill_path.read_text(encoding="utf-8")
            self.assertIn("  version: 0.2.2", updated)
            self.assertIn("  channel: stable", updated)


if __name__ == "__main__":
    unittest.main()