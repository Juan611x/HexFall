# These outputs are the contract with tools/deploy_webgl.py and the deploy workflow.

output "bucket_name" {
  value = module.client.bucket_name
}

output "bucket_arn" {
  value = module.client.bucket_arn
}

output "distribution_id" {
  value = module.client.distribution_id
}

output "distribution_arn" {
  value = module.client.distribution_arn
}

output "distribution_domain_name" {
  value = module.client.distribution_domain_name
}

output "url" {
  value = module.client.url
}

output "cognito_user_pool_id" {
  value = module.auth.user_pool_id
}

output "cognito_client_id" {
  value = module.auth.client_id
}

output "name_check_url" {
  value = module.auth.name_check_url
}

output "aws_region" {
  value = var.aws_region
}
