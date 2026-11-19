variable "project" {
  description = "Project name. Feeds the pool and client names."
  type        = string
  default     = "hexfall"
}

variable "environment" {
  description = "Environment name (dev, prod). Feeds the pool and client names."
  type        = string
}

variable "deletion_protection" {
  description = "ACTIVE blocks deleting the pool (and its players) until it is switched off."
  type        = string
  default     = "ACTIVE"

  validation {
    condition     = contains(["ACTIVE", "INACTIVE"], var.deletion_protection)
    error_message = "deletion_protection must be ACTIVE or INACTIVE."
  }
}

variable "tags" {
  description = "Extra tags."
  type        = map(string)
  default     = {}
}

variable "pre_sign_up_source_dir" {
  description = "Directory with the Pre Sign-up Lambda source (handler.py). Tests are excluded from the zip."
  type        = string
}

variable "permissions_boundary_name" {
  description = "Name of a permissions boundary policy attached to the Lambda roles. The lab account requires \"StudentBoundary\"; leave null in accounts without such a policy."
  type        = string
  default     = null
}

variable "log_retention_days" {
  description = "Retention of the trigger Lambda logs."
  type        = number
  default     = 14
}

variable "name_check_source_dir" {
  description = "Directory with the name check Lambda source (handler.py). Tests are excluded from the zip."
  type        = string
}

variable "name_check_origins" {
  description = "Browser origins allowed to call the name check URL (CORS), for example the client CloudFront URL."
  type        = list(string)
}
