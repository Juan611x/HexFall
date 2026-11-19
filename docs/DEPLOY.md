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
$env:HEXFALL_API_BASE_URL = terraform -chdir=infra/envs/dev output -raw name_check_url

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
export HEXFALL_API_BASE_URL=$(terraform -chdir=infra/envs/dev output -raw name_check_url)

python tools/deploy_webgl.py --build-dir game/Builds/WebGL --bucket "$BUCKET" --distribution-id "$DIST" --dry-run
python tools/deploy_webgl.py --build-dir game/Builds/WebGL --bucket "$BUCKET" --distribution-id "$DIST"
```

The Cognito and name check values come from `terraform output`, so apply the infrastructure first.

> **The variables live only in the current shell session.** A new terminal starts empty, so repeat the block above (or at least the `HEXFALL_*` lines) before every publish. **Empty Cognito values publish without any error**: the game then falls back to the in-memory fake accounts and nobody can really sign up. An empty `HEXFALL_API_BASE_URL` also publishes silently: guests then enter without the name check.

Check the values before running `deploy_webgl.py`:

```powershell
# Windows PowerShell
"AWS_PROFILE=$env:AWS_PROFILE"
"HEXFALL_REGION=$env:HEXFALL_REGION"
"HEXFALL_COGNITO_USER_POOL_ID=$env:HEXFALL_COGNITO_USER_POOL_ID"
"HEXFALL_COGNITO_CLIENT_ID=$env:HEXFALL_COGNITO_CLIENT_ID"
"HEXFALL_API_BASE_URL=$env:HEXFALL_API_BASE_URL"
"Bucket=$Bucket"
"Distribution=$Dist"
```

```bash
# bash / zsh
echo "AWS_PROFILE=$AWS_PROFILE"
echo "HEXFALL_REGION=$HEXFALL_REGION"
echo "HEXFALL_COGNITO_USER_POOL_ID=$HEXFALL_COGNITO_USER_POOL_ID"
echo "HEXFALL_COGNITO_CLIENT_ID=$HEXFALL_COGNITO_CLIENT_ID"
echo "HEXFALL_API_BASE_URL=$HEXFALL_API_BASE_URL"
echo "Bucket=$BUCKET"
echo "Distribution=$DIST"
```

All seven lines must have a value.

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
curl.exe -s "$Url/config.json"           # environment "dev", region "eu-west-1", cognito ids filled in
```

```bash
# bash / zsh
URL=$(terraform -chdir=infra/envs/dev output -raw url)
curl -sI "$URL/" | head -1
curl -sI "$URL/Build/WebGL.wasm.br" | grep -i content-encoding
curl -s "$URL/config.json"
```

`config.json` must show a non-empty `cognito.userPoolId`, `cognito.clientId` and `apiBaseUrl` (the name check URL, ending in `.lambda-url.eu-west-1.on.aws/`), not only `environment` and `region`. Empty ids mean the build was published with the fake accounts; set the variables and publish again. Then try the accounts as described in [Accounts (Cognito)](#accounts-cognito).

## Accounts (Cognito)

Player accounts live in the Cognito user pool `hexfall-dev-players` (design: [DD-014](DESIGN_DECISIONS.md#dd-014-cognito-user-pool-called-directly-by-the-client) and the [authentication journal](journal/authentication.md)). Terraform creates it with the rest of `infra/envs/dev`; the deploy script only copies its ids into `config.json`.

### Smoke test in the browser

After a publish, open the `url` output and walk through:

1. **Sign up** with a real email you can read, a wizard name and a password with 8+ characters, upper and lower case, a number and a symbol.
2. An email titled **HexFall verification code** arrives (check spam; the default sender is limited to about 50 emails a day). Enter the code: you land in the lobby.
3. **Log out and log in** again with the same email and password: lobby.
4. **Wrong password**: log in with a bad one. The message is `Incorrect email or password.`
5. **Closed tab before verifying**: sign up with another email, close the tab without entering the code, reopen the page and log in. The game sends a fresh code and opens the Verify screen; enter it and you land in the lobby.
6. **Reload keeps the session**: while in the lobby, reload the page (F5). You land in the lobby again, not on the title screen.
7. **Log out, then reload**: log out and reload. The title screen shows.
8. **Forgot password**: on Login choose **Forgot password?**. Enter the email, read the code from the email, enter it with a new password: the game signs you in and opens the lobby. Log out and log in with the new password to confirm.
9. **Duplicate wizard name**: sign up with the wizard name `Merlin`, then try another email with `merlin`. The register screen shows `That wizard name is already taken.` (see [Unique wizard names](#unique-wizard-names)).
10. **Guest with a registered name**: on the title screen choose **PLAY AS GUEST**, type `Merlin` (a registered name, any capitalisation) and press enter. The button shows `CHECKING...`, then the screen shows `That wizard name is already taken.` Press the random-name button a few times: each name appears after a short check and entering with it opens the lobby.

If sign-up never sends an email, check `config.json` first (previous section): the game is probably using the fake accounts.

### View and reset users

Console: Amazon Cognito in `eu-west-1` → user pool `hexfall-dev-players` → **Users**. Status `Confirmed` means the code was entered; `Unconfirmed` means sign-up finished but verification did not.

CLI (same profile as the rest of this runbook):

```powershell
# Windows PowerShell
$PoolId = terraform -chdir=infra/envs/dev output -raw cognito_user_pool_id
aws cognito-idp list-users --region eu-west-1 --user-pool-id $PoolId `
  --query "Users[].{email:Attributes[?Name=='email']|[0].Value,wizard:Attributes[?Name=='preferred_username']|[0].Value,status:UserStatus}" --output table

# Delete a test account so the email can sign up again
aws cognito-idp admin-delete-user --region eu-west-1 --user-pool-id $PoolId --username someone@example.com
```

```bash
# bash / zsh
POOL_ID=$(terraform -chdir=infra/envs/dev output -raw cognito_user_pool_id)
aws cognito-idp list-users --region eu-west-1 --user-pool-id "$POOL_ID"   --query "Users[].{email:Attributes[?Name=='email']|[0].Value,wizard:Attributes[?Name=='preferred_username']|[0].Value,status:UserStatus}" --output table

# Delete a test account so the email can sign up again
aws cognito-idp admin-delete-user --region eu-west-1 --user-pool-id "$POOL_ID" --username someone@example.com
```

The sign-in name is the email address. Deleting a user is permanent.

### Unique wizard names

The apply also creates the Pre Sign-up trigger ([DD-018](DESIGN_DECISIONS.md#dd-018-unique-wizard-names-reserved-by-a-pre-sign-up-trigger)): the DynamoDB table `hexfall-dev-wizard-names`, the Lambda `hexfall-dev-names-trigger`, its log group and the IAM role `student-hexfall-dev-names-trigger` (with the `StudentBoundary` boundary). If the apply fails creating the role or the table with an access-denied error, the boundary denies that action: stop and see the risk in the [authentication journal](journal/authentication.md).

Accounts created before this change are not in the table, so their names are not reserved. Delete the old test users (previous section) before testing, or a new player could take their names.

Check it by signing up twice with the same wizard name (any capitalisation) and different emails: the second shows `That wizard name is already taken.`

See the reservations:

```bash
aws dynamodb scan --region eu-west-1 --table-name hexfall-dev-wizard-names --query "Items[].{name:name.S,owner:owner_email.S,created:created_at.S}" --output table
```

Trigger logs are in CloudWatch under `/aws/lambda/hexfall-dev-names-trigger`.

### Guest name check

The same apply creates the guest name check ([DD-021](DESIGN_DECISIONS.md#dd-021-guest-names-checked-through-a-public-lambda-function-url)): the Lambda `hexfall-dev-name-check`, its log group, the role `student-hexfall-dev-name-check` (`StudentBoundary`, read-only on the names table), a public **Lambda Function URL** limited by CORS to the client origin, and two resource-based permissions that make the URL public. If the apply fails creating the URL or a permission with an access-denied error, the lab denies public Function URLs: stop and use the fallback in DD-021 (an API Gateway HTTP API over the same Lambda).

The client reads the URL from `apiBaseUrl` in `config.json`, so publish with `HEXFALL_API_BASE_URL` set (step 4). Check the endpoint directly:

```powershell
# Windows PowerShell
$Api = terraform -chdir=infra/envs/dev output -raw name_check_url
curl.exe -s "${Api}names/Merlin"      # {"name": "Merlin", "available": false} once Merlin has signed up
curl.exe -s "${Api}names/Free_Name"   # {"name": "Free_Name", "available": true}
curl.exe -s "${Api}names/no"          # {"error": "InvalidWizardName"}
```

```bash
# bash / zsh
API=$(terraform -chdir=infra/envs/dev output -raw name_check_url)
curl -s "${API}names/Merlin"
curl -s "${API}names/Free_Name"
curl -s "${API}names/no"
```

The output ends with a slash, so append the path without another one. Logs are under `/aws/lambda/hexfall-dev-name-check`.

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
| `Module not installed` after pulling infra changes | A new module (for example `user-pool`) was added since the last `init` | Re-run `terraform -chdir=infra/envs/dev init "-backend-config=backend.hcl"` |
| Sign-up never sends an email code | `config.json` has empty Cognito ids, so the client uses the fake accounts | Set the `HEXFALL_COGNITO_*` variables (new shell?) and publish again |
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
