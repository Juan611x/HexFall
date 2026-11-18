import io
import json
import os
import tempfile
import unittest

import deploy_webgl as d

BUCKET = "hexfall-dev-client-123456789012"
DIST = "E123ABC"

BUILD_FILES = [
    "index.html",
    "Build/WebGL.loader.js",
    "Build/WebGL.framework.js.br",
    "Build/WebGL.wasm.br",
    "Build/WebGL.data.br",
]


def flag(command, name):
    """Value that follows `name` in a command, or None."""
    return command[command.index(name) + 1] if name in command else None


def target_key(command):
    return command[command.index("cp") + 2].split(f"s3://{BUCKET}/", 1)[1]


def uploads(config_path="/tmp/config.json"):
    return d.upload_commands(BUILD_FILES, "/build", BUCKET, config_path)


def by_key():
    return {target_key(c): c for c in uploads()}


class UploadOrderTests(unittest.TestCase):
    def test_every_file_plus_config_is_uploaded_once(self):
        keys = [target_key(c) for c in uploads()]
        self.assertCountEqual(keys, BUILD_FILES + ["config.json"])

    def test_assets_come_before_config_and_index(self):
        keys = [target_key(c) for c in uploads()]
        self.assertEqual(keys[-2:], ["config.json", "index.html"])
        self.assertEqual(len(keys), len(BUILD_FILES) + 1)

    def test_index_is_always_last_even_if_listed_first(self):
        self.assertEqual(target_key(uploads()[-1]), "index.html")


class HeaderTests(unittest.TestCase):
    def test_brotli_wasm(self):
        c = by_key()["Build/WebGL.wasm.br"]
        self.assertEqual(flag(c, "--content-type"), "application/wasm")
        self.assertEqual(flag(c, "--content-encoding"), "br")

    def test_brotli_framework_js(self):
        c = by_key()["Build/WebGL.framework.js.br"]
        self.assertEqual(flag(c, "--content-type"), "application/javascript")
        self.assertEqual(flag(c, "--content-encoding"), "br")

    def test_brotli_data(self):
        c = by_key()["Build/WebGL.data.br"]
        self.assertEqual(flag(c, "--content-type"), "application/octet-stream")
        self.assertEqual(flag(c, "--content-encoding"), "br")

    def test_loader_has_type_and_no_encoding(self):
        c = by_key()["Build/WebGL.loader.js"]
        self.assertEqual(flag(c, "--content-type"), "application/javascript")
        self.assertNotIn("--content-encoding", c)

    def test_index_is_html_without_encoding(self):
        c = by_key()["index.html"]
        self.assertEqual(flag(c, "--content-type"), "text/html")
        self.assertNotIn("--content-encoding", c)

    def test_config_is_json(self):
        c = by_key()["config.json"]
        self.assertEqual(flag(c, "--content-type"), "application/json")

    def test_unknown_extension_falls_back_to_octet_stream(self):
        self.assertEqual(d.content_type_for("Build/blob.xyz"), "application/octet-stream")


class CacheControlTests(unittest.TestCase):
    def test_assets_are_cached_five_minutes(self):
        for key in BUILD_FILES[1:]:
            self.assertEqual(
                flag(by_key()[key], "--cache-control"), "public, max-age=300", key
            )

    def test_index_and_config_are_never_cached(self):
        for key in ("index.html", "config.json"):
            self.assertEqual(flag(by_key()[key], "--cache-control"), "no-cache", key)


class CommandShapeTests(unittest.TestCase):
    def test_copy_uses_aws_s3_cp_with_local_path_and_bucket(self):
        c = by_key()["Build/WebGL.wasm.br"]
        self.assertEqual(c[:3], ["aws", "s3", "cp"])
        self.assertEqual(c[3], os.path.join("/build", "Build/WebGL.wasm.br"))
        self.assertEqual(c[4], f"s3://{BUCKET}/Build/WebGL.wasm.br")

    def test_config_is_copied_from_the_given_path(self):
        self.assertEqual(by_key()["config.json"][3], "/tmp/config.json")


class StaleKeyTests(unittest.TestCase):
    def test_remote_only_keys_are_stale(self):
        stale = d.stale_keys(["Build/old.data.br", "index.html"], ["index.html"])
        self.assertEqual(stale, ["Build/old.data.br"])

    def test_config_json_is_never_stale(self):
        stale = d.stale_keys(["config.json", "Build/old.js"], ["index.html"])
        self.assertEqual(stale, ["Build/old.js"])

    def test_nothing_remote_means_nothing_stale(self):
        self.assertEqual(d.stale_keys([], ["index.html"]), [])

    def test_delete_commands(self):
        self.assertEqual(
            d.delete_commands(["a", "b"], BUCKET),
            [["aws", "s3", "rm", f"s3://{BUCKET}/a"], ["aws", "s3", "rm", f"s3://{BUCKET}/b"]],
        )

    def test_list_output_null_means_empty_bucket(self):
        self.assertEqual(d.parse_remote_keys("null\n"), [])
        self.assertEqual(d.parse_remote_keys(""), [])
        self.assertEqual(d.parse_remote_keys('["a","b"]'), ["a", "b"])


class InvalidationTests(unittest.TestCase):
    def test_invalidates_everything(self):
        self.assertEqual(
            d.invalidation_command(DIST),
            ["aws", "cloudfront", "create-invalidation",
             "--distribution-id", DIST, "--paths", "/*"],
        )


class ConfigTests(unittest.TestCase):
    def test_defaults_are_empty_strings(self):
        self.assertEqual(
            d.build_config({}),
            {
                "environment": "",
                "region": "",
                "apiBaseUrl": "",
                "realtimeUrl": "",
                "cognito": {"userPoolId": "", "clientId": ""},
            },
        )

    def test_values_come_from_env(self):
        env = {
            "HEXFALL_ENVIRONMENT": "dev",
            "HEXFALL_REGION": "eu-west-1",
            "HEXFALL_API_BASE_URL": "https://api.example",
            "HEXFALL_REALTIME_URL": "wss://rt.example",
            "HEXFALL_COGNITO_USER_POOL_ID": "eu-west-1_abc",
            "HEXFALL_COGNITO_CLIENT_ID": "client123",
        }
        self.assertEqual(
            d.build_config(env),
            {
                "environment": "dev",
                "region": "eu-west-1",
                "apiBaseUrl": "https://api.example",
                "realtimeUrl": "wss://rt.example",
                "cognito": {"userPoolId": "eu-west-1_abc", "clientId": "client123"},
            },
        )


class FakeRunner:
    def __init__(self, remote_keys=None):
        self.calls = []
        self.remote_keys = remote_keys if remote_keys is not None else []

    def __call__(self, command):
        self.calls.append(command)
        if "list-objects-v2" in command:
            return json.dumps(self.remote_keys)
        return ""


class MainTests(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.TemporaryDirectory()
        self.addCleanup(self.dir.cleanup)
        for rel in BUILD_FILES:
            path = os.path.join(self.dir.name, *rel.split("/"))
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with open(path, "wb") as handle:
                handle.write(b"x")

    def run_main(self, argv, env=None, runner=None):
        out, err = io.StringIO(), io.StringIO()
        runner = runner or FakeRunner()
        code = d.main(argv, env=env or {}, runner=runner, out=out, err=err)
        return code, runner, out.getvalue(), err.getvalue()

    def args(self, *extra):
        return ["--build-dir", self.dir.name, "--bucket", BUCKET,
                "--distribution-id", DIST, *extra]

    def test_full_run_orders_uploads_delete_then_invalidation(self):
        runner = FakeRunner(remote_keys=["Build/stale.js", "config.json", "index.html"])
        code, runner, _, _ = self.run_main(self.args(), runner=runner)
        self.assertEqual(code, 0)
        kinds = [c[2] if c[1] != "cloudfront" else "invalidate" for c in runner.calls]
        # cp x6 (5 build files incl. index + config), list, rm, invalidate
        self.assertEqual(kinds[:6], ["cp"] * 6)
        self.assertEqual(kinds[6:], ["list-objects-v2", "rm", "invalidate"])
        self.assertEqual(target_key(runner.calls[5]), "index.html")
        rm = runner.calls[7]
        self.assertEqual(rm[-1], f"s3://{BUCKET}/Build/stale.js")
        self.assertEqual(runner.calls[-1][:3], ["aws", "cloudfront", "create-invalidation"])

    def test_config_json_written_from_env_is_what_gets_uploaded(self):
        captured = {}

        def runner(command):
            if "cp" in command and command[command.index("cp") + 2].endswith("/config.json"):
                with open(command[3], encoding="utf-8") as handle:
                    captured["config"] = json.load(handle)
            return "[]" if "list-objects-v2" in command else ""

        self.run_main(self.args(), env={"HEXFALL_ENVIRONMENT": "dev"}, runner=runner)
        self.assertEqual(captured["config"]["environment"], "dev")

    def test_bucket_and_distribution_can_come_from_env(self):
        code, runner, _, _ = self.run_main(
            ["--build-dir", self.dir.name],
            env={"HEXFALL_BUCKET": BUCKET, "HEXFALL_DISTRIBUTION_ID": DIST},
        )
        self.assertEqual(code, 0)
        self.assertIn(DIST, runner.calls[-1])

    def test_missing_bucket_is_a_clear_error(self):
        code, runner, _, err = self.run_main(
            ["--build-dir", self.dir.name, "--distribution-id", DIST]
        )
        self.assertNotEqual(code, 0)
        self.assertIn("bucket", err.lower())
        self.assertIn("HEXFALL_BUCKET", err)
        self.assertEqual(runner.calls, [])

    def test_missing_distribution_is_a_clear_error(self):
        code, runner, _, err = self.run_main(
            ["--build-dir", self.dir.name, "--bucket", BUCKET]
        )
        self.assertNotEqual(code, 0)
        self.assertIn("HEXFALL_DISTRIBUTION_ID", err)
        self.assertEqual(runner.calls, [])

    def test_missing_index_html_is_an_error(self):
        os.remove(os.path.join(self.dir.name, "index.html"))
        code, runner, _, err = self.run_main(self.args())
        self.assertNotEqual(code, 0)
        self.assertIn("index.html", err)
        self.assertEqual(runner.calls, [])

    def test_missing_build_dir_is_an_error(self):
        code, runner, _, err = self.run_main(
            ["--build-dir", os.path.join(self.dir.name, "nope"),
             "--bucket", BUCKET, "--distribution-id", DIST]
        )
        self.assertNotEqual(code, 0)
        self.assertIn("build", err.lower())
        self.assertEqual(runner.calls, [])

    def test_dry_run_executes_nothing_and_prints_the_plan(self):
        code, runner, out, _ = self.run_main(self.args("--dry-run"))
        self.assertEqual(code, 0)
        self.assertEqual(runner.calls, [])
        self.assertIn("aws s3 cp", out)
        self.assertIn("create-invalidation", out)
        self.assertLess(out.index("index.html"), out.index("create-invalidation"))

    def test_failing_command_stops_the_deploy(self):
        calls = []

        def runner(command):
            calls.append(command)
            raise d.CommandFailed(command, 1, "boom")

        code, _, _, err = self.run_main(self.args(), runner=runner)
        self.assertNotEqual(code, 0)
        self.assertEqual(len(calls), 1)
        self.assertIn("boom", err)


if __name__ == "__main__":
    unittest.main()
