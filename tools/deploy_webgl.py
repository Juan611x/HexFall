"""Deploy a Unity WebGL build to S3 and invalidate CloudFront.

Usage:
  python tools/deploy_webgl.py --build-dir game/Builds/WebGL \
      --bucket <name> --distribution-id <id> [--dry-run]

Bucket and distribution can also come from HEXFALL_BUCKET and
HEXFALL_DISTRIBUTION_ID (the Terraform outputs). The runtime config.json is
generated from the HEXFALL_* variables listed in build_config().

Order matters: assets first, then config.json and index.html (the entry points,
never cached), then stale keys are removed, and the cache is invalidated last.
A visitor therefore never gets a new index.html that points at missing files.

Planning is pure (functions returning `aws` commands); only run_command touches
the outside world, so everything else is unit-tested without AWS.
"""
import argparse
import json
import os
import shlex
import subprocess
import sys
import tempfile

from serve_webgl import headers_for

ASSET_CACHE_CONTROL = "public, max-age=300"  # Unity file names are not hashed
ENTRY_CACHE_CONTROL = "no-cache"
ENTRY_POINT = "index.html"
CONFIG_KEY = "config.json"
DO_NOT_SHIP_SUFFIX = "_DoNotShip"

_PLAIN_TYPES = {
    ".html": "text/html",
    ".js": "application/javascript",
    ".json": "application/json",
    ".wasm": "application/wasm",
    ".data": "application/octet-stream",
    ".css": "text/css",
    ".png": "image/png",
    ".jpg": "image/jpeg",
    ".ico": "image/x-icon",
    ".svg": "image/svg+xml",
    ".txt": "text/plain",
}


class DeployError(Exception):
    """A problem the user can fix; printed without a traceback."""


class CommandFailed(DeployError):
    def __init__(self, command, returncode, stderr):
        super().__init__(
            f"Command failed ({returncode}): {' '.join(command)}\n{stderr}".rstrip()
        )
        self.command = command


def content_type_for(key):
    """Content-Type for an object key. `.br` files use serve_webgl's rules."""
    overrides = headers_for("/" + key)
    if "Content-Type" in overrides:
        return overrides["Content-Type"]
    _, extension = os.path.splitext(key)
    return _PLAIN_TYPES.get(extension.lower(), "application/octet-stream")


def _copy_command(source, key, bucket, cache_control):
    command = [
        "aws", "s3", "cp", source, f"s3://{bucket}/{key}",
        "--content-type", content_type_for(key),
        "--cache-control", cache_control,
        "--only-show-errors",
    ]
    encoding = headers_for("/" + key).get("Content-Encoding")
    if encoding:
        command += ["--content-encoding", encoding]
    return command


def upload_commands(keys, build_dir, bucket, config_path):
    """Ordered `aws s3 cp` commands: assets, then config.json, then index.html."""
    commands = [
        _copy_command(os.path.join(build_dir, key), key, bucket, ASSET_CACHE_CONTROL)
        for key in sorted(keys)
        if key != ENTRY_POINT
    ]
    commands.append(_copy_command(config_path, CONFIG_KEY, bucket, ENTRY_CACHE_CONTROL))
    commands.append(
        _copy_command(os.path.join(build_dir, ENTRY_POINT), ENTRY_POINT, bucket,
                      ENTRY_CACHE_CONTROL)
    )
    return commands


def list_command(bucket):
    return ["aws", "s3api", "list-objects-v2", "--bucket", bucket,
            "--query", "Contents[].Key", "--output", "json"]


def parse_remote_keys(output):
    text = output.strip()
    if not text:
        return []
    return json.loads(text) or []  # the CLI prints `null` for an empty bucket


def stale_keys(remote_keys, local_keys):
    """Remote keys no longer produced by the build. config.json is always kept."""
    keep = set(local_keys) | {CONFIG_KEY}
    return [key for key in remote_keys if key not in keep]


def delete_commands(keys, bucket):
    return [["aws", "s3", "rm", f"s3://{bucket}/{key}"] for key in keys]


def invalidation_command(distribution_id):
    return ["aws", "cloudfront", "create-invalidation",
            "--distribution-id", distribution_id, "--paths", "/*"]


def build_config(env):
    """Runtime config the client fetches at startup. Unset values become ''."""
    return {
        "environment": env.get("HEXFALL_ENVIRONMENT", ""),
        "region": env.get("HEXFALL_REGION", ""),
        "apiBaseUrl": env.get("HEXFALL_API_BASE_URL", ""),
        "realtimeUrl": env.get("HEXFALL_REALTIME_URL", ""),
        "cognito": {
            "userPoolId": env.get("HEXFALL_COGNITO_USER_POOL_ID", ""),
            "clientId": env.get("HEXFALL_COGNITO_CLIENT_ID", ""),
        },
    }


def collect_keys(build_dir):
    """Relative POSIX keys of every file in the build directory."""
    if not os.path.isdir(build_dir):
        raise DeployError(f"Build directory not found: {build_dir}")
    keys = []
    for root, dirs, files in os.walk(build_dir):
        # Unity writes debug output beside the build (e.g. Burst's
        # <Product>_BurstDebugInformation_DoNotShip); it must never be published.
        dirs[:] = [name for name in dirs if not name.endswith(DO_NOT_SHIP_SUFFIX)]
        for name in files:
            path = os.path.join(root, name)
            keys.append(os.path.relpath(path, build_dir).replace(os.sep, "/"))
    if ENTRY_POINT not in keys:
        raise DeployError(f"{ENTRY_POINT} not found in {build_dir}; is this a WebGL build?")
    return keys


def run_command(command):
    """Run a command and return its stdout. Raises CommandFailed on a non-zero exit."""
    result = subprocess.run(command, capture_output=True, text=True)
    if result.returncode != 0:
        raise CommandFailed(command, result.returncode, result.stderr.strip())
    return result.stdout


def _parse_args(argv, env):
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--build-dir", default="game/Builds/WebGL")
    parser.add_argument("--bucket", default=env.get("HEXFALL_BUCKET"))
    parser.add_argument("--distribution-id", default=env.get("HEXFALL_DISTRIBUTION_ID"))
    parser.add_argument("--dry-run", action="store_true",
                        help="print the commands without running them")
    return parser.parse_args(argv)


def deploy(args, env, runner, out):
    if not args.bucket:
        raise DeployError("Missing bucket: pass --bucket or set HEXFALL_BUCKET.")
    if not args.distribution_id:
        raise DeployError(
            "Missing distribution id: pass --distribution-id or set HEXFALL_DISTRIBUTION_ID."
        )

    keys = collect_keys(args.build_dir)
    config = build_config(env)

    def execute(command):
        if args.dry_run:
            out.write(shlex.join(command) + "\n")
            return ""
        return runner(command)

    with tempfile.TemporaryDirectory() as tmp:
        config_path = os.path.join(tmp, CONFIG_KEY)
        with open(config_path, "w", encoding="utf-8") as handle:
            json.dump(config, handle, indent=2)
        if args.dry_run:
            out.write(f"# {CONFIG_KEY}: {json.dumps(config)}\n")

        for command in upload_commands(keys, args.build_dir, args.bucket, config_path):
            execute(command)

        if args.dry_run:
            out.write("# stale keys are computed at run time from the listing below\n")
        remote = parse_remote_keys(execute(list_command(args.bucket)))
        for command in delete_commands(stale_keys(remote, keys), args.bucket):
            execute(command)

        execute(invalidation_command(args.distribution_id))


def main(argv=None, env=None, runner=run_command, out=None, err=None):
    env = os.environ if env is None else env
    out = out or sys.stdout
    err = err or sys.stderr
    try:
        args = _parse_args(argv, env)
        deploy(args, env, runner, out)
    except DeployError as error:
        err.write(f"error: {error}\n")
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
