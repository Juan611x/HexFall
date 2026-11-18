mock_provider "aws" {
  # The default mocked json is a random string, which aws_s3_bucket_policy
  # rejects as invalid JSON on apply.
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

override_data {
  target = data.aws_cloudfront_cache_policy.caching_optimized
  values = {
    id = "658327ea-f89d-4fab-a63d-7e88639e58f6"
  }
}

variables {
  environment = "dev"
}

run "bucket_is_private_and_encrypted" {
  command = plan

  assert {
    condition     = aws_s3_bucket.client.bucket == "hexfall-dev-client-123456789012"
    error_message = "Bucket name must be <project>-<environment>-client-<account id>."
  }

  assert {
    condition     = aws_s3_bucket.client.force_destroy == false
    error_message = "force_destroy must default to false."
  }

  assert {
    condition = alltrue([
      aws_s3_bucket_public_access_block.client.block_public_acls,
      aws_s3_bucket_public_access_block.client.block_public_policy,
      aws_s3_bucket_public_access_block.client.ignore_public_acls,
      aws_s3_bucket_public_access_block.client.restrict_public_buckets,
    ])
    error_message = "All four Block Public Access settings must be on."
  }

  assert {
    condition     = aws_s3_bucket_ownership_controls.client.rule[0].object_ownership == "BucketOwnerEnforced"
    error_message = "ACLs must be disabled with BucketOwnerEnforced."
  }

  assert {
    condition     = one(aws_s3_bucket_server_side_encryption_configuration.client.rule).apply_server_side_encryption_by_default[0].sse_algorithm == "AES256"
    error_message = "Default encryption must be SSE-S3 (AES256)."
  }
}

run "versioning_is_suspended_by_default" {
  command = plan

  assert {
    condition     = aws_s3_bucket_versioning.client.versioning_configuration[0].status == "Suspended"
    error_message = "Versioning must be Suspended unless enabled."
  }
}

run "versioning_can_be_enabled" {
  command = plan

  variables {
    enable_versioning = true
    force_destroy     = true
  }

  assert {
    condition     = aws_s3_bucket_versioning.client.versioning_configuration[0].status == "Enabled"
    error_message = "enable_versioning = true must set status Enabled."
  }

  assert {
    condition     = aws_s3_bucket.client.force_destroy == true
    error_message = "force_destroy must follow the variable."
  }
}

run "cloudfront_reads_the_bucket_through_oac" {
  command = plan

  assert {
    condition     = aws_cloudfront_origin_access_control.client.origin_access_control_origin_type == "s3"
    error_message = "OAC origin type must be s3."
  }

  assert {
    condition     = aws_cloudfront_origin_access_control.client.signing_protocol == "sigv4"
    error_message = "OAC must sign with sigv4."
  }

  assert {
    condition     = aws_cloudfront_origin_access_control.client.signing_behavior == "always"
    error_message = "OAC must always sign requests."
  }
}

run "distribution_settings" {
  command = plan

  assert {
    condition     = aws_cloudfront_distribution.client.default_root_object == "index.html"
    error_message = "Default root object must be index.html."
  }

  assert {
    condition     = aws_cloudfront_distribution.client.default_cache_behavior[0].viewer_protocol_policy == "redirect-to-https"
    error_message = "HTTP must redirect to HTTPS."
  }

  assert {
    condition     = aws_cloudfront_distribution.client.default_cache_behavior[0].compress == false
    error_message = "compress must be false: Unity files are already Brotli."
  }

  assert {
    condition     = aws_cloudfront_distribution.client.price_class == "PriceClass_100"
    error_message = "Price class must default to PriceClass_100."
  }

  assert {
    condition     = aws_cloudfront_distribution.client.http_version == "http2and3"
    error_message = "HTTP/2 and HTTP/3 must be on."
  }

  assert {
    condition     = aws_cloudfront_distribution.client.is_ipv6_enabled == true
    error_message = "IPv6 must be on."
  }

  assert {
    condition     = aws_cloudfront_distribution.client.viewer_certificate[0].cloudfront_default_certificate == true
    error_message = "The CloudFront default certificate must be used."
  }

  assert {
    condition     = one(aws_cloudfront_distribution.client.restrictions[0].geo_restriction).restriction_type == "none"
    error_message = "No geo restriction."
  }
}

run "price_class_can_be_overridden" {
  command = plan

  variables {
    price_class = "PriceClass_200"
  }

  assert {
    condition     = aws_cloudfront_distribution.client.price_class == "PriceClass_200"
    error_message = "price_class must follow the variable."
  }
}

run "invalid_price_class_is_rejected" {
  command = plan

  variables {
    price_class = "PriceClass_Cheap"
  }

  expect_failures = [var.price_class]
}

# The distribution ARN is unknown at plan time, so the policy is checked after a
# (mocked) apply. The mock does not render data.aws_iam_policy_document.json, so
# the assertions read the statement inputs the module feeds into it.
run "bucket_policy_only_trusts_this_distribution" {
  command = apply

  assert {
    condition     = one(data.aws_iam_policy_document.bucket.statement).effect == "Allow"
    error_message = "The statement must Allow."
  }

  assert {
    condition     = one(data.aws_iam_policy_document.bucket.statement).actions == toset(["s3:GetObject"])
    error_message = "Only s3:GetObject may be granted."
  }

  assert {
    condition     = one(one(data.aws_iam_policy_document.bucket.statement).principals).identifiers == toset(["cloudfront.amazonaws.com"])
    error_message = "The principal must be the CloudFront service."
  }

  assert {
    condition     = one(one(data.aws_iam_policy_document.bucket.statement).condition).test == "StringEquals"
    error_message = "The condition must use StringEquals."
  }

  assert {
    condition     = one(one(data.aws_iam_policy_document.bucket.statement).condition).variable == "AWS:SourceArn"
    error_message = "The condition must match AWS:SourceArn."
  }

  assert {
    condition     = tolist(one(one(data.aws_iam_policy_document.bucket.statement).condition).values) == tolist([aws_cloudfront_distribution.client.arn])
    error_message = "AWS:SourceArn must be this distribution ARN."
  }

  assert {
    condition     = output.url == "https://${aws_cloudfront_distribution.client.domain_name}"
    error_message = "url output must be https://<domain>."
  }
}
