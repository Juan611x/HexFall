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
