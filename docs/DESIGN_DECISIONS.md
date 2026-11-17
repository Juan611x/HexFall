# Design Decisions

This log records the meaningful decisions behind HexFall: what was decided, why, and what was traded away. Entries are append-only; a decision that changes gets a new entry that supersedes the old one.

**Statuses:** `Accepted` · `Proposed` · `Superseded by DD-XXX`

| ID | Decision | Status |
|---|---|---|
| [DD-001](#dd-001-free-for-all-wizard-arena) | Free-for-all wizard arena | Accepted |
| [DD-002](#dd-002-spells-as-map-pickups) | Spells as map pickups | Accepted |
| [DD-003](#dd-003-low-poly-3d-with-a-retro-psx-look) | Low-poly 3D with a retro PSX look | Accepted |
| [DD-004](#dd-004-unity-webgl-client) | Unity WebGL client | Accepted |
| [DD-005](#dd-005-websockets-through-an-alb) | WebSockets through an ALB | Accepted |
| [DD-006](#dd-006-authoritative-server-on-ecs-fargate) | Authoritative server on ECS Fargate | Accepted |
| [DD-007](#dd-007-static-client-on-s3--cloudfront) | Static client on S3 + CloudFront | Accepted |
| [DD-008](#dd-008-monorepo-layout) | Monorepo layout | Accepted |
| [DD-009](#dd-009-infrastructure-as-code-with-terraform) | Infrastructure as code with Terraform | Accepted |

---

## DD-001: Free-for-all wizard arena

- **Status:** Accepted
- **Context:** The project must be achievable as a course final project yet strong enough for a portfolio.
- **Decision:** Free-for-all deathmatch between wizards, 2–8 players, 3-minute rounds, most eliminations wins. Top-down twin-stick controls.
- **Rationale:** One mode, one map, and a short round keep the scope small. Twin-stick movement happens on a single plane, so network state is just position (`x, z`) and facing angle.
- **Trade-offs:** No teams or progression in the first version.

## DD-002: Spells as map pickups

- **Status:** Accepted
- **Context:** Players need a reason to move around the map instead of camping.
- **Decision:** Every wizard has a basic bolt. Stronger spells spawn at fixed points on the map and are collected as pickups (limited charges or duration).
- **Rationale:** Pickups create contested zones and map control, the core loop of arena shooters. Spell spawning is server-owned, which reinforces the authoritative model.
- **Trade-offs:** Spawn timing and placement need balancing.

## DD-003: Low-poly 3D with a retro PSX look

- **Status:** Accepted
- **Context:** The game should look striking without a large art budget.
- **Decision:** Low-poly 3D characters and arena, rendered at low internal resolution (around 320×180, upscaled with point filtering), small point-filtered textures without mipmaps, optional vertex snapping, fog, and dithering.
- **Rationale:** The retro aesthetic hides low asset fidelity and is cheap to produce. Spell particles and colored dynamic lights stand out strongly against it.
- **Trade-offs:** 3D characters need rigging and animations (more work than billboard sprites); mitigated with free CC0 asset packs.

## DD-004: Unity WebGL client

- **Status:** Accepted
- **Context:** Players and recruiters should be able to try the game without installing anything.
- **Decision:** Ship the client as a Unity WebGL build.
- **Rationale:** A link is the lowest-friction way to play.
- **Trade-offs:** Browsers cannot open UDP sockets, so the transport must be WebSockets (see DD-005). WebGL has tighter performance and memory limits than native builds.

## DD-005: WebSockets through an ALB

- **Status:** Accepted
- **Context:** The WebGL client can only use WebSockets for real-time traffic.
- **Decision:** Real-time traffic uses WSS through an Application Load Balancer, which supports the WebSocket upgrade natively.
- **Rationale:** The ALB terminates TLS and routes to game server tasks; no extra proxy needed.
- **Trade-offs:** TCP head-of-line blocking adds latency under packet loss (acceptable for a casual top-down game). Requires sticky routing per match and heartbeats, because the ALB idle timeout defaults to 60 s.

## DD-006: Authoritative server on ECS Fargate

- **Status:** Accepted
- **Context:** Amazon GameLift is denied by an organization SCP in the target account.
- **Decision:** Run the game server as a container on ECS Fargate. The server owns all game state: movement validation, hit detection, spell spawns, scoring.
- **Rationale:** Fargate removes instance management, and an authoritative server prevents client-side cheating.
- **Trade-offs:** Matchmaking and server lifecycle must be built by hand instead of using GameLift.

## DD-007: Static client on S3 + CloudFront

- **Status:** Accepted
- **Context:** A WebGL build is only static files (`.html`, `.js`, `.wasm`, `.data`).
- **Decision:** Host the client in S3 and serve it through CloudFront.
- **Rationale:** No servers to patch, pay-per-use, automatic scaling, and edge caching. Compute is reserved for what actually needs it (the game server).
- **Trade-offs:** Objects need correct `Content-Encoding`/`Content-Type` metadata for Brotli/gzip and `.wasm`. A custom domain needs an ACM certificate in `us-east-1`, which the account SCP may block; the default `*.cloudfront.net` domain works with HTTPS.

## DD-008: Monorepo layout

- **Status:** Accepted
- **Context:** Game, server, services, infrastructure, and docs evolve together.
- **Decision:** One repository with top-level folders `game/`, `server/`, `services/`, `infra/`, `docs/`. The Unity project lives in `game/`, never at the root.
- **Rationale:** Unity imports everything under `Assets/` and generates heavy folders at its root, so keeping it isolated avoids polluting it. Path-scoped CI triggers become simple.
- **Trade-offs:** CI workflows need path filters to avoid running unrelated jobs.

## DD-009: Infrastructure as code with Terraform

- **Status:** Accepted
- **Context:** Anyone should be able to recreate the environment automatically.
- **Decision:** All AWS resources are defined in Terraform, with remote state in S3 using native lock files (`use_lockfile`, Terraform ≥ 1.10).
- **Rationale:** Reproducible, reviewable infrastructure changes; S3 native locking avoids a separate lock table.
- **Trade-offs:** Account constraints must be encoded in code: region `eu-west-1`, IAM roles named `student-*` with the `StudentBoundary` permissions boundary.
