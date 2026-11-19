# user-pool

Amazon Cognito user pool and public app client for the Unity WebGL client. The client calls the Cognito API directly with `USER_PASSWORD_AUTH`; there is no Hosted UI or OAuth. The reasoning is in [DD-014](../../../docs/DESIGN_DECISIONS.md).

Players sign in with their email. The email is auto-verified by a code, and `preferred_username` carries the wizard name. The password policy mirrors the client's `PasswordPolicy`. Emails go through the default Cognito sender (about 50 per day).

## Unique wizard names

A Cognito Pre Sign-up trigger (Lambda, Python 3.12, source in `services/wizard_names/`) reserves each wizard name in a DynamoDB table (`<project>-<environment>-wizard-names`, key `name_key` = lowercase name) with a conditional write. A taken name rejects the sign-up with `WizardNameTaken`. Names held by deleted accounts, or by accounts left UNCONFIRMED for more than 24 hours, are reclaimed. The reasoning is in [DD-018](../../../docs/DESIGN_DECISIONS.md).

The module also creates the Lambda, its log group, an IAM role `student-<project>-<environment>-names-trigger` (with the `StudentBoundary` permissions boundary and a policy scoped to the table and the pool) and the invoke permission for Cognito.

## Inputs

| Name | Type | Default | Description |
|---|---|---|---|
| `project` | string | `hexfall` | Feeds the pool and client names. |
| `environment` | string | required | Feeds the names (`<project>-<environment>-players` and `-game`). |
| `deletion_protection` | string | `ACTIVE` | `ACTIVE` or `INACTIVE`. Dev sets `INACTIVE` so it can be destroyed. |
| `pre_sign_up_source_dir` | string | required | Directory with the trigger source (`handler.py`); zipped with `hashicorp/archive`, tests excluded. |
| `permissions_boundary_name` | string | `StudentBoundary` | Lab permissions boundary attached to the trigger role. |
| `log_retention_days` | number | `14` | Trigger log retention. |
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

The tests use `mock_provider "aws"`, so they need no credentials and touch no AWS API. The trigger runs apply against the mock because ARNs are unknown at plan time.
