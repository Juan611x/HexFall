data "aws_caller_identity" "current" {}

locals {
  account_id           = data.aws_caller_identity.current.account_id
  state_bucket_name    = "${var.project}-tfstate-${local.account_id}"
  permissions_boundary = "arn:aws:iam::${local.account_id}:policy/${var.permissions_boundary_name}"

  github_oidc_host = "token.actions.githubusercontent.com"
  oidc_provider_arn = (
    var.create_github_oidc_provider
    ? aws_iam_openid_connect_provider.github[0].arn
    : data.aws_iam_openid_connect_provider.github[0].arn
  )

  # Buckets the client module creates: <project>-<environment>-client-<account id>.
  client_bucket_arn_pattern = "arn:aws:s3:::${var.project}-*-client-*"
}

# ---------------------------------------------------------------------------
# Remote state bucket for every other root (DD-009). Never destroyed by accident.
# ---------------------------------------------------------------------------

resource "aws_s3_bucket" "state" {
  bucket = local.state_bucket_name

  lifecycle {
    prevent_destroy = true
  }
}

resource "aws_s3_bucket_public_access_block" "state" {
  bucket = aws_s3_bucket.state.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_ownership_controls" "state" {
  bucket = aws_s3_bucket.state.id

  rule {
    object_ownership = "BucketOwnerEnforced"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "state" {
  bucket = aws_s3_bucket.state.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

# Versioning keeps every previous state, so a bad apply can be rolled back.
resource "aws_s3_bucket_versioning" "state" {
  bucket = aws_s3_bucket.state.id

  versioning_configuration {
    status = "Enabled"
  }
}

# ---------------------------------------------------------------------------
# GitHub OIDC: Actions exchanges its signed token for short-lived credentials,
# so no access key is stored in GitHub.
# ---------------------------------------------------------------------------

# thumbprint_list is omitted on purpose: AWS validates GitHub's certificate
# against its own trusted CA store and the argument is optional in provider 6.x.
resource "aws_iam_openid_connect_provider" "github" {
  count = var.create_github_oidc_provider ? 1 : 0

  url            = "https://${local.github_oidc_host}"
  client_id_list = ["sts.amazonaws.com"]
}

data "aws_iam_openid_connect_provider" "github" {
  count = var.create_github_oidc_provider ? 0 : 1

  url = "https://${local.github_oidc_host}"
}

data "aws_iam_policy_document" "trust" {
  statement {
    sid     = "GitHubActionsOidc"
    effect  = "Allow"
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [local.oidc_provider_arn]
    }

    condition {
      test     = "StringEquals"
      variable = "${local.github_oidc_host}:aud"
      values   = ["sts.amazonaws.com"]
    }

    # Only this repository, and only the deploy branches or a GitHub Environment.
    # Pull requests from forks produce a different subject and cannot match.
    condition {
      test     = "StringLike"
      variable = "${local.github_oidc_host}:sub"
      values = [
        "repo:${var.github_repository}:ref:refs/heads/develop",
        "repo:${var.github_repository}:ref:refs/heads/main",
        "repo:${var.github_repository}:environment:*",
      ]
    }
  }
}

# The lab only lets roles named student-* exist, and only with the boundary.
resource "aws_iam_role" "ci" {
  name                 = "student-ci-deploy"
  description          = "Assumed by GitHub Actions to deploy the HexFall client."
  assume_role_policy   = data.aws_iam_policy_document.trust.json
  permissions_boundary = local.permissions_boundary
}

# ---------------------------------------------------------------------------
# What the pipeline may do. Each statement names the step that needs it.
# ---------------------------------------------------------------------------

data "aws_iam_policy_document" "ci" {
  # Step "terraform init": read and write state and the .tflock lock file.
  statement {
    sid       = "StateBucketList"
    actions   = ["s3:ListBucket", "s3:GetBucketVersioning"]
    resources = [aws_s3_bucket.state.arn]
  }

  statement {
    sid       = "StateObjects"
    actions   = ["s3:GetObject", "s3:PutObject", "s3:DeleteObject"]
    resources = ["${aws_s3_bucket.state.arn}/*"]
  }

  # Step "terraform apply": create and refresh the client bucket and its
  # settings (public access block, ownership, encryption, versioning, policy).
  statement {
    sid = "ClientBucketManagement"
    actions = [
      "s3:CreateBucket",
      "s3:DeleteBucket",
      "s3:ListBucket",
      "s3:ListBucketVersions",
      "s3:GetBucket*",
      "s3:GetAccelerateConfiguration",
      "s3:GetEncryptionConfiguration",
      "s3:GetLifecycleConfiguration",
      "s3:GetReplicationConfiguration",
      "s3:PutBucketPolicy",
      "s3:DeleteBucketPolicy",
      "s3:PutBucketPublicAccessBlock",
      "s3:PutBucketOwnershipControls",
      "s3:PutEncryptionConfiguration",
      "s3:PutBucketVersioning",
      "s3:PutBucketTagging",
    ]
    resources = [local.client_bucket_arn_pattern]
  }

  # Step "deploy": upload the build, delete stale files, and empty the bucket
  # on destroy (force_destroy).
  statement {
    sid = "ClientObjects"
    actions = [
      "s3:GetObject",
      "s3:PutObject",
      "s3:DeleteObject",
      "s3:DeleteObjectVersion",
      "s3:AbortMultipartUpload",
    ]
    resources = ["${local.client_bucket_arn_pattern}/*"]
  }

  # Steps "terraform apply" and "deploy": distribution and OAC lifecycle plus the
  # cache invalidation. Create actions cannot be scoped to an ARN that does not
  # exist yet, so the resource is "*".
  statement {
    sid = "CloudFrontManagement"
    actions = [
      "cloudfront:CreateDistribution",
      "cloudfront:UpdateDistribution",
      "cloudfront:DeleteDistribution",
      "cloudfront:GetDistribution",
      "cloudfront:GetDistributionConfig",
      "cloudfront:ListDistributions",
      "cloudfront:CreateOriginAccessControl",
      "cloudfront:UpdateOriginAccessControl",
      "cloudfront:DeleteOriginAccessControl",
      "cloudfront:GetOriginAccessControl",
      "cloudfront:GetOriginAccessControlConfig",
      "cloudfront:ListOriginAccessControls",
      "cloudfront:TagResource",
      "cloudfront:UntagResource",
      "cloudfront:ListTagsForResource",
      "cloudfront:CreateInvalidation",
      "cloudfront:GetInvalidation",
      "cloudfront:ListInvalidations",
    ]
    resources = ["*"]
  }

  # Step "terraform plan": the module looks up the managed CachingOptimized policy by name.
  statement {
    sid = "CloudFrontCachePolicyRead"
    actions = [
      "cloudfront:GetCachePolicy",
      "cloudfront:ListCachePolicies",
    ]
    resources = ["*"]
  }

  # Step "configure-aws-credentials" and the smoke checks.
  statement {
    sid       = "WhoAmI"
    actions   = ["sts:GetCallerIdentity"]
    resources = ["*"]
  }
}

resource "aws_iam_policy" "ci" {
  name        = "student-ci-deploy"
  description = "Least-privilege permissions for the HexFall client deploy pipeline."
  policy      = data.aws_iam_policy_document.ci.json
}

resource "aws_iam_role_policy_attachment" "ci" {
  role       = aws_iam_role.ci.name
  policy_arn = aws_iam_policy.ci.arn
}
