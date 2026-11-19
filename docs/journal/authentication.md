# Journal: Authentication (Amazon Cognito)

This journal entry tells how HexFall got player accounts: what we needed, the options we weighed, why we chose Amazon Cognito, and what testing on dev taught us. Unlike the [client hosting](client-hosting.md) entry, this one describes something already running on the dev environment.

The picture lives in [architecture.drawio](../diagrams/architecture.drawio) (the whole system, including Cognito) with the hosting detail in [client-hosting.drawio](../diagrams/client-hosting.drawio). Open them in [diagrams.net](https://app.diagrams.net) or with the VS Code Draw.io extension. The decisions are recorded in [DD-014](../DESIGN_DECISIONS.md#dd-014-cognito-user-pool-called-directly-by-the-client) to [DD-017](../DESIGN_DECISIONS.md#dd-017-ignore-browser-extension-errors-before-the-unity-loader); the operational steps are in [DEPLOY.md](../DEPLOY.md#accounts-cognito).

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
- The wizard name is stored in the `preferred_username` attribute. After sign-up the password is kept in memory only so that the Verify screen can sign the player in; tokens also live in memory only.

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

## What is still open

- The session is not persisted: reloading the page signs the player out.
- There is no forgot-password screen, although the pool supports recovery by verified email.
- Wizard names cannot be changed after sign-up (renaming is out of scope for DD-018).
- The default email sender caps testing at about 50 emails a day; SES would lift it.
- The game server (ALB + Fargate, DD-005, DD-006) is not built. When it is, it can verify the Cognito JWTs instead of running login code.
