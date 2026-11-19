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
