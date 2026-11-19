# Journal: CI/CD Pipeline for the Client

This entry tells how we designed the pipeline that turns a push to `develop` into a running game at a public URL. It is a design and an implementation of that design, not a record of a successful run: at the time of writing the pipeline has not been executed against AWS, GitHub or Unity in CI. The operational steps live in [DEPLOY.md](../DEPLOY.md). The infrastructure it deploys onto is described in [client-hosting.md](client-hosting.md).

## What we wanted

A change merged into `develop` should end up playable in a browser without anyone running commands. Everything that can break a deploy should be caught earlier, on the pull request, where it is cheap.

That gives two workflows. The first, `ci.yml`, runs on pull requests and only checks. The second, `deploy-dev.yml`, runs on pushes to `develop` and changes things in the world.

## Stages

The deploy workflow has four jobs: **infra**, **build**, **deploy** and **smoke**.

Infra and build do not depend on each other, so they run in parallel. Infra applies Terraform; build takes the longest of anything here (a WebGL build is about six minutes locally, plus a cold Unity container in CI), so overlapping it with the apply saves real time. Deploy needs both: it needs the bucket name and distribution id from infra, and the files from build. Smoke comes last and asks the public URL three questions: does `/` answer, does `/config.json` answer, and does the `.wasm.br` come back with `Content-Encoding: br`. The third one is the check that would have caught the failure we spent most of the hosting journal worrying about.

We also made the deploy run one at a time with `concurrency`, and chose not to cancel an in-progress run. Cancelling a half-finished `terraform apply` or a half-finished upload is worse than making the next push wait.

## Terraform owns the infrastructure, the script owns the releases

The hosting journal already settled this split, and the pipeline keeps it. The `infra` job runs Terraform and nothing else; the `deploy` job runs `tools/deploy_webgl.py` and nothing else. The only things that cross between them are the Terraform outputs (bucket name, distribution id, URL).

The practical consequence is that a normal release never produces a Terraform plan. And the script is the same one a developer runs by hand, so there is one way to deploy, not a CI way and a local way that drift apart. It is also why the script is written as pure functions that build `aws` commands, with a thin runner at the edge: nearly all of it is tested without touching AWS.

## OIDC instead of access keys

The pipeline needs AWS credentials. The simple way is an IAM user, an access key, and two GitHub secrets. We rejected it for three reasons.

- A long-lived key is a standing liability. If it leaks, it works until someone notices and rotates it.
- It is permanent access for a project that deploys a few times a week.
- The lab account manages people through IAM Identity Center and does not expect users with keys.

With GitHub's OIDC, the workflow gets a signed token that says which repository and branch it is, and AWS trades it for credentials that last about an hour. Nothing secret is stored in GitHub. What is stored (the role ARN, the region, the state bucket name) is not secret at all, so it lives in plain variables.

The trust policy is the security boundary, so it is narrow. The role can only be assumed for the audience `sts.amazonaws.com` and for subjects of this repository on `develop`, on `main`, or in a GitHub Environment. A pull request from a fork produces a different subject and cannot match. The role is named `student-ci-deploy` and carries the `StudentBoundary` boundary because the lab only lets roles exist on those terms.

The role's own permissions are written by hand and kept small: state bucket access, management of buckets that match `hexfall-*-client-*`, CloudFront distributions, access controls and invalidations, and nothing else. Each statement carries a comment naming the pipeline step that needs it. Some CloudFront actions have to use `*` as the resource, because a create call cannot name an ARN that does not exist yet; that is a limit of the service, not a shortcut.

## Why the state bucket is a separate, manual step

There is a chicken-and-egg problem. Terraform stores its state in an S3 bucket, so something has to create that bucket before Terraform can use it, and the CI role cannot exist before something creates the OIDC provider that trusts it.

The `infra/bootstrap` root solves it by using local state and being applied once, by a person, with their own credentials. It creates the state bucket (versioned, encrypted, private, protected with `prevent_destroy`), the OIDC provider and the CI role. Everything after that, including all of the pipeline, uses the bucket it created. The cost is one manual step and a local state file to keep; the alternative, a pipeline that creates its own foundations, would need credentials before it can have any.

One variable, `create_github_oidc_provider`, exists purely because we do not know whether the lab will let us create the provider. It can be turned off to read an existing one instead. If neither works, the design has an open question, recorded in DEPLOY.md, not a hidden assumption.

## Runtime `config.json` instead of compiled-in URLs

The client will eventually need to know where the API, the realtime endpoint and Cognito are. The tempting approach is to write them into the Unity build. We avoided it.

A value compiled into the build ties that build to one environment. To promote the same build from dev to production you would have to rebuild it, and what you tested would not be what you shipped. Instead the deploy script writes a small `config.json` next to the game from environment variables, and the client will fetch it at startup. One build, many environments; the environment decides, not the binary.

The file is uploaded with `Cache-Control: no-cache` so a configuration change is visible on the next load. The client does not read it yet; there is no backend to point at. We ship the file now so the contract exists, and left reading it for the task that has something to read.

## Upload order

The script uploads in a deliberate order.

1. **Assets first.** The `.br` files, the loader, everything except the entry point.
2. **`config.json` and `index.html` last**, both with `no-cache`.
3. **Delete files that are no longer in the build.**
4. **Invalidate `/*`.**

The reason is what a visitor sees mid-deploy. If `index.html` went first, a visitor could receive the new page while the files it points to were still the old ones, or missing. Uploading the entry point last means the page only changes once everything it needs is in place. Deleting stale files after the upload, not before, means the old version stays complete until the new one is. The invalidation is last because there is nothing to invalidate until the content is final.

Unity does not hash its file names, so the assets get a short cache (`max-age=300`) as a compromise: long enough to help repeat visitors, short enough that a missed invalidation heals itself within five minutes.

## Pull request checks

The PR workflow is about speed and cost. Paths are filtered, so a change to documentation runs nothing and a change to Terraform runs only the Terraform checks. We picked `dorny/paths-filter` over per-job logic because it expresses the three groups once, in one place.

The Terraform tests use a mocked AWS provider, so they need no credentials and cannot touch anything. That is what lets them run on pull requests, including from forks, with no secrets.

Unity tests are different. They need a license, which is a secret, and secrets are not visible to a job-level `if`. The job therefore checks inside a step and, if the license is missing, skips the rest with a visible warning instead of failing. A contributor without the secret still gets a green check for everything else.

## Where we stopped

The pipeline is written, its unit-testable parts are tested, and the Terraform configuration validates. It has not run. The things we do not know yet are:

- whether the lab allows creating the OIDC provider;
- whether a GameCI image exists for Unity `6000.5.3f1`;
- whether a Unity Personal license activates in CI;
- whether the CI role needs more permissions than we granted once `apply` meets the real account.

Each of them is listed in DEPLOY.md under known risks, so the first run is a checklist and not a surprise.

## Meeting the real account

The first of those questions was answered quickly, and not the way we hoped.

Running the bootstrap for real taught us something small first. The Terraform providers do not name an AWS profile, so the first `plan` used whatever stale credentials the default chain found and failed with `InvalidClientTokenId`. Selecting the SSO profile (`AWS_PROFILE=commit-lab`) fixed it, and the runbook now says so for both PowerShell and bash.

The `apply` then created the state bucket and the CI policy, and stopped at the OIDC provider: `iam:CreateOpenIDConnectProvider` is not allowed for the lab role. We checked whether the admins had already created one we could reuse. The list was empty. We tried the fallback we had ranked last, an IAM user with access keys, and `iam:CreateUser` was denied too. So in this account GitHub Actions has no door into AWS at all: no federated identity, and no user to hold a key.

Two doors are still possible. The lab admins could create the GitHub provider, which would leave everything we wrote untouched. Or the deploy could run inside AWS CodeBuild with our own `student-*` role, because the lab does let us create roles; it only blocks trusting GitHub. Both cost time we would rather spend on the game right now.

So we parked the automation. What we needed today was a public URL, and every piece of the pipeline is a step a person can run: `terraform apply`, the Unity build script, the deploy script. The bootstrap now creates only the state bucket unless `enable_ci_access` is switched on, the deploy workflow runs only when triggered by hand, and the pull request checks stay on because they never needed AWS. When we come back to CI/CD, the pipeline will run those same three steps; nothing about them changes.
