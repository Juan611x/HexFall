output "state_bucket_name" {
  description = "Bucket for remote state. Goes into backend.hcl and the TF_STATE_BUCKET variable."
  value       = aws_s3_bucket.state.bucket
}

output "ci_role_arn" {
  description = "Role GitHub Actions assumes. Goes into the AWS_ROLE_ARN variable."
  value       = aws_iam_role.ci.arn
}

output "oidc_provider_arn" {
  description = "GitHub OIDC provider (created here or read from the account)."
  value       = local.oidc_provider_arn
}
