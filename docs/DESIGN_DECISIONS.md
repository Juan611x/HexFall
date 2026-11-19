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
| [DD-010](#dd-010-private-client-bucket-behind-cloudfront-origin-access-control) | Private client bucket behind CloudFront Origin Access Control | Accepted |
| [DD-011](#dd-011-terraform-provisions-a-script-publishes-builds) | Terraform provisions, a script publishes builds | Accepted |
| [DD-012](#dd-012-reusable-static-site-module-and-thin-environments) | Reusable static-site module and thin environments | Accepted |
| [DD-013](#dd-013-manual-deploys-until-ci-can-reach-aws) | Manual deploys until CI can reach AWS | Accepted |
| [DD-014](#dd-014-cognito-user-pool-called-directly-by-the-client) | Cognito user pool called directly by the client | Accepted |
| [DD-015](#dd-015-auth-port-with-a-cognito-adapter-and-an-in-memory-fake) | Auth port with a Cognito adapter and an in-memory fake | Accepted |
| [DD-016](#dd-016-unverified-sign-in-resumes-verification) | Unverified sign-in resumes verification | Accepted |
| [DD-017](#dd-017-ignore-browser-extension-errors-before-the-unity-loader) | Ignore browser-extension errors before the Unity loader | Accepted |
| [DD-018](#dd-018-unique-wizard-names-reserved-by-a-pre-sign-up-trigger) | Unique wizard names reserved by a Pre Sign-up trigger | Accepted |
| [DD-019](#dd-019-persistent-session-with-a-stored-refresh-token) | Persistent session with a stored refresh token | Accepted |
| [DD-020](#dd-020-password-reset-through-cognitos-forgot-password-flow) | Password reset through Cognito's forgot-password flow | Accepted |

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

## DD-010: Private client bucket behind CloudFront Origin Access Control

- **Status:** Accepted
- **Context:** The WebGL client (DD-004, DD-007) is a folder of static, Brotli-compressed files that must be served over HTTPS from the lab account.
- **Decision:** The files live in a private S3 bucket (`<project>-<env>-client-<account-id>`) with all Block Public Access settings on, ACLs disabled (`BucketOwnerEnforced`) and SSE-S3 encryption. A CloudFront distribution serves them through Origin Access Control (SigV4) on the default `*.cloudfront.net` certificate, redirecting HTTP to HTTPS, with `index.html` as root object, the managed `CachingOptimized` policy and compression off. The bucket policy allows `s3:GetObject` only to CloudFront and only for this distribution (`AWS:SourceArn`). No VPC is involved.
- **Rationale:** One public entry point and no way to expose the bucket by mistake; the `SourceArn` condition stops any other distribution from reading it. The default certificate gives HTTPS without ACM in `us-east-1`, which the region lock blocks. Compression is off because Unity already ships Brotli files.
- **Trade-offs:** Missing files answer 403 instead of 404 (no `s3:ListBucket`). No custom domain. The default certificate fixes the minimum TLS version at TLSv1 (browsers still negotiate TLS 1.2+).

## DD-011: Terraform provisions, a script publishes builds

- **Status:** Accepted
- **Context:** The infrastructure changes rarely; the game build changes on every release.
- **Decision:** Terraform owns the bucket, policy, OAC and distribution. `tools/deploy_webgl.py` publishes a build: it uploads every file with its real `Content-Type` and `Content-Encoding: br` for Brotli files (the same table as the local server), uploads `config.json` and `index.html` last with `no-cache`, deletes files no longer in the build, skips Unity `_DoNotShip` debug folders, and invalidates `/*`. Terraform outputs (bucket name, distribution id, URL) are the only contract between them. The client's runtime settings go in a generated `config.json`, not in the build.
- **Rationale:** Releases stay one quick command and build artifacts stay out of Terraform state. Upload order keeps a new `index.html` from pointing at missing files. `config.json` lets the same build serve any environment and receive backend URLs later.
- **Trade-offs:** Two tools to keep aligned. Unity file names are not content-hashed, so every release invalidates the whole distribution.

## DD-012: Reusable static-site module and thin environments

- **Status:** Accepted
- **Context:** The same hosting must be recreatable for other environments or accounts without editing code.
- **Decision:** `infra/modules/static-site` holds the hosting; `infra/envs/dev` is a thin composition with dev values (`force_destroy` on, versioning off). `infra/bootstrap` creates the state bucket (`<project>-tfstate-<account-id>`, versioned, private, protected from destroy) with local state. Environments use a partial S3 backend through a git-ignored `backend.hcl`, native locking and provider `default_tags`. Names derive from the project, environment and caller account id; the region is a variable defaulting to `eu-west-1`. The module and bootstrap are covered by `terraform test` with a mocked provider.
- **Rationale:** Nothing is tied to one account, and a new environment is a new thin folder. The state bucket has to exist before any configuration can store state in it, so it is created separately.
- **Trade-offs:** Agnostic to account, environment and region, not to cloud provider. The bootstrap state lives on one machine and must be kept safe.

## DD-013: Manual deploys until CI can reach AWS

- **Status:** Accepted
- **Context:** The lab account denies creating the GitHub OIDC provider and IAM users, and has no provider to reuse, so GitHub Actions cannot authenticate to AWS.
- **Decision:** Deploys run by hand from a developer machine with the lab SSO profile, following the runbook in `docs/DEPLOY.md`. CI access in the bootstrap is behind `enable_ci_access` (off). The deploy workflow only runs on manual trigger; pull request checks, which need no AWS access, stay on.
- **Rationale:** A working public URL now, using the same three steps a pipeline would run, without long-lived credentials.
- **Trade-offs:** Releases depend on a person with lab access. Automation needs either the GitHub OIDC provider created by the lab admins or a CodeBuild-based deploy with a `student-*` role.

## DD-014: Cognito user pool called directly by the client

- **Status:** Accepted
- **Context:** Players need accounts (email, password, wizard name) before there is a game backend. The lab account allows Cognito but restricts IAM: roles must be named `student-*` and carry the `StudentBoundary` boundary ([AWS_ENVIRONMENT.md](AWS_ENVIRONMENT.md)). Three ways to provide accounts were compared; the story is in the [authentication journal](journal/authentication.md).
- **Decision:** An Amazon Cognito user pool (`infra/modules/user-pool`) with email as the sign-in name, auto-verified by a code, and `preferred_username` as the wizard name. A public app client with no secret allows only `USER_PASSWORD_AUTH` and refresh tokens; the Unity client calls the Cognito API directly. Emails use the default Cognito sender. The pool and client ids reach the client through the generated `config.json` (DD-011, DD-015).
- **Rationale:** The options, judged on development and integration effort:

  | Option | What we would have to build and run | Verdict |
  |---|---|---|
  | **(a) Custom auth service on ECS Fargate** | We already plan Fargate for the game server (DD-006), but auth there means writing and securing password hashing, verification emails, token signing and rotation, plus an always-on task, ALB routing and IAM task roles under the `student-*` rules. | Most code, most to secure, paid around the clock. |
  | **(b) Lambda + API Gateway** | Serverless and cheaper when idle, but still our own code for hashing, emails (SES), tokens, a user table (DynamoDB), CORS, and a `student-*` role for every function. | Less infrastructure than (a), same security work. |
  | **(c) Amazon Cognito** | Managed: it stores users, verifies emails, issues tokens and throttles abuse. No roles, no servers; the client calls it directly. Integration is two ids in `config.json`. | Chosen. |

  Cognito wins on development and integration simplicity for this stage: the whole account system is one Terraform module plus a client adapter, and the lab's IAM restrictions never come into play. The game server (DD-006) can later verify the Cognito JWTs instead of running any login code. Hiding user-existence errors (`prevent_user_existence_errors`) avoids leaking which emails are registered. A public client is the only option for WebGL, which cannot keep a secret.
  - **Why public ids are fine:** the pool id and client id are public identifiers by design; the browser sends them on every Cognito call. The real secret would be a client secret, and this public client has none. What remains are abuse risks, not leaks: junk sign-ups exhausting the email quota, password guessing (Cognito throttles and locks out) and sign-up revealing that an email exists. Later mitigations: AWS WAF on the pool, Cognito threat protection (Plus tier), a CAPTCHA through a pre-sign-up trigger, and SES.
  - **Other alternatives within Cognito:** SRP (stronger, but needs a big-number crypto implementation in the client) and the Hosted UI with OAuth (needs a Cognito or custom domain, redirects and a browser flow around WebGL).
- **Trade-offs:** The password travels to Cognito over TLS instead of being proven with SRP. The default email sender is limited to about 50 emails a day, so heavy sign-up testing needs SES. We depend on a managed service and its limits, and moving away later means migrating users. Wizard names are not unique in Cognito itself; DD-018 enforces that with a trigger Lambda and a table (with a `student-*` role). Sessions across reloads are handled in DD-019 and password recovery in DD-020.

## DD-015: Auth port with a Cognito adapter and an in-memory fake

- **Status:** Accepted
- **Context:** The UI must work in the Unity Editor and in tests without AWS, and the same WebGL build should serve any environment (DD-011).
- **Decision:** The UI depends on the `IAuthService` port. `CognitoAuthService` implements it by calling the Cognito Identity Provider JSON API with `UnityWebRequest` through an `ICognitoTransport` (no AWS SDK); `FakeAuthService` keeps accounts in memory. At startup `MenuBootstrap` reads `config.json` (WebGL only) and uses Cognito when `region`, `cognito.userPoolId` and `cognito.clientId` are all set; otherwise, and always in the Editor, it uses the fake. Errors are translated by `CognitoErrorMapper`. Token storage is covered by DD-019 (ID and access tokens stay in memory; only the refresh token is stored).
- **Rationale:** Presenters never know which implementation they use, the fake keeps tests and Editor play fast and offline, and the environment is chosen by data rather than by a rebuild.
- **Trade-offs:** A missing or empty config silently means fake accounts, so deploys must check `config.json` (see [DEPLOY.md](DEPLOY.md)). Two implementations must keep the same behavior.

## DD-016: Unverified sign-in resumes verification

- **Status:** Accepted
- **Context:** A player who closed the tab before entering the verification code was stuck: logging in said "verify your email", and signing up again said the email already exists.
- **Decision:** Logging in to an unconfirmed account returns an "unverified" result instead of an error. `LoginPresenter` requests a new code and opens the Verify screen. After sign-up the password is kept in memory only, so confirming the code signs the player in without retyping it.
- **Rationale:** The player always has a way forward from either screen, with no manual cleanup of half-created accounts.
- **Trade-offs:** The password stays in memory between sign-up and verification (not stored, and gone on reload). Logging in to an unconfirmed account sends a verification email as a side effect.

## DD-017: Ignore browser-extension errors before the Unity loader

- **Status:** Accepted
- **Context:** A wallet extension (MetaMask) throws page errors from its own scripts. The Unity 6 loader shows an alert on any page error, and during startup it routes errors to its internal `startupErrorHandler`, bypassing `config.errorHandler`, so setting `errorHandler` was not enough.
- **Decision:** `game/Assets/WebGLTemplates/HexFall/index.html` registers `error` and `unhandledrejection` listeners before the Unity loader and stops errors whose origin is a browser extension (`chrome-extension://`, `moz-extension://`, `safari-web-extension://`, `ms-browser-extension://`).
- **Rationale:** Extension errors are not ours and the player cannot act on them; the listeners run first, so the loader never sees them.
- **Trade-offs:** Errors from extensions are hidden, which could mask an extension that really breaks the page. Errors from our own files still reach the loader.

## DD-018: Unique wizard names reserved by a Pre Sign-up trigger

- **Status:** Accepted
- **Context:** Two players could register with the same wizard name, which Cognito does not prevent (`preferred_username` is not unique). Names must be unique, case-insensitive, and two simultaneous sign-ups for one name must not both succeed.
- **Decision:** A Cognito Pre Sign-up trigger (Lambda, Python 3.12, `services/wizard_names/`) validates the name (3-16 of `[A-Za-z0-9_]`) and reserves it with a conditional DynamoDB write keyed by the lowercase name (`attribute_not_exists(name_key) OR owner_email = :email`). A taken name raises `WizardNameTaken` and a bad format `InvalidWizardName`; the client maps both to player-facing copy. A name whose owner no longer exists, or is UNCONFIRMED for more than 24 hours, is reclaimed: the stale user is deleted and the name is handed over with a conditional write on the old owner. The same email signing up again with its name is allowed. The pool id comes from the trigger event, not an environment variable. Guests are not reserved.
- **Rationale:** The conditional write is atomic, so concurrent sign-ups cannot both win. Reserving at sign-up gives the error on the register screen. Deleting the stale unconfirmed user stops its owner from confirming later with a duplicate name.
- **Trade-offs:** A name is held for up to 24 hours by an account that never verifies. The reservation lives outside Cognito, so deleting a confirmed user by hand leaves its name taken until it is cleaned from the table (a missing owner is reclaimed on the next attempt, so it self-heals). Adds a Lambda, a table and a `student-*` role. Renaming is out of scope. Alternatives rejected: a `ListUsers` check before sign-up (not atomic, and it needs a pre-flight call from the client) and reserving at verification (the player learns the name is taken only after typing the code).

## DD-019: Persistent session with a stored refresh token

- **Status:** Accepted
- **Context:** Tokens lived in memory only (DD-015), so every reload of the page signed the player out and sent them back to the title screen.
- **Decision:** Only the Cognito refresh token and the email are persisted, behind the `ISessionStore` port (`PlayerPrefsSessionStore` in the game; in WebGL `PlayerPrefs` is backed by the browser's IndexedDB; an in-memory store in tests). ID and access tokens stay in memory. At startup, after `config.json` selects the adapter and before the first screen shows, `CognitoAuthService.Restore` calls `InitiateAuth` with `REFRESH_TOKEN_AUTH`: success opens the Lobby, otherwise the Title screen. The refresh response carries no new refresh token, so the stored one is kept. A network failure during restore keeps the stored token for the next visit; any other failure clears it. Log out clears the local state first and navigates immediately, then sends `RevokeToken` without waiting for the result (the app client has token revocation enabled). Guests are never persisted, and the fake auth never restores. Refresh tokens last 30 days (`infra/modules/user-pool/main.tf`).
- **Rationale:** Alternatives considered:
  - **Tokens in memory only:** the previous behavior and the problem that was hit; the session is lost on every reload.
  - **Also storing ID and access tokens:** they are shorter-lived but more exposed, and unnecessary because the refresh token recreates them on every start.
  - **Cookies set by a backend:** `HttpOnly` cookies would keep the token away from page scripts, but no backend exists yet (DD-014).

  The refresh token is the smallest credential that restores the session. Clearing it on a definitive failure avoids retrying a token that Cognito rejected; keeping it on a network failure avoids logging a player out because they were offline.
- **Trade-offs:** A refresh token in browser storage can be read by any script running on the page. The page only loads the game and the Unity loader (no third-party scripts), and revoking the token on log out limits the window. Log out is fire-and-forget, so if the revoke call fails the token stays valid on the server until it expires. Revisit when a backend can set `HttpOnly` cookies.

## DD-020: Password reset through Cognito's forgot-password flow

- **Status:** Accepted
- **Context:** A player who forgot the password had no way back into the account. The pool already supports recovery through the verified email.
- **Decision:** "Forgot password?" on the Login screen opens a two-step screen (`ForgotPasswordPresenter`) with the typed email prefilled. Step 1 sends the email to `ForgotPassword`, which emails a 6-digit code. Step 2 takes the code, a new password and its confirmation, with the same live rules as sign-up, and calls `ConfirmForgotPassword`. The game then signs in with the new password and opens the Lobby; if that sign-in fails it returns to Login with a one-shot notice (`Password updated. Log in with your new password.`). Resend is available after a 60 s countdown, and "Use a different email" goes back to step 1. An unconfirmed account cannot reset: the message is `Sign in first to finish verifying your email.`, which leads into the flow from DD-016. The calls go through `IAuthService` (`RequestPasswordReset`, `ConfirmPasswordReset`) and errors are translated by `CognitoErrorMapper.MapForgotPassword`.
- **Rationale:** Cognito already owns codes, expiry and throttling, so the feature is two API calls plus a screen, with no backend. Signing in right after the reset spares the player retyping the new password. With `prevent_user_existence_errors` enabled, Cognito does not reveal whether an email has an account when the reset is requested; the screen moves to the code step on any successful response.
- **Trade-offs:** The reset email uses the default Cognito sender, with the same daily limit as sign-up (DD-014). An unconfirmed account must first finish verification. The two-step flow lives in one presenter, which has to keep the fake and the Cognito adapters consistent (DD-015).
