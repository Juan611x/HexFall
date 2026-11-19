# user-pool

Amazon Cognito user pool and public app client for the Unity WebGL client. The client calls the Cognito API directly with `USER_PASSWORD_AUTH`; there is no Hosted UI, OAuth, Lambda or IAM role. The reasoning is in [DD-014](../../../docs/DESIGN_DECISIONS.md).

Players sign in with their email. The email is auto-verified by a code, and `preferred_username` carries the wizard name. The password policy mirrors the client's `PasswordPolicy`. Emails go through the default Cognito sender (about 50 per day).

## Inputs

| Name | Type | Default | Description |
|---|---|---|---|
| `project` | string | `hexfall` | Feeds the pool and client names. |
| `environment` | string | required | Feeds the names (`<project>-<environment>-players` and `-game`). |
| `deletion_protection` | string | `ACTIVE` | `ACTIVE` or `INACTIVE`. Dev sets `INACTIVE` so it can be destroyed. |
| `tags` | map(string) | `{}` | Extra tags. |

## Outputs

| Name | Description |
|---|---|
| `user_pool_id` | Becomes `HEXFALL_COGNITO_USER_POOL_ID` in `config.json`. |
| `user_pool_arn` | Pool ARN. |
| `client_id` | Becomes `HEXFALL_COGNITO_CLIENT_ID` in `config.json`. |

## Tests

```bash
terraform init -backend=false
terraform test
```

The tests use `mock_provider "aws"`, so they need no credentials and touch no AWS API.
