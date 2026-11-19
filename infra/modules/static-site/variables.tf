variable "project" {
  description = "Project name. Feeds the bucket name."
  type        = string
  default     = "hexfall"
}

variable "environment" {
  description = "Environment name (dev, prod). Feeds the bucket name."
  type        = string
}

variable "price_class" {
  description = "CloudFront price class."
  type        = string
  default     = "PriceClass_100"

  validation {
    condition     = contains(["PriceClass_100", "PriceClass_200", "PriceClass_All"], var.price_class)
    error_message = "price_class must be PriceClass_100, PriceClass_200 or PriceClass_All."
  }
}

variable "enable_versioning" {
  description = "Keep old object versions in the bucket (rollback of bad uploads)."
  type        = bool
  default     = false
}

variable "force_destroy" {
  description = "Allow terraform destroy to delete a bucket that still holds files."
  type        = bool
  default     = false
}

variable "default_root_object" {
  description = "Object served for the bare URL."
  type        = string
  default     = "index.html"
}

variable "tags" {
  description = "Extra tags merged with the provider default tags."
  type        = map(string)
  default     = {}
}
