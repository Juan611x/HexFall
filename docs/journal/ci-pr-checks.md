# Journal: Pull Request Checks in Practice

This entry looks at the pull request checks (`.github/workflows/ci.yml`) after using them for the first time on a real chain of pull requests: the eight PRs of the login release. [ci-cd.md](ci-cd.md) explains why the checks were designed this way. This entry records what we observed, two gaps we found, and the options for closing them. Nothing here is decided yet; the outcome will go to [DESIGN_DECISIONS.md](../DESIGN_DECISIONS.md) once it is.

## When the checks run

The workflow runs on `pull_request` events targeting `develop` or `main`: when a PR is opened and every time a commit is pushed to it. Merging does not start a new run. The green checks shown next to a merge are the ones that ran on the pull request. Deploying is separate and manual (`deploy-dev.yml` only has `workflow_dispatch`, see DD-013).

## How a pull request picks its checks

A first job, `changes`, uses `dorny/paths-filter` to see which folders the PR touches. Each check job depends on it and runs only when its group matched:

| Job | Runs when the PR touches | What it does |
|---|---|---|
| `terraform` | `infra/**` or `.github/workflows/**` | `fmt -check`, `init -backend=false` and `validate` per directory, `terraform test` with a mocked provider. No AWS credentials. |
| `python` | `tools/**` or `.github/workflows/**` | `unittest` discovery over `tools/test_*.py` (the deploy script). |
| `unity-tests` | `game/**` or `.github/workflows/**` | Unity EditMode tests with `game-ci/unity-test-runner`. |

## Two different kinds of "skipped"

While merging the login chain, some checks showed as skipped. There are two causes, and they mean very different things.

**Skipped by the path filter.** The job did not run because the PR does not touch its folder. A docs-only PR skips all three jobs, and the browser-extension fix (one file under `game/`) skipped `terraform` and `python`. This is the intended behavior: nothing that job checks has changed.

**Skipped for a missing Unity license.** `unity-tests` needs a Unity license stored as the repository secrets `UNITY_LICENSE`, `UNITY_EMAIL` and `UNITY_PASSWORD`. A job-level `if` cannot read secrets, so the job always starts, checks the secret inside its first step and, when it is missing, prints the warning `UNITY_LICENSE is not set; skipping the Unity EditMode tests` and skips the remaining steps. The job then reports **success**.

The second kind is the one to watch. The check is green, but no test ran. During the login work the EditMode suite (327 tests at the end) was run locally through the Unity Editor, not by CI. A green `unity-tests` check is only evidence when its log does not contain that warning.

## Gap 1: the Unity tests may never run in CI

The design accepted the license skip so that contributors without the secret, including forks, still get green checks for the rest. That trade-off was made before the repository had a real C# test suite. Now the client has hundreds of tests covering the login flow, and a silent skip hides them.

Options:

- **Add the license secrets.** A Unity Personal license works; GameCI documents the activation steps ([game.ci activation](https://game.ci/docs/github/activation)). This is the only option that actually runs the tests. It needs the account owner, because it involves the Unity account credentials.
- **Make the skip visible on our own PRs.** Keep skipping on forks, but fail (or post a notice on the PR) when the secret is missing on a PR from this repository. That turns "no tests ran" into something a reviewer sees.
- **Leave it as it is** and keep running the suite locally before merging. This works while one person develops the project, and stops working once others contribute.

Making the repository public (planned) raises the stakes: external pull requests come from forks, which never receive secrets, so their Unity tests will always be skipped no matter what we choose.

## Gap 2: a Terraform module that CI never checks

The `terraform` job lists its directories by hand:

- `init` + `validate`: `infra/modules/static-site`, `infra/bootstrap`, `infra/envs/dev`
- `terraform test`: `infra/modules/static-site`, `infra/bootstrap`

The login release added `infra/modules/user-pool`, with five `terraform test` runs, and did not update those lists. CI therefore never validates that module on its own and never runs its tests. They pass locally, and `infra/envs/dev` (which uses the module) is still validated, so a syntax error would be caught there. A broken test or a behavior change in the module would not be.

This is a maintenance trap rather than a one-off mistake: every new module has to be remembered in two places.

Options:

- **Add the directory** to both loops. The smallest fix, but the trap remains for the next module.
- **Discover the directories.** Loop over `infra/modules/*`, `infra/bootstrap` and `infra/envs/*` for `validate`, and over every directory that contains a `tests/` folder for `terraform test`. New modules are covered automatically.

Discovery looks like the better fit: the repository already follows a fixed layout (`modules/`, `envs/`, `bootstrap/`), and the cost is a few lines of shell.

## What is open

- Whether to add the Unity license secrets, and who does it.
- Whether a missing license on this repository's own PRs should fail or warn.
- Switching the Terraform job to directory discovery (and, until then, adding `user-pool` by hand).
