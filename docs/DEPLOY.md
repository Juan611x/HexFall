# Deploying the WebGL client

The client is hosted on S3 + CloudFront (design: [client-hosting journal](journal/client-hosting.md)). Terraform creates the infrastructure; GitHub Actions builds, uploads and checks every release from `develop`. The reasoning behind the pipeline is in the [CI/CD journal](journal/ci-cd.md).

> Status: the pipeline is written and its unit tests pass, but it has **not run against AWS or GitHub yet**. Treat the steps below as the first-run checklist.

## One-time setup (a human, once)

You need AWS access to the lab account ([AWS_ENVIRONMENT.md](AWS_ENVIRONMENT.md)).

### 1. Bootstrap AWS

Creates the Terraform state bucket, the GitHub OIDC provider and the CI role. It uses local state, which is why it is applied by hand and only once.

The Terraform providers do not name a profile, so the shell must select it. Without it Terraform falls back to the default credential chain and fails with `InvalidClientTokenId` if those credentials are stale.

```powershell
# Windows PowerShell
aws sso login --profile commit-lab
$env:AWS_PROFILE = "commit-lab"
aws sts get-caller-identity   # must show the lab account
```

```bash
# bash / zsh
aws sso login --profile commit-lab
export AWS_PROFILE=commit-lab
aws sts get-caller-identity
```

Then, from the repository root:

```bash
terraform -chdir=infra/bootstrap init
terraform -chdir=infra/bootstrap plan
terraform -chdir=infra/bootstrap apply
terraform -chdir=infra/bootstrap output
```

Keep the output: `state_bucket_name` and `ci_role_arn` feed the GitHub variables below. The local state file (`infra/bootstrap/terraform.tfstate`) is git-ignored; keep it somewhere safe, or you cannot change the bootstrap later.

If the account refuses to create the OIDC provider (or already has one), apply with `-var create_github_oidc_provider=false`; the existing provider is then read instead. If neither works, see the open question under [Known risks](#known-risks).

### 2. Configure GitHub

Repository **Settings > Environments > New environment**: create `dev`. Then add these to that environment.

| Kind | Name | Value |
|---|---|---|
| Variable | `AWS_REGION` | `eu-west-1` |
| Variable | `AWS_ROLE_ARN` | `ci_role_arn` from the bootstrap output |
| Variable | `TF_STATE_BUCKET` | `state_bucket_name` from the bootstrap output |
| Variable | `TF_STATE_KEY` | `hexfall/dev/client.tfstate` |
| Variable | `HEXFALL_API_BASE_URL` | empty until a backend exists |
| Variable | `HEXFALL_REALTIME_URL` | empty until a backend exists |
| Variable | `HEXFALL_COGNITO_USER_POOL_ID` | empty until Cognito exists |
| Variable | `HEXFALL_COGNITO_CLIENT_ID` | empty until Cognito exists |
| Secret | `UNITY_LICENSE` | contents of the Unity license file (below) |
| Secret | `UNITY_EMAIL` | Unity account email |
| Secret | `UNITY_PASSWORD` | Unity account password |

The `UNITY_*` secrets are also needed by the PR checks. Add them as **repository** secrets too, so `ci.yml` (which has no environment) can read them. Without `UNITY_LICENSE` the PR check skips the Unity tests with a warning, and the deploy build fails with a clear message.

### 3. Unity Personal license

GameCI activates Unity with a license file.

1. Run the [GameCI activation workflow](https://game.ci/docs/github/activation) once (it uploads a `.alf` request file as an artifact), or create the `.alf` by running the Unity editor in batch mode as described there.
2. Upload the `.alf` at <https://license.unity3d.com/manual>, sign in, choose the Personal license, and download the `.ulf`.
3. Paste the full text of the `.ulf` into the `UNITY_LICENSE` secret.

Personal licenses are renewed periodically; if the build starts failing on activation, repeat the steps.

### 4. First run

Push to `develop` (or run **Deploy dev** from the Actions tab). Expect a first run to surface small problems; the list under [Known risks](#known-risks) is where to look first.

## What the workflows do

### `ci.yml` (pull requests to `develop` and `main`)

A `changes` job (dorny/paths-filter) decides what to run, so a docs-only PR costs nothing.

| Job | Runs when | Does |
|---|---|---|
| `terraform` | `infra/**` changed | `fmt -check`, `init -backend=false` + `validate` in the module, bootstrap and dev, then `terraform test` (mocked provider, no credentials) |
| `python` | `tools/**` changed | `python -m unittest discover -s tools -p "test_*.py"` |
| `unity-tests` | `game/**` changed | Unity EditMode tests with GameCI; skipped with a warning if `UNITY_LICENSE` is missing |

Changing a workflow file runs all three.

### `deploy-dev.yml` (push to `develop`, or manual)

Runs one at a time (`concurrency: deploy-dev`), in the `dev` environment, with short-lived AWS credentials from OIDC.

1. **infra**: assumes the CI role, `terraform init` with the backend configured from variables, `terraform apply -auto-approve`, exports the outputs.
2. **build**: GameCI builds WebGL by calling `WebGLBuilder.BuildFromCommandLine`, then uploads the build as an artifact. The output path is passed as `-buildOutput /github/workspace/build/WebGL`: `WebGLBuilder` reads that argument, and `/github/workspace` is where the action mounts the repository in its container, so the files land in `build/WebGL` on the runner.
3. **deploy**: downloads the artifact and runs `tools/deploy_webgl.py`.
4. **smoke**: `curl` checks `/` returns 200, `/config.json` returns 200, and the `.wasm.br` answers with `Content-Encoding: br`.

`infra` and `build` run in parallel; `deploy` waits for both.

## Deploying by hand

The pipeline calls the same script you can run locally. After a build ([BUILD.md](BUILD.md)) and `aws sso login`:

```bash
export AWS_PROFILE=commit-lab
cd infra/envs/dev
terraform init -backend-config=backend.hcl      # copy backend.hcl.example first
BUCKET=$(terraform output -raw bucket_name)
DIST=$(terraform output -raw distribution_id)
cd ../../..

python tools/deploy_webgl.py --build-dir game/Builds/WebGL \
    --bucket "$BUCKET" --distribution-id "$DIST" --dry-run   # print the plan
python tools/deploy_webgl.py --build-dir game/Builds/WebGL \
    --bucket "$BUCKET" --distribution-id "$DIST"
```

`HEXFALL_BUCKET` and `HEXFALL_DISTRIBUTION_ID` can replace the flags. `config.json` is generated from `HEXFALL_ENVIRONMENT`, `HEXFALL_REGION`, `HEXFALL_API_BASE_URL`, `HEXFALL_REALTIME_URL`, `HEXFALL_COGNITO_USER_POOL_ID` and `HEXFALL_COGNITO_CLIENT_ID`; unset values become empty strings. The script needs the AWS CLI and Python 3 only.

Order of operations: assets, then `config.json` and `index.html` (never cached), then deletion of files no longer in the build, then a `/*` invalidation.

## Known risks

Nothing here has run yet.

- **OIDC provider may be denied.** [AWS_ENVIRONMENT.md](AWS_ENVIRONMENT.md) lists what IAM allows, and creating an identity provider is not on it. If the lab denies it, the pipeline cannot assume a role without long-lived keys. **Open question:** the fallbacks are an existing provider in the account (`create_github_oidc_provider=false`), a request to the account admins, or, as a last resort, an IAM user with access keys stored as GitHub secrets (rejected in the journal for good reasons, so it needs an explicit decision).
- **GameCI image for Unity `6000.5.3f1` is unverified.** `unity-builder` pulls a Docker image per Unity version and WebGL module; it may not exist yet for this editor build.
- **Unity Personal license activation in CI** is unverified, including whether the account allows it.
- **Lab permissions for CloudFront and S3 creation** were only verified for reads. The CI role policy may need adjusting after the first `apply`.
- **Backend `use_lockfile`** needs Terraform 1.10 or later; the workflows pin `~1.10`.
- **The client does not read `config.json` yet.** The file is deployed and served, but reading it is a later task.
