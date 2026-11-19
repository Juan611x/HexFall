locals {
  name_prefix = "${var.project}-${var.environment}"
}

resource "aws_cognito_user_pool" "players" {
  name                = "${local.name_prefix}-players"
  deletion_protection = var.deletion_protection

  username_attributes      = ["email"]
  auto_verified_attributes = ["email"]

  username_configuration {
    case_sensitive = false
  }

  # Must match PasswordPolicy in the client.
  password_policy {
    minimum_length    = 8
    require_uppercase = true
    require_lowercase = true
    require_numbers   = true
    require_symbols   = true
  }

  verification_message_template {
    default_email_option = "CONFIRM_WITH_CODE"
    email_subject        = "HexFall verification code"
    email_message        = "Your HexFall verification code is {####}"
  }

  # The default sender is limited to about 50 emails a day, enough for the lab.
  email_configuration {
    email_sending_account = "COGNITO_DEFAULT"
  }

  account_recovery_setting {
    recovery_mechanism {
      name     = "verified_email"
      priority = 1
    }
  }

  admin_create_user_config {
    allow_admin_create_user_only = false
  }

  tags = var.tags
}

resource "aws_cognito_user_pool_client" "game" {
  name         = "${local.name_prefix}-game"
  user_pool_id = aws_cognito_user_pool.players.id

  # The WebGL client is public and cannot keep a secret.
  generate_secret = false

  explicit_auth_flows = [
    "ALLOW_USER_PASSWORD_AUTH",
    "ALLOW_REFRESH_TOKEN_AUTH",
  ]

  # No Hosted UI or OAuth: the client calls the Cognito API directly.
  allowed_oauth_flows_user_pool_client = false
  allowed_oauth_flows                  = []

  prevent_user_existence_errors = "ENABLED"
  enable_token_revocation       = true

  access_token_validity  = 60
  id_token_validity      = 60
  refresh_token_validity = 30

  token_validity_units {
    access_token  = "minutes"
    id_token      = "minutes"
    refresh_token = "days"
  }

  read_attributes  = ["email", "email_verified", "preferred_username"]
  write_attributes = ["email", "preferred_username"]
}
