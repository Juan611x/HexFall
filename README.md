# HexFall

> Free-for-all multiplayer wizard arena. Low-poly 3D with a retro PSX look, playable in the browser, running on AWS.

**Status:** 🚧 Early development: repository scaffolding.

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

The rationale for each choice is recorded in [docs/DESIGN_DECISIONS.md](docs/DESIGN_DECISIONS.md).

## Repository layout

| Path | Contents |
|---|---|
| `game/` | Unity project (client and headless server build) |
| `server/` | Game server container (Dockerfile, runtime config) |
| `services/` | Lambda functions for the REST API |
| `infra/bootstrap/` | One-time Terraform state backend |
| `infra/modules/` | Reusable Terraform modules |
| `infra/envs/dev/` | Development environment composition |
| `docs/` | Design decisions, diagrams, runbooks |
| `.github/workflows/` | CI/CD pipelines |

## Branching

| Branch | Purpose |
|---|---|
| `main` | Stable, deployable. Changes arrive only through pull requests from `develop`. |
| `develop` | Integration branch. Feature branches (`feat/*`, `fix/*`, `infra/*`) merge here. |

Commits follow [Conventional Commits](https://www.conventionalcommits.org/).

## Getting started

Prerequisites:

- Unity 6 LTS with the **WebGL Build Support** module
- Git LFS (`git lfs install`)
- Terraform ≥ 1.10
- AWS CLI v2

```bash
git clone https://github.com/Juan611x/HexFall.git
cd HexFall
git lfs pull
```

Deployment instructions will be added together with the infrastructure.

## AWS environment constraints

The project targets a restricted lab account. These constraints shape the infrastructure code:

- All regional resources live in **`eu-west-1`**.
- IAM roles must be named `student-*` and carry the `StudentBoundary` permissions boundary.
- Amazon GameLift is not available, so game servers run on ECS Fargate.
