output "bucket_name" {
  description = "Bucket the deploy script uploads to."
  value       = aws_s3_bucket.client.bucket
}

output "bucket_arn" {
  description = "Bucket ARN."
  value       = aws_s3_bucket.client.arn
}

output "distribution_id" {
  description = "CloudFront distribution id, target of the invalidation."
  value       = aws_cloudfront_distribution.client.id
}

output "distribution_arn" {
  description = "CloudFront distribution ARN."
  value       = aws_cloudfront_distribution.client.arn
}

output "distribution_domain_name" {
  description = "The dxxxx.cloudfront.net host."
  value       = aws_cloudfront_distribution.client.domain_name
}

output "url" {
  description = "Public HTTPS address of the client."
  value       = "https://${aws_cloudfront_distribution.client.domain_name}"
}
