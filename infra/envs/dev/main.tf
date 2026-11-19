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

  pre_sign_up_source_dir = "${path.root}/../../../services/wizard_names"
}
