provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project     = "hexfall"
      Environment = "dev"
      ManagedBy   = "terraform"
    }
  }
}

module "client" {
  source = "../../modules/static-site"

  project       = "hexfall"
  environment   = "dev"
  force_destroy = true
}

module "auth" {
  source = "../../modules/user-pool"

  project             = "hexfall"
  environment         = "dev"
  deletion_protection = "INACTIVE"

  # The lab account requires this boundary on every role (docs/AWS_ENVIRONMENT.md).
  permissions_boundary_name = "StudentBoundary"

  pre_sign_up_source_dir = "${path.root}/../../../services/wizard_names"
  name_check_source_dir  = "${path.root}/../../../services/name_check"
  name_check_origins     = [module.client.url]
}
