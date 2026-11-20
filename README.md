# HexFall

> Free-for-all multiplayer wizard arena. Low-poly 3D with a retro PSX look, playable in the browser, running on AWS.

**Status:** 🚧 Early development. The menu, player accounts (Amazon Cognito) and the hosting on AWS work; the arena gameplay is not built yet.

## The game

Wizards fight in an arcane arena. **Every player for themselves, 3-minute rounds, most eliminations wins.**

- **Spells are pickups:** every wizard starts with a basic bolt; powerful spells spawn around the map and must be fought over.
- **Twin-stick controls:** move with `WASD`, aim and cast with the mouse.
- **2–8 players** per match, server-authoritative.
- **Look:** low-poly 3D characters and arena, low-resolution rendering, point-filtered textures (retro PSX aesthetic).

## Architecture (planned)

```
Player browser (Unity WebGL)
  ├── HTTPS ─► CloudFront ─► S3                  static game client
  ├── HTTPS ─► API Gateway ─► Lambda ─► DynamoDB  profiles, leaderboard, matchmaking
  └── WSS   ─► ALB ─► ECS Fargate                authoritative game server
                         └─► DynamoDB             match results
Cognito (auth) · CloudWatch (logs, metrics, alarms) · Terraform (all infrastructure)
```

Built so far: the static client on CloudFront + S3, Cognito accounts with a Pre Sign-up trigger that keeps wizard names unique, and a public name-check Lambda for guests (the diagram of what runs today is [docs/diagrams/architecture.drawio](docs/diagrams/architecture.drawio)). The rationale for each choice is recorded in [docs/DESIGN_DECISIONS.md](docs/DESIGN_DECISIONS.md).

## Repository layout

| Path | Contents |
|---|---|
| `game/` | Unity project (the WebGL client; a headless server build is planned) |
| `art/` | Source art (`.blend` files, generator scripts); exports land in `game/Assets` |
| `server/` | Game server container (planned) |
| `services/` | Lambda functions: the Cognito Pre Sign-up trigger (`wizard_names`) and the guest name check (`name_check`) |
| `infra/bootstrap/` | One-time Terraform state backend |
| `infra/modules/` | Reusable Terraform modules |
| `infra/envs/dev/` | Development environment composition |
| `docs/` | [Design decisions](docs/DESIGN_DECISIONS.md), [AWS environment](docs/AWS_ENVIRONMENT.md), [decision journal](docs/journal/), [asset credits](docs/ASSET_CREDITS.md), [WebGL build](docs/BUILD.md), [deploy runbook](docs/DEPLOY.md), diagrams ([architecture](docs/diagrams/architecture.drawio), [client hosting](docs/diagrams/client-hosting.drawio)), runbooks |
| `.github/workflows/` | CI/CD pipelines |

## Branching

| Branch | Purpose |
|---|---|
| `main` | Stable, deployable. Changes arrive only through pull requests from `develop`. |
| `develop` | Integration branch. Feature branches (`feat/*`, `fix/*`, `infra/*`) merge here. |

Commits follow [Conventional Commits](https://www.conventionalcommits.org/).

## Getting started

There are two ways to use this repository. Pick the one you need.

### Run your own copy (no Unity needed)

The WebGL build does not depend on the environment: it reads `config.json` at startup, which the release script writes with the ids of your AWS resources. You can deploy a prebuilt build to your own account.

You need:

- An AWS account and the AWS CLI v2, signed in with a profile that can create S3, CloudFront, Cognito, DynamoDB, Lambda and IAM resources
- Terraform 1.10 or later
- Python 3
- The `HexFall-WebGL-<version>.zip` asset from this repository's [Releases](https://github.com/Juan611x/HexFall/releases), unzipped

Steps (details in [docs/DEPLOY.md](docs/DEPLOY.md)):

1. **Bootstrap** the Terraform state bucket once per account: `infra/bootstrap`.
2. **Create the environment**: copy `infra/envs/dev/backend.hcl.example` to `backend.hcl`, then `terraform init` and `apply` in `infra/envs/dev`. The region defaults to `eu-west-1` (`aws_region` variable).
3. **Publish** the unzipped build with `python tools/deploy_webgl.py --build-dir <folder> ...`, exporting the values from `terraform output` first (DEPLOY.md, step 4).

`infra/envs/dev` sets `permissions_boundary_name = "StudentBoundary"` because the lab account this project was built in requires it. In an account without that policy, remove the line; the modules then create the roles without a boundary.

### Modify and build the game

Everything above, plus:

- Unity Hub with **Unity 6000.5.3f1** and the **WebGL Build Support** module (a Unity Personal license is enough)
- Git LFS (`git lfs install`): textures, models and audio are stored with it
- Windows with PowerShell for `tools/build-webgl.ps1` (it launches Unity in batch mode; see [docs/BUILD.md](docs/BUILD.md))

```bash
git clone https://github.com/Juan611x/HexFall.git
cd HexFall
git lfs pull
```

Open `game/` with Unity Hub. The EditMode tests run from the Unity Test Runner; the Python and Terraform checks run as in `.github/workflows/ci.yml`.

**Third-person sandbox (work in progress):** the `PlayerWizard` prefab and the `Sandbox` scene use Unity's free **First Person + Third Person | Character Controllers** package (Starter Assets 2.0.2, by Unity Technologies, on the Asset Store). Its Asset Store licence is still under review, so it is not in this repository (`game/Assets/Starter Assets/` is ignored). Before opening `Sandbox`:

1. Add the package to your Unity account from the Asset Store.
2. In Unity, open **Window > Package Manager > My Assets**, and import it into `game/Assets/Starter Assets/` (the default location).

Without it, the player shows missing scripts and the scene has no camera rig. The main menu does not depend on it.

Git LFS files count against the repository owner's GitHub LFS bandwidth, so clones and forks that pull them use that quota.

## The lab account

HexFall is developed in a restricted AWS lab account: every regional resource lives in `eu-west-1`, IAM roles are named `student-*` and carry the `StudentBoundary` permissions boundary, and Amazon GameLift is not available (game servers will run on ECS Fargate). [docs/AWS_ENVIRONMENT.md](docs/AWS_ENVIRONMENT.md) records those rules. They are lab-specific; a normal AWS account only needs the steps above.

## License

The code and the self-made content are licensed under the [GNU General Public License v3.0](LICENSE), with an [additional permission under section 7](LICENSE-EXCEPTION.md) to link and convey the game with the Unity engine and its runtime libraries. Third-party assets keep their own licenses (CC0 1.0 and SIL OFL 1.1), listed in [docs/ASSET_CREDITS.md](docs/ASSET_CREDITS.md).
