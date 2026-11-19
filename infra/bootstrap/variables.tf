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

variable "deploy_environments" {
  description = "GitHub Environments whose jobs may assume the CI role (matches `environment:` in the workflows)."
  type        = list(string)
  default     = ["dev", "prod"]
}

variable "permissions_boundary_name" {
  description = "Name of a permissions boundary policy to attach to every role. The lab account requires \"StudentBoundary\"; leave null in accounts without such a policy."
  type        = string
  default     = null
}

variable "enable_ci_access" {
  description = "Create the GitHub OIDC trust, CI role and CI policy. Off: the lab account denies the OIDC provider and IAM users, so deploys run by hand (docs/DEPLOY.md)."
  type        = bool
  default     = false
}

variable "create_github_oidc_provider" {
  description = "Create the GitHub OIDC provider. Set false if the account already has one (or denies creating it); it is then read with a data source."
  type        = bool
  default     = true
}
