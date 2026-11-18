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
