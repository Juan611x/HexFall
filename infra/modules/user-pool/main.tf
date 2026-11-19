data "aws_caller_identity" "current" {}

locals {
  name_prefix          = "${var.project}-${var.environment}"
  account_id           = data.aws_caller_identity.current.account_id
  permissions_boundary = "arn:aws:iam::${local.account_id}:policy/${var.permissions_boundary_name}"
  names_table_name     = "${local.name_prefix}-wizard-names"
  trigger_name         = "${local.name_prefix}-names-trigger"
  name_check_name      = "${local.name_prefix}-name-check"
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

  lambda_config {
    pre_sign_up = aws_lambda_function.pre_sign_up.arn
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

# Unique wizard names: a Pre Sign-up trigger reserves each name in DynamoDB
# with a conditional write. See DD-018.
resource "aws_dynamodb_table" "wizard_names" {
  name         = local.names_table_name
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "name_key"

  attribute {
    name = "name_key"
    type = "S"
  }

  tags = var.tags
}

data "archive_file" "pre_sign_up" {
  type        = "zip"
  source_dir  = var.pre_sign_up_source_dir
  output_path = "${path.module}/.build/pre_sign_up.zip"
  excludes    = ["__pycache__", "test_handler.py"]
}

resource "aws_cloudwatch_log_group" "pre_sign_up" {
  name              = "/aws/lambda/${local.trigger_name}"
  retention_in_days = var.log_retention_days
  tags              = var.tags
}

resource "aws_iam_role" "names_trigger" {
  name                 = "student-${local.trigger_name}"
  permissions_boundary = local.permissions_boundary

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "lambda.amazonaws.com" }
      Action    = "sts:AssumeRole"
    }]
  })

  tags = var.tags
}

resource "aws_iam_role_policy" "names_trigger" {
  name = "names-trigger"
  role = aws_iam_role.names_trigger.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect   = "Allow"
        Action   = ["dynamodb:GetItem", "dynamodb:PutItem"]
        Resource = [aws_dynamodb_table.wizard_names.arn]
      },
      {
        Effect   = "Allow"
        Action   = ["cognito-idp:AdminGetUser", "cognito-idp:AdminDeleteUser"]
        Resource = [aws_cognito_user_pool.players.arn]
      },
      {
        Effect   = "Allow"
        Action   = ["logs:CreateLogStream", "logs:PutLogEvents"]
        Resource = ["${aws_cloudwatch_log_group.pre_sign_up.arn}:*"]
      },
    ]
  })
}

resource "aws_lambda_function" "pre_sign_up" {
  function_name    = local.trigger_name
  role             = aws_iam_role.names_trigger.arn
  runtime          = "python3.12"
  handler          = "handler.handler"
  filename         = data.archive_file.pre_sign_up.output_path
  source_code_hash = data.archive_file.pre_sign_up.output_base64sha256
  timeout          = 10

  environment {
    variables = {
      NAMES_TABLE = local.names_table_name
    }
  }

  depends_on = [aws_cloudwatch_log_group.pre_sign_up]

  tags = var.tags
}

resource "aws_lambda_permission" "cognito_invoke" {
  statement_id  = "AllowCognitoInvoke"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.pre_sign_up.function_name
  principal     = "cognito-idp.amazonaws.com"
  source_arn    = aws_cognito_user_pool.players.arn
}

# Public name availability check for guests, served by a Lambda Function URL.
# Read-only: the role can only GetItem on the names table. See DD-021.
data "archive_file" "name_check" {
  type        = "zip"
  source_dir  = var.name_check_source_dir
  output_path = "${path.module}/.build/name_check.zip"
  excludes    = ["__pycache__", "test_handler.py"]
}

resource "aws_cloudwatch_log_group" "name_check" {
  name              = "/aws/lambda/${local.name_check_name}"
  retention_in_days = var.log_retention_days
  tags              = var.tags
}

resource "aws_iam_role" "name_check" {
  name                 = "student-${local.name_check_name}"
  permissions_boundary = local.permissions_boundary

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "lambda.amazonaws.com" }
      Action    = "sts:AssumeRole"
    }]
  })

  tags = var.tags
}

resource "aws_iam_role_policy" "name_check" {
  name = "name-check"
  role = aws_iam_role.name_check.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect   = "Allow"
        Action   = ["dynamodb:GetItem"]
        Resource = [aws_dynamodb_table.wizard_names.arn]
      },
      {
        Effect   = "Allow"
        Action   = ["logs:CreateLogStream", "logs:PutLogEvents"]
        Resource = ["${aws_cloudwatch_log_group.name_check.arn}:*"]
      },
    ]
  })
}

resource "aws_lambda_function" "name_check" {
  function_name    = local.name_check_name
  role             = aws_iam_role.name_check.arn
  runtime          = "python3.12"
  handler          = "handler.handler"
  filename         = data.archive_file.name_check.output_path
  source_code_hash = data.archive_file.name_check.output_base64sha256
  timeout          = 10

  environment {
    variables = {
      NAMES_TABLE = local.names_table_name
    }
  }

  depends_on = [aws_cloudwatch_log_group.name_check]

  tags = var.tags
}

resource "aws_lambda_function_url" "name_check" {
  function_name      = aws_lambda_function.name_check.function_name
  authorization_type = "NONE"

  # Browsers only; GET only. CORS is answered by the URL, not by the handler.
  cors {
    allow_origins = var.name_check_origins
    allow_methods = ["GET"]
    max_age       = 3600
  }
}

# A public URL (auth NONE) needs both statements since October 2025.
resource "aws_lambda_permission" "name_check_url" {
  statement_id           = "AllowPublicInvokeFunctionUrl"
  action                 = "lambda:InvokeFunctionUrl"
  function_name          = aws_lambda_function.name_check.function_name
  principal              = "*"
  function_url_auth_type = "NONE"
}

resource "aws_lambda_permission" "name_check_invoke" {
  statement_id             = "AllowPublicInvokeFunctionViaUrl"
  action                   = "lambda:InvokeFunction"
  function_name            = aws_lambda_function.name_check.function_name
  principal                = "*"
  invoked_via_function_url = true
}
