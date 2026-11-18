# static-site

Private S3 bucket plus a CloudFront distribution (Origin Access Control) that serves the Unity WebGL client over HTTPS. The design and the reason for each setting are in [docs/journal/client-hosting.md](../../../docs/journal/client-hosting.md).

Terraform owns the infrastructure only. Files are uploaded by `tools/deploy_webgl.py`, which reads the outputs below.

## Inputs

| Name | Type | Default | Description |
|---|---|---|---|
| `project` | string | `hexfall` | Feeds the bucket name. |
| `environment` | string | required | Feeds the bucket name (`<project>-<environment>-client-<account id>`). |
| `price_class` | string | `PriceClass_100` | One of `PriceClass_100`, `PriceClass_200`, `PriceClass_All`. |
| `enable_versioning` | bool | `false` | Bucket versioning `Enabled` or `Suspended`. |
| `force_destroy` | bool | `false` | Let `terraform destroy` delete a non-empty bucket. |
| `default_root_object` | string | `index.html` | Object served for the bare URL. |
| `tags` | map(string) | `{}` | Extra tags. |

## Outputs

| Name | Description |
|---|---|
| `bucket_name` | Upload target. |
| `bucket_arn` | Bucket ARN. |
| `distribution_id` | Target of the cache invalidation. |
| `distribution_arn` | Distribution ARN. |
| `distribution_domain_name` | The `dxxxx.cloudfront.net` host. |
| `url` | `https://<domain>`. |

## Tests

```bash
terraform init -backend=false
terraform test
```

The tests use `mock_provider "aws"`, so they need no credentials and touch no AWS API.
