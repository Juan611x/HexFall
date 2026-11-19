mock_provider "aws" {}

variables {
  environment = "dev"
}

run "pool_identity_and_sign_up" {
  command = plan

  assert {
    condition     = aws_cognito_user_pool.players.name == "hexfall-dev-players"
    error_message = "Pool name must be <project>-<environment>-players."
  }

  assert {
    condition     = length(aws_cognito_user_pool.players.username_attributes) == 1 && contains(aws_cognito_user_pool.players.username_attributes, "email")
    error_message = "Players must sign in with their email."
  }

  assert {
    condition     = length(aws_cognito_user_pool.players.auto_verified_attributes) == 1 && contains(aws_cognito_user_pool.players.auto_verified_attributes, "email")
    error_message = "Only the email must be auto-verified."
  }

  assert {
    condition     = aws_cognito_user_pool.players.username_configuration[0].case_sensitive == false
    error_message = "Usernames must be case-insensitive."
  }

  assert {
    condition     = aws_cognito_user_pool.players.verification_message_template[0].default_email_option == "CONFIRM_WITH_CODE"
    error_message = "Email verification must use a code."
  }

  assert {
    condition     = aws_cognito_user_pool.players.email_configuration[0].email_sending_account == "COGNITO_DEFAULT"
    error_message = "The lab uses the default Cognito email sender."
  }
}

run "password_policy_matches_client" {
  command = plan

  assert {
    condition = (
      aws_cognito_user_pool.players.password_policy[0].minimum_length == 8 &&
      aws_cognito_user_pool.players.password_policy[0].require_uppercase == true &&
      aws_cognito_user_pool.players.password_policy[0].require_lowercase == true &&
      aws_cognito_user_pool.players.password_policy[0].require_numbers == true &&
      aws_cognito_user_pool.players.password_policy[0].require_symbols == true
    )
    error_message = "Password policy must match the client PasswordPolicy."
  }
}

run "deletion_protection_defaults_active" {
  command = plan

  assert {
    condition     = aws_cognito_user_pool.players.deletion_protection == "ACTIVE"
    error_message = "deletion_protection must default to ACTIVE."
  }
}

run "deletion_protection_can_be_disabled" {
  command = plan

  variables {
    deletion_protection = "INACTIVE"
  }

  assert {
    condition     = aws_cognito_user_pool.players.deletion_protection == "INACTIVE"
    error_message = "deletion_protection must be overridable."
  }
}

run "client_is_public_and_password_auth_only" {
  command = plan

  assert {
    condition     = aws_cognito_user_pool_client.game.name == "hexfall-dev-game"
    error_message = "Client name must be <project>-<environment>-game."
  }

  assert {
    condition     = aws_cognito_user_pool_client.game.generate_secret == false
    error_message = "A WebGL client cannot keep a secret."
  }

  assert {
    condition = (
      length(aws_cognito_user_pool_client.game.explicit_auth_flows) == 2 &&
      contains(aws_cognito_user_pool_client.game.explicit_auth_flows, "ALLOW_USER_PASSWORD_AUTH") &&
      contains(aws_cognito_user_pool_client.game.explicit_auth_flows, "ALLOW_REFRESH_TOKEN_AUTH")
    )
    error_message = "Only USER_PASSWORD_AUTH and REFRESH_TOKEN_AUTH are allowed."
  }

  assert {
    condition     = aws_cognito_user_pool_client.game.prevent_user_existence_errors == "ENABLED"
    error_message = "User existence errors must be hidden."
  }

  assert {
    condition     = length(aws_cognito_user_pool_client.game.allowed_oauth_flows) == 0
    error_message = "No OAuth flows: the client talks to the Cognito API directly."
  }
}
