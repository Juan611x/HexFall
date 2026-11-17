# AWS Environment

HexFall deploys to a restricted lab account managed through AWS IAM Identity Center. This document records what the account allows, verified with real API calls, so the infrastructure code respects it from day one.

## Access

Access is through an IAM Identity Center permission set (`StudentLabAccess`). Configure a CLI profile once with `aws configure sso`; the values below are what matter:

| Setting | Value |
|---|---|
| SSO region (Identity Center) | `eu-west-1` |
| Workload region | `eu-west-1` |
| Local profile name (convention) | `commit-lab` |

```bash
aws sso login --profile commit-lab
aws sts get-caller-identity --profile commit-lab
```

> The SSO region must be `eu-west-1`. Using another region fails with `InvalidRequestException` on `RegisterClient` or `StartDeviceAuthorization`.

## Effective permissions

The permission set grants `PowerUserAccess` plus an inline policy for limited IAM management. Organization SCPs then restrict it further.

### Service control policies

- **Region lock:** every regional service is denied outside `eu-west-1`. Global services (CloudFront, IAM reads) work.
- **GameLift:** explicitly denied in every region.

### Service access in `eu-west-1` (verified with read-only calls)

| Allowed | Denied |
|---|---|
| API Gateway (REST and WebSocket), Lambda, DynamoDB, S3, CloudFront, EC2, Elastic Load Balancing, ECS, ECR, Cognito, SQS, CloudWatch Logs | GameLift |

Read access was verified; create access must be confirmed when each resource is first provisioned.

### IAM rules for infrastructure code

| Action | Allowed only for |
|---|---|
| Create, update, attach policies to roles | Roles named `student-*` **with** permissions boundary `StudentBoundary` |
| Delete, tag roles | Roles named `student-*` |
| Create and manage customer policies | Policies named `student-*` |
| Create instance profiles | Instance profiles named `student-*` |
| `iam:PassRole` | `student-*` and the pre-provisioned roles below |
| Create service-linked roles | ECS and EFS only |

Pre-provisioned roles that can be passed to services:

- `studentEcsTaskExecutionRole` (ECS task execution)
- `studentLambdaExecutionRole` (Lambda execution)
- `studentCodePipelineServiceRole`, `studentCodeBuildServiceRole`, `studentCodeDeployServiceRole`, `studentCodeDeployEC2Role`

In Terraform this means every `aws_iam_role` sets `name` with the `student-` prefix and `permissions_boundary` pointing to the `StudentBoundary` policy ARN.

## Known risks

- **Custom domain on CloudFront:** requires an ACM certificate in `us-east-1`, which the region lock likely blocks. Use the default `*.cloudfront.net` domain unless proven otherwise.
- **Console region:** the AWS console may open in `us-east-1`, where almost everything shows "Access denied". Switch to `eu-west-1` before concluding a service is unavailable.
- **Policy simulation ignores SCPs:** `iam simulate-principal-policy` reports actions as allowed even when an SCP denies them. Test with real calls.
