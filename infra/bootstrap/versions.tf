terraform {
  required_version = ">= 1.10"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
  }

  # Local state on purpose: this root creates the bucket that holds the state of
  # everything else, so it cannot use that bucket. It is applied once by a human.
}
