mock_provider "aws" {
  # The default mocked json is a random string, which the provider rejects as
  # invalid JSON. The assertions below read the statement inputs instead.
  mock_data "aws_iam_policy_document" {
    defaults = {
      json = "{\"Version\":\"2012-10-17\",\"Statement\":[]}"
    }
  }
}

override_data {
  target = data.aws_caller_identity.current
  values = {
    account_id = "123456789012"
  }
}

run "state_bucket_is_private_versioned_and_protected" {
  command = plan

  assert {
    condition     = aws_s3_bucket.state.bucket == "hexfall-tfstate-123456789012"
    error_message = "State bucket must be <project>-tfstate-<account id>."
  }

  assert {
    condition     = aws_s3_bucket_versioning.state.versioning_configuration[0].status == "Enabled"
    error_message = "State bucket versioning must be Enabled."
  }

  assert {
    condition = alltrue([
      aws_s3_bucket_public_access_block.state.block_public_acls,
      aws_s3_bucket_public_access_block.state.block_public_policy,
      aws_s3_bucket_public_access_block.state.ignore_public_acls,
      aws_s3_bucket_public_access_block.state.restrict_public_buckets,
    ])
    error_message = "All four Block Public Access settings must be on."
  }

  assert {
    condition     = aws_s3_bucket_ownership_controls.state.rule[0].object_ownership == "BucketOwnerEnforced"
    error_message = "ACLs must be disabled with BucketOwnerEnforced."
  }

  assert {
    condition     = one(aws_s3_bucket_server_side_encryption_configuration.state.rule).apply_server_side_encryption_by_default[0].sse_algorithm == "AES256"
    error_message = "State must be encrypted with SSE-S3."
  }
}

run "default_bootstrap_creates_only_the_state_bucket" {
  command = plan

  # The lab account denies the GitHub OIDC provider and IAM users, so CI access
  # stays off until there is a way in (docs/DEPLOY.md, "CI/CD status").
  assert {
    condition = alltrue([
      length(aws_iam_openid_connect_provider.github) == 0,
      length(data.aws_iam_openid_connect_provider.github) == 0,
      length(aws_iam_role.ci) == 0,
      length(aws_iam_policy.ci) == 0,
      length(aws_iam_role_policy_attachment.ci) == 0,
    ])
    error_message = "Without enable_ci_access the bootstrap must not touch IAM."
  }

  assert {
    condition     = output.ci_role_arn == null && output.oidc_provider_arn == null
    error_message = "CI outputs must be null when CI access is off."
  }
}

run "ci_role_follows_the_account_rules" {
  command = plan

  variables {
    enable_ci_access          = true
    permissions_boundary_name = "StudentBoundary"
  }

  assert {
    condition     = startswith(aws_iam_role.ci[0].name, "student-")
    error_message = "IAM role names must start with student-."
  }

  assert {
    condition     = endswith(aws_iam_role.ci[0].permissions_boundary, "policy/StudentBoundary")
    error_message = "The role must carry the StudentBoundary permissions boundary."
  }

  assert {
    condition     = aws_iam_role.ci[0].permissions_boundary == "arn:aws:iam::123456789012:policy/StudentBoundary"
    error_message = "The boundary ARN must use this account id."
  }

  assert {
    condition     = startswith(aws_iam_policy.ci[0].name, "student-")
    error_message = "Customer policy names must start with student-."
  }
}

# Outside the lab there is no StudentBoundary policy, so no boundary is the default.
run "ci_role_has_no_boundary_by_default" {
  command = plan

  variables {
    enable_ci_access = true
  }

  assert {
    condition     = aws_iam_role.ci[0].permissions_boundary == null
    error_message = "Without permissions_boundary_name the CI role must not carry a boundary."
  }
}

run "trust_policy_is_limited_to_the_repository_branches" {
  command = plan

  variables {
    enable_ci_access = true
  }

  assert {
    condition     = one(data.aws_iam_policy_document.trust[0].statement).actions == toset(["sts:AssumeRoleWithWebIdentity"])
    error_message = "Only AssumeRoleWithWebIdentity may be allowed."
  }

  assert {
    condition = anytrue([
      for c in one(data.aws_iam_policy_document.trust[0].statement).condition :
      c.test == "StringEquals" && c.variable == "token.actions.githubusercontent.com:aud" && toset(c.values) == toset(["sts.amazonaws.com"])
    ])
    error_message = "The audience must be sts.amazonaws.com."
  }

  assert {
    condition = anytrue([
      for c in one(data.aws_iam_policy_document.trust[0].statement).condition :
      c.test == "StringLike" && c.variable == "token.actions.githubusercontent.com:sub" && toset(c.values) == toset([
        "repo:Juan611x/HexFall:ref:refs/heads/develop",
        "repo:Juan611x/HexFall:ref:refs/heads/main",
        "repo:Juan611x/HexFall:environment:dev",
        "repo:Juan611x/HexFall:environment:prod",
      ])
    ])
    error_message = "The subject must be limited to develop, main and the dev and prod environments, never any environment."
  }
}

run "oidc_provider_is_created_by_default" {
  command = plan

  variables {
    enable_ci_access = true
  }

  assert {
    condition     = length(aws_iam_openid_connect_provider.github) == 1
    error_message = "The GitHub OIDC provider must be created by default."
  }

  assert {
    condition     = aws_iam_openid_connect_provider.github[0].url == "https://token.actions.githubusercontent.com"
    error_message = "The provider URL must be the GitHub Actions issuer."
  }

  assert {
    condition     = aws_iam_openid_connect_provider.github[0].client_id_list == toset(["sts.amazonaws.com"])
    error_message = "The client id must be sts.amazonaws.com."
  }
}

run "existing_oidc_provider_is_reused_when_creation_is_off" {
  command = plan

  variables {
    enable_ci_access            = true
    create_github_oidc_provider = false
  }

  override_data {
    target = data.aws_iam_openid_connect_provider.github[0]
    values = {
      arn = "arn:aws:iam::123456789012:oidc-provider/token.actions.githubusercontent.com"
    }
  }

  assert {
    condition     = length(aws_iam_openid_connect_provider.github) == 0
    error_message = "No provider may be created when create_github_oidc_provider is false."
  }

  assert {
    condition     = output.oidc_provider_arn == "arn:aws:iam::123456789012:oidc-provider/token.actions.githubusercontent.com"
    error_message = "The existing provider ARN must be exposed."
  }
}

run "repository_is_a_parameter" {
  command = plan

  variables {
    enable_ci_access  = true
    github_repository = "someone/else"
  }

  assert {
    condition = anytrue([
      for c in one(data.aws_iam_policy_document.trust[0].statement).condition :
      c.variable == "token.actions.githubusercontent.com:sub" && contains(c.values, "repo:someone/else:ref:refs/heads/develop")
    ])
    error_message = "github_repository must drive the subject."
  }
}
