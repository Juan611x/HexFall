variable "project" {
  description = "Project name. Feeds the state bucket name."
  type        = string
  default     = "hexfall"
}

variable "aws_region" {
  description = "AWS region. The lab SCP allows only eu-west-1."
  type        = string
  default     = "eu-west-1"
}

variable "github_repository" {
  description = "GitHub repository (owner/name) allowed to assume the CI role."
  type        = string
  default     = "Juan611x/HexFall"
}

variable "permissions_boundary_name" {
  description = "Name of the permissions boundary policy the lab requires on every role."
  type        = string
  default     = "StudentBoundary"
}

variable "create_github_oidc_provider" {
  description = "Create the GitHub OIDC provider. Set false if the account already has one (or denies creating it); it is then read with a data source."
  type        = bool
  default     = true
}
