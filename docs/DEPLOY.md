# Deploying the WebGL client

The client is hosted on S3 + CloudFront (design: [client-hosting journal](journal/client-hosting.md)). Terraform creates the infrastructure and `tools/deploy_webgl.py` publishes each build.

> **Status:** deploys run **by hand** from a developer machine. Automated deployment (CI/CD) is parked: the lab account gives GitHub Actions no way to authenticate to AWS (see [CI/CD status](#cicd-status)). Pull request checks (`ci.yml`) still run, because they never touch AWS.

## The flow at a glance

```
Once per AWS account     1. Bootstrap ............ state bucket            (infra/bootstrap, local state)
Once per environment     2. Infrastructure ....... bucket + CloudFront     (infra/envs/<env>, state in S3)
Every new game build     3. Build ................ tools/build-webgl.ps1   (Unity closed)
                         4. Publish .............. tools/deploy_webgl.py   (upload + invalidate)
                         5. Check ................ open the URL
```

Infrastructure changes (Terraform code under `infra/`) mean re-running step 2. A new game build is only steps 3 to 5.

| What | Where |
|---|---|
| Bootstrap state | `infra/bootstrap/terraform.tfstate` on the machine that applied it (git-ignored) |
| Environment state | S3 bucket `<project>-tfstate-<account-id>`, key `hexfall/<env>/client.tfstate` |
| Backend settings | `infra/envs/<env>/backend.hcl` (git-ignored; copy of `backend.hcl.example`) |
| Bucket name, distribution id, URL | `terraform -chdir=infra/envs/<env> output` |
| Build output | `game/Builds/WebGL` (git-ignored) |

## Before you start

You need AWS access to the lab account ([AWS_ENVIRONMENT.md](AWS_ENVIRONMENT.md)), Terraform 1.10 or later, the AWS CLI and Python 3.

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

All commands below run from the repository root.

## 1. Bootstrap (once per account)

Creates the bucket that holds Terraform state for every other configuration. It keeps its own state locally, which is why a person applies it once.

```bash
terraform -chdir=infra/bootstrap init
terraform -chdir=infra/bootstrap apply
terraform -chdir=infra/bootstrap output state_bucket_name
```

The local state file (`infra/bootstrap/terraform.tfstate`) is git-ignored. Keep it somewhere safe, or you cannot change the bootstrap later.

CI access (OIDC provider, CI role and policy) is behind `enable_ci_access`, off by default. Leave it off in the lab account; see [CI/CD status](#cicd-status).

## 2. Infrastructure for an environment

Copy the backend example and replace `<account-id>` with the account id (the end of `state_bucket_name`). `backend.hcl` is git-ignored because it names the account.

```powershell
# Windows PowerShell
Copy-Item infra/envs/dev/backend.hcl.example infra/envs/dev/backend.hcl
```

```bash
# bash / zsh
cp infra/envs/dev/backend.hcl.example infra/envs/dev/backend.hcl
```

Then, in either shell:

```bash
terraform -chdir=infra/envs/dev init "-backend-config=backend.hcl"   # quotes: PowerShell splits -flag=a.b otherwise
terraform -chdir=infra/envs/dev plan
terraform -chdir=infra/envs/dev apply
terraform -chdir=infra/envs/dev output url
```

Re-run `apply` only when the infrastructure code changes. A new game build does not need it.

## 3. Build

With the Unity editor closed ([BUILD.md](BUILD.md)):

```powershell
powershell -File tools/build-webgl.ps1
```

Restore the files a batch build re-serializes before committing anything (BUILD.md, "Build side effects").

## 4. Publish

```powershell
# Windows PowerShell
$env:HEXFALL_ENVIRONMENT = "dev"
$env:HEXFALL_REGION = "eu-west-1"
$Bucket = terraform -chdir=infra/envs/dev output -raw bucket_name
$Dist = terraform -chdir=infra/envs/dev output -raw distribution_id
$env:HEXFALL_REGION = terraform -chdir=infra/envs/dev output -raw aws_region
$env:HEXFALL_COGNITO_USER_POOL_ID = terraform -chdir=infra/envs/dev output -raw cognito_user_pool_id
$env:HEXFALL_COGNITO_CLIENT_ID = terraform -chdir=infra/envs/dev output -raw cognito_client_id

python tools/deploy_webgl.py --build-dir game/Builds/WebGL --bucket $Bucket --distribution-id $Dist --dry-run
python tools/deploy_webgl.py --build-dir game/Builds/WebGL --bucket $Bucket --distribution-id $Dist
```

```bash
# bash / zsh
export HEXFALL_ENVIRONMENT=dev HEXFALL_REGION=eu-west-1
BUCKET=$(terraform -chdir=infra/envs/dev output -raw bucket_name)
DIST=$(terraform -chdir=infra/envs/dev output -raw distribution_id)
export HEXFALL_REGION=$(terraform -chdir=infra/envs/dev output -raw aws_region)
export HEXFALL_COGNITO_USER_POOL_ID=$(terraform -chdir=infra/envs/dev output -raw cognito_user_pool_id)
export HEXFALL_COGNITO_CLIENT_ID=$(terraform -chdir=infra/envs/dev output -raw cognito_client_id)

python tools/deploy_webgl.py --build-dir game/Builds/WebGL --bucket "$BUCKET" --distribution-id "$DIST" --dry-run
python tools/deploy_webgl.py --build-dir game/Builds/WebGL --bucket "$BUCKET" --distribution-id "$DIST"
```

The Cognito values come from `terraform output`, so apply the infrastructure first. Without them the client falls back to the in-memory fake accounts.

Run it only after the build has finished: `build-webgl.ps1` prints `HEXFALL_BUILD_RESULT Succeeded`. A half-written build (for example a 0-byte `WebGL.wasm.br`) would be published as is.

`--dry-run` prints every command without running it. Folders ending in `_DoNotShip` (Unity debug output such as Burst symbols) are never uploaded. `HEXFALL_BUCKET` and `HEXFALL_DISTRIBUTION_ID` can replace the flags.

Order of operations: assets, then `config.json` and `index.html` (never cached), then deletion of files no longer in the build, then a `/*` invalidation.

`config.json` is generated from `HEXFALL_ENVIRONMENT`, `HEXFALL_REGION`, `HEXFALL_API_BASE_URL`, `HEXFALL_REALTIME_URL`, `HEXFALL_COGNITO_USER_POOL_ID` and `HEXFALL_COGNITO_CLIENT_ID`; unset values become empty strings. The client fetches it at startup, next to `index.html`. When `region`, `cognito.userPoolId` and `cognito.clientId` are all set it signs players in through Amazon Cognito; otherwise (and in the Unity Editor) it uses the in-memory fake accounts. Backend URLs can be changed without rebuilding the game.

## 5. Check

Open the `url` output. CloudFront can take a few minutes to serve a brand-new distribution and to apply an invalidation. Audio starts after the first click.

```powershell
# Windows PowerShell (curl.exe, not the curl alias of Invoke-WebRequest)
$Url = terraform -chdir=infra/envs/dev output -raw url
curl.exe -sI "$Url/"                     # first line: HTTP/1.1 200 or HTTP/2 200
curl.exe -sI "$Url/Build/WebGL.wasm.br"  # must include content-encoding: br
curl.exe -s "$Url/config.json"           # environment "dev", region "eu-west-1"
```

```bash
# bash / zsh
URL=$(terraform -chdir=infra/envs/dev output -raw url)
curl -sI "$URL/" | head -1
curl -sI "$URL/Build/WebGL.wasm.br" | grep -i content-encoding
curl -s "$URL/config.json"
```

## Tearing down an environment

```bash
terraform -chdir=infra/envs/dev destroy
```

`force_destroy` is on in dev, so the bucket is emptied and removed with its files. CloudFront takes a few minutes to disable and delete the distribution. The state bucket is protected (`prevent_destroy`) and is not touched; removing it means editing the bootstrap on purpose.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `InvalidClientTokenId` on `plan` | No profile selected; Terraform used stale default credentials | `$env:AWS_PROFILE = "commit-lab"` (or `export`) |
| `ExpiredToken` / `Token has expired` | The SSO session ended (it lasts hours) | `aws sso login --profile commit-lab` |
| `No positional arguments are expected` | PowerShell split `-backend-config=backend.hcl` at the dot | Quote it: `"-backend-config=backend.hcl"` |
| `AccessDenied` creating IAM resources | The lab only allows roles/policies `student-*`; no OIDC providers or users | Expected; see [CI/CD status](#cicd-status) |
| The page shows the previous build | CloudFront or the browser cache | Wait 1–2 minutes for the invalidation; hard refresh (Ctrl+F5) |
| `403 AccessDenied` (XML) for a file | The file is not in the bucket (no `s3:ListBucket`, so missing files answer 403, not 404) | Check the path in the build and re-run publish |
| The game fails to load, `.wasm` errors in the console | Wrong `Content-Encoding`/`Content-Type` on an object, or a half-written build was published | Re-run publish after a finished build; check headers with `curl.exe -sI` |
| Settings files show as modified after a build | Batch builds re-serialize them | `git restore game/Assets/Settings game/ProjectSettings/ProjectSettings.asset` and delete `game/Data` (BUILD.md) |

## CI/CD status

Parked on 2026-11-18. The pipeline code exists and its tests pass, but it cannot authenticate to AWS in the lab account:

| Tried | Result |
|---|---|
| Create the GitHub OIDC provider (`iam:CreateOpenIDConnectProvider`) | `AccessDenied`: no identity-based policy allows it |
| Reuse an existing provider | none in the account (`list-open-id-connect-providers` is empty) |
| IAM user with access keys (`iam:CreateUser`) | `AccessDenied` |

Ways forward, when CI/CD is picked up again:

1. **Ask the lab admins to create the GitHub OIDC provider** (`token.actions.githubusercontent.com`, audience `sts.amazonaws.com`). Then apply the bootstrap with `-var enable_ci_access=true -var create_github_oidc_provider=false` and restore the `push` trigger in `deploy-dev.yml`. Nothing else changes.
2. **Run the deploy in AWS CodeBuild** with our own role `student-*` trusted by `codebuild.amazonaws.com` (roles `student-*` are allowed; only the GitHub trust is blocked), triggered by a GitHub webhook. Needs verifying that the lab allows CodeBuild and a GitHub connection, and building Unity inside CodeBuild.

When CI access exists, GitHub also needs: Environment `dev` with variables `AWS_REGION`, `AWS_ROLE_ARN`, `TF_STATE_BUCKET`, `TF_STATE_KEY` (`hexfall/dev/client.tfstate`), the `HEXFALL_*` variables above, and secrets `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` (also as repository secrets for `ci.yml`). The Unity Personal license is obtained with the [GameCI activation steps](https://game.ci/docs/github/activation).

## What the workflows do

### `ci.yml` (pull requests to `develop` and `main`) — active

A `changes` job (dorny/paths-filter) decides what to run, so a docs-only PR costs nothing.

| Job | Runs when | Does |
|---|---|---|
| `terraform` | `infra/**` changed | `fmt -check`, `init -backend=false` + `validate` in the module, bootstrap and dev, then `terraform test` (mocked provider, no credentials) |
| `python` | `tools/**` changed | `python -m unittest discover -s tools -p "test_*.py"` |
| `unity-tests` | `game/**` changed | Unity EditMode tests with GameCI; skipped with a warning if `UNITY_LICENSE` is missing |

Changing a workflow file runs all three.

### `deploy-dev.yml` — parked (manual trigger only)

Runs the same four steps as a manual deploy, in the `dev` environment, one at a time:

1. **infra**: assumes the CI role, `terraform init` with the backend from variables, `terraform apply -auto-approve`, exports the outputs.
2. **build**: GameCI builds WebGL through `WebGLBuilder.BuildFromCommandLine` with `-buildOutput /github/workspace/build/WebGL` (where the action mounts the repository), and uploads the build as an artifact.
3. **deploy**: downloads the artifact and runs `tools/deploy_webgl.py`.
4. **smoke**: `curl` checks `/` and `/config.json` return 200 and the `.wasm.br` answers with `Content-Encoding: br`.

## Known risks

- **Lab permissions for creating CloudFront and S3 resources** were only verified for reads. The first `apply` of `infra/envs/dev` is the real test.
- **GameCI image for Unity `6000.5.3f1`** and **Unity Personal license activation in CI** are unverified (only relevant once CI/CD resumes).
- **Backend `use_lockfile`** needs Terraform 1.10 or later.
