# Journal: Authentication (Amazon Cognito)

This journal entry tells how HexFall got player accounts: what we needed, the options we weighed, why we chose Amazon Cognito, and what testing on dev taught us. Unlike the [client hosting](client-hosting.md) entry, this one describes something already running on the dev environment.

The picture lives in [architecture.drawio](../diagrams/architecture.drawio) (the whole system, including Cognito) with the hosting detail in [client-hosting.drawio](../diagrams/client-hosting.drawio). Open them in [diagrams.net](https://app.diagrams.net) or with the VS Code Draw.io extension. The decisions are recorded in [DD-014](../DESIGN_DECISIONS.md#dd-014-cognito-user-pool-called-directly-by-the-client) to [DD-020](../DESIGN_DECISIONS.md#dd-020-password-reset-through-cognitos-forgot-password-flow); the operational steps are in [DEPLOY.md](../DEPLOY.md#accounts-cognito).

## What we needed

Players need an account: an email, a password and a wizard name. Email has to be verified so that accounts belong to real people. The game itself (a server, matches) does not exist yet, so whatever we pick must work without one. And the lab account limits IAM: roles must be named `student-*` and carry the `StudentBoundary` boundary ([AWS_ENVIRONMENT.md](../AWS_ENVIRONMENT.md)), so every role we create is extra friction.

## Three ways to get accounts

### A service on ECS Fargate

We already plan Fargate for the authoritative game server (DD-006), so it was the first idea: add an auth service next to it. We ruled it out. Auth there means writing and securing password hashing, the verification emails, token signing and rotation, and then running an always-on task behind the ALB with IAM task roles under the `student-*` rules. It is the most code and the most security surface of the three, for something that is not the point of the game.

### Lambda + API Gateway

This is cheaper when idle and needs no servers to patch. But it is still our code: password hashing, email sending (SES), token handling, a user table in DynamoDB, CORS, and a `student-*` role for every function. It trims the infrastructure of the first option, not the security work.

### Amazon Cognito

Cognito is managed: it stores users and passwords, sends the verification code, issues tokens and throttles abuse. It needs no roles and no servers because the browser calls it directly. Integration with the client is two ids in a config file. For this stage it wins on development and integration simplicity, which is what decided it (DD-014). The cost is dependence on a managed service and its limits, and a later migration if we leave it.

## Choices inside Cognito

| Question | Chosen | Why |
|---|---|---|
| SRP or `USER_PASSWORD_AUTH` | `USER_PASSWORD_AUTH` | SRP is stronger, but needs a big-number crypto implementation in a WebGL client. The password travels over TLS instead. |
| Hosted UI or our own screens | Our own UI | The Hosted UI needs a Cognito or custom domain, OAuth redirects and a browser flow around a WebGL page. |
| AWS SDK or direct API | Direct JSON API with `UnityWebRequest` | No SDK to import into a WebGL build; the few calls we need (SignUp, ConfirmSignUp, InitiateAuth, resend code) are simple. |

The app client is public with no secret, because a browser cannot keep one. It also hides user-existence errors, so a wrong email and a wrong password look the same at login.

## Are the ids secret?

The pool id and client id end up in `config.json`, served to anyone. That is by design: the browser sends them on every Cognito call, and the client has no secret to leak (a secret would belong to a confidential client, which a browser cannot be). What remains are abuse risks: junk sign-ups using up the roughly 50 emails a day of the default sender, password guessing (Cognito throttles and locks out) and sign-up revealing that an email already exists. Later mitigations are AWS WAF on the pool, Cognito threat protection (Plus tier), a CAPTCHA through a pre-sign-up trigger and SES.

## How the pieces connect

- **Terraform** creates the pool and client in `infra/modules/user-pool`, used as `module "auth"` in `infra/envs/dev`, and outputs `cognito_user_pool_id`, `cognito_client_id` and `aws_region`.
- **The deploy script** reads them from environment variables and writes `config.json` next to the build (DD-011).
- **The client** fetches `config.json` at startup. `MenuBootstrap` picks the Cognito adapter when region, pool id and client id are all set, and the in-memory fake otherwise, and always in the Editor (DD-015). Presenters only know the `IAuthService` port.
- The wizard name is stored in the `preferred_username` attribute. After sign-up the password is kept in memory only so that the Verify screen can sign the player in; ID and access tokens live in memory only; the refresh token is the one thing persisted (see [Session and password reset](#session-and-password-reset)).

## What testing on dev found

**Unverified accounts dead-ended.** Closing the tab before entering the code left an unconfirmed account. Logging in said to verify, but there was no way back to the code screen, and signing up again said the email already existed. Login now returns an "unverified" result, `LoginPresenter` sends a fresh code and opens the Verify screen, and the cached password signs the player in once the code is accepted (DD-016).

**A wallet extension broke loading.** With MetaMask installed the page raised an alert on startup. The Unity 6 loader alerts on any page error, and during startup it sends errors to its own `startupErrorHandler`, so the `errorHandler` option alone did not help. The HTML template now registers `error` and `unhandledrejection` listeners before the loader and ignores errors that come from browser-extension URLs (DD-017).

**Empty config is silent.** If a deploy runs without the Cognito variables, the page still publishes and the game quietly uses fake accounts. DEPLOY.md now has a check command before publishing and asks to verify `config.json` afterwards.

## Unique wizard names

**Decision.** Wizard names are unique, case-insensitive, and reserved when the player signs up (DD-018). Cognito's `preferred_username` is not unique, so the pool alone cannot enforce it.

**Why a Pre Sign-up trigger plus a DynamoDB conditional write.** Checking with `ListUsers` before sign-up is not atomic: two players can both see the name free and both succeed. A conditional `PutItem` (`attribute_not_exists(name_key) OR owner_email = :email`) is atomic, so only one concurrent sign-up wins. Reserving at verification time instead would let the player type the whole code before learning the name is taken. The trigger runs inside the sign-up call, so the rejection comes back on the register screen (`UserLambdaValidationException` carrying `WizardNameTaken`), and the client maps it.

**Why the pool id comes from the event.** The trigger needs the pool id for `AdminGetUser` and `AdminDeleteUser`. The pool references the Lambda (`lambda_config`) and the role policy references the pool, so passing the id as an environment variable would make the Lambda depend on the pool that depends on the Lambda: a Terraform cycle. The event already carries `userPoolId`, and the policy can use the pool ARN because it is a separate resource.

**The reclaim rule.** A reservation is taken over when its owner no longer exists, or is UNCONFIRMED for more than 24 hours. In the second case the stale Cognito user is deleted first, so its owner cannot confirm later with a name now held by someone else. The takeover is a conditional write on the old owner, so if two new players race for the same stale name, one loses. The same email signing up again with the same name is not a clash: it is a retry.

**The StudentBoundary risk.** The trigger role needs DynamoDB and Cognito admin permissions. The lab boundary (`StudentBoundary`, see AWS_ENVIRONMENT.md) may not allow them, and only the first `apply` will tell. If it denies them, the fallback is to reserve at verification time or to drop the reclaim (and with it the Cognito admin permissions).

## Session and password reset

**Staying signed in (DD-019).** Reloading the page used to sign the player out, because tokens lived only in memory. Now the Cognito refresh token and the email are stored through the `ISessionStore` port (`PlayerPrefs`, which is IndexedDB in WebGL). ID and access tokens are not stored: the refresh token recreates them, so storing them would only add exposure. At startup the client trades the refresh token for new tokens before the first screen shows: success opens the Lobby, anything else the Title screen. Keeping tokens in a backend-set cookie was not an option because there is no backend yet.

**Two rules decide what happens to the stored token.** A network failure during restore keeps it, so an offline player is not logged out. Any other failure clears it, because Cognito rejected it. On log out the client clears its local state and navigates first, then asks Cognito to revoke the token without waiting. The accepted risk is that a script on the page could read the stored token; the page loads only the game and the Unity loader, and revocation on log out limits the window.

**Resetting the password (DD-020).** "Forgot password?" on Login opens a two-step screen with the typed email prefilled. Step 1 sends a 6-digit code by email; step 2 takes the code and a new password with the same live rules as sign-up. After a successful reset the game signs in with the new password and opens the Lobby; if that sign-in fails it returns to Login with a notice to log in with the new password. An unconfirmed account cannot reset, so the screen says to sign in first to finish verifying the email, which leads into the flow from DD-016.

The screens are drawn in the [HexFall Menu Mockups canvas](https://claude.ai/artifact/9UT9f4Fi5BkJLC92hEQiLn), boards "7 · Forgot password: request a code" and "8 · Forgot password: code + new password".

## Guest names

**How we found it.** Unique names (DD-018) went live on dev and worked for sign-up. Reviewing the rest of the menu after that release, the user pointed out that guests skipped the rule: a guest could enter with the name of a registered account, and the random-name button could produce one too. Two players could then show up with the same name, which is exactly what DD-018 set out to prevent.

**Why the game could not check by itself.** The guest flow never talks to a backend: `GuestPresenter` validated only the name format, locally. The list of taken names exists in two places, and the browser can reach neither. The DynamoDB table `hexfall-<env>-wizard-names` is private and needs AWS credentials. Cognito can list users by `preferred_username`, but `ListUsers` is an IAM-signed admin call that needs the same kind of credentials, and the pre sign-up trigger only runs when someone creates an account. Before this change, nothing checked guests at all; for real accounts the check has always been the pre sign-up trigger of DD-018, because Cognito by itself does not keep `preferred_username` unique. So the gap was not a bug in the trigger: there was simply no public way to ask "is this name taken?"

**What was built (DD-021).** A small read-only Lambda answers `GET /names/<name>` with whether the name is free, behind a public Lambda Function URL. The guest screen calls it before entering, and the random-name button asks about each candidate and tries up to 5 before giving up. The call sits behind an `INameAvailability` port, so the Editor uses a fake that treats the in-memory accounts as taken and an old `config.json` without `apiBaseUrl` simply skips the check.

**Choices.** Check only, no reservation: two guests may share a name, which the user accepted to avoid expiry jobs. Any reservation counts as taken, even one that sign-up could reclaim, because the conservative answer is cheaper than a second reclaim rule. A Function URL beat API Gateway because it needs no extra service for one read-only call; DD-021 lists what API Gateway would add and when to switch.

**Who deploys it.** Terraform, like the pre sign-up trigger: `terraform apply` zips `services/name_check/` with `archive_file`, creates the Lambda, its role, log group and Function URL, and prints `name_check_url`. The release script `tools/deploy_webgl.py` only uploads the game to S3; it learns the URL through `HEXFALL_API_BASE_URL` and writes it into `config.json` as `apiBaseUrl`.

**The risk.** The endpoint is public and unauthenticated, and the lab may deny public Function URLs or their permissions; only the first apply tells. The fallback is an API Gateway HTTP API over the same Lambda. AWS now wants two resource-based permissions for a public URL (`lambda:InvokeFunctionUrl` and `lambda:InvokeFunction` through the URL), so the module creates both.

## What is still open

- The refresh token sits in browser storage (DD-019). A backend that sets `HttpOnly` cookies would remove that exposure once one exists.
- Wizard names cannot be changed after sign-up (renaming is out of scope for DD-018).
- The guest name check is public and has no rate limiting beyond Lambda concurrency (DD-021).
- The default email sender caps testing at about 50 emails a day; SES would lift it.
- The game server (ALB + Fargate, DD-005, DD-006) is not built. When it is, it can verify the Cognito JWTs instead of running login code.
