mock_provider "aws" {
  mock_resource "aws_iam_role" {
    defaults = {
      arn = "arn:aws:iam::123456789012:role/mock"
    }
  }

  mock_resource "aws_lambda_function" {
    defaults = {
      arn = "arn:aws:lambda:eu-west-1:123456789012:function:mock"
    }
  }

  mock_resource "aws_cognito_user_pool" {
    defaults = {
      arn = "arn:aws:cognito-idp:eu-west-1:123456789012:userpool/mock"
    }
  }

  mock_resource "aws_dynamodb_table" {
    defaults = {
      arn = "arn:aws:dynamodb:eu-west-1:123456789012:table/mock"
    }
  }

  mock_resource "aws_cloudwatch_log_group" {
    defaults = {
      arn = "arn:aws:logs:eu-west-1:123456789012:log-group:mock"
    }
  }
}

override_data {
  target = data.aws_caller_identity.current
  values = {
    account_id = "123456789012"
  }
}

variables {
  environment            = "dev"
  pre_sign_up_source_dir = "../../../services/wizard_names"
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

run "names_table_is_keyed_by_lowercase_name" {
  command = plan

  assert {
    condition     = aws_dynamodb_table.wizard_names.name == "hexfall-dev-wizard-names"
    error_message = "Table name must be <project>-<environment>-wizard-names."
  }

  assert {
    condition     = aws_dynamodb_table.wizard_names.hash_key == "name_key" && aws_dynamodb_table.wizard_names.billing_mode == "PAY_PER_REQUEST"
    error_message = "The table must be keyed by name_key and billed on demand."
  }
}

# apply against the mock: ARNs and ids are unknown at plan time.
run "trigger_lambda_is_wired" {
  command = apply

  assert {
    condition     = aws_lambda_function.pre_sign_up.runtime == "python3.12" && aws_lambda_function.pre_sign_up.handler == "handler.handler"
    error_message = "The trigger must run handler.handler on python3.12."
  }

  assert {
    condition     = aws_lambda_function.pre_sign_up.environment[0].variables["NAMES_TABLE"] == "hexfall-dev-wizard-names"
    error_message = "The Lambda must receive the table name in NAMES_TABLE."
  }

  assert {
    condition     = aws_cognito_user_pool.players.lambda_config[0].pre_sign_up != null
    error_message = "The pool must call the Lambda before sign-up."
  }

  assert {
    condition     = aws_lambda_permission.cognito_invoke.principal == "cognito-idp.amazonaws.com"
    error_message = "Only Cognito may invoke the trigger."
  }
}

# apply against the mock: ARNs and ids are unknown at plan time.
run "trigger_role_follows_lab_rules" {
  command = apply

  assert {
    condition     = startswith(aws_iam_role.names_trigger.name, "student-")
    error_message = "Lab roles must be named student-*."
  }

  assert {
    condition     = endswith(aws_iam_role.names_trigger.permissions_boundary, "policy/StudentBoundary")
    error_message = "The role must carry the StudentBoundary permissions boundary."
  }
}

# apply against the mock: ARNs and ids are unknown at plan time.
run "trigger_policy_is_scoped" {
  command = apply

  assert {
    condition = alltrue([
      for s in jsondecode(aws_iam_role_policy.names_trigger.policy).Statement :
      !contains(flatten([s.Resource]), "*") if length([for a in flatten([s.Action]) : a if startswith(a, "dynamodb:") || startswith(a, "cognito-idp:")]) > 0
    ])
    error_message = "DynamoDB and Cognito permissions must not use a wildcard resource."
  }
}
