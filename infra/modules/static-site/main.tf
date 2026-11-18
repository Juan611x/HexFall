data "aws_caller_identity" "current" {}

# Managed policy, referenced by name so no ID is hard-coded.
data "aws_cloudfront_cache_policy" "caching_optimized" {
  name = "Managed-CachingOptimized"
}

locals {
  bucket_name = "${var.project}-${var.environment}-client-${data.aws_caller_identity.current.account_id}"
}

# ---------------------------------------------------------------------------
# Bucket: private, ACLs disabled, encrypted. Only CloudFront reads it.
# ---------------------------------------------------------------------------

resource "aws_s3_bucket" "client" {
  bucket        = local.bucket_name
  force_destroy = var.force_destroy
  tags          = var.tags
}

resource "aws_s3_bucket_public_access_block" "client" {
  bucket = aws_s3_bucket.client.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_ownership_controls" "client" {
  bucket = aws_s3_bucket.client.id

  rule {
    object_ownership = "BucketOwnerEnforced"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "client" {
  bucket = aws_s3_bucket.client.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_versioning" "client" {
  bucket = aws_s3_bucket.client.id

  versioning_configuration {
    status = var.enable_versioning ? "Enabled" : "Suspended"
  }
}

# ---------------------------------------------------------------------------
# CloudFront in front of the bucket (Origin Access Control, SigV4).
# ---------------------------------------------------------------------------

resource "aws_cloudfront_origin_access_control" "client" {
  name                              = local.bucket_name
  description                       = "Signs CloudFront requests to the ${var.environment} client bucket."
  origin_access_control_origin_type = "s3"
  signing_behavior                  = "always"
  signing_protocol                  = "sigv4"
}

resource "aws_cloudfront_distribution" "client" {
  enabled             = true
  comment             = "${var.project} ${var.environment} WebGL client"
  default_root_object = var.default_root_object
  price_class         = var.price_class
  http_version        = "http2and3"
  is_ipv6_enabled     = true
  tags                = var.tags

  origin {
    origin_id                = "s3-client"
    domain_name              = aws_s3_bucket.client.bucket_regional_domain_name
    origin_access_control_id = aws_cloudfront_origin_access_control.client.id
  }

  default_cache_behavior {
    target_origin_id       = "s3-client"
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["GET", "HEAD"]
    cached_methods         = ["GET", "HEAD"]
    cache_policy_id        = data.aws_cloudfront_cache_policy.caching_optimized.id
    # Unity ships Brotli files with their own Content-Encoding; CloudFront
    # must not try to compress them again.
    compress = false
  }

  restrictions {
    geo_restriction {
      restriction_type = "none"
    }
  }

  viewer_certificate {
    cloudfront_default_certificate = true
  }
}

# ---------------------------------------------------------------------------
# Bucket policy: only this distribution may read objects.
# ---------------------------------------------------------------------------

data "aws_iam_policy_document" "bucket" {
  statement {
    sid       = "AllowCloudFrontRead"
    effect    = "Allow"
    actions   = ["s3:GetObject"]
    resources = ["${aws_s3_bucket.client.arn}/*"]

    principals {
      type        = "Service"
      identifiers = ["cloudfront.amazonaws.com"]
    }

    # Confused-deputy guard: without it any distribution in any account
    # could be pointed at this bucket.
    condition {
      test     = "StringEquals"
      variable = "AWS:SourceArn"
      values   = [aws_cloudfront_distribution.client.arn]
    }
  }
}

resource "aws_s3_bucket_policy" "client" {
  bucket = aws_s3_bucket.client.id
  policy = data.aws_iam_policy_document.bucket.json

  depends_on = [aws_s3_bucket_public_access_block.client]
}
