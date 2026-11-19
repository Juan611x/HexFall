output "user_pool_id" {
  description = "Cognito user pool id, exported to the client as HEXFALL_COGNITO_USER_POOL_ID."
  value       = aws_cognito_user_pool.players.id
}

output "user_pool_arn" {
  description = "Cognito user pool ARN."
  value       = aws_cognito_user_pool.players.arn
}

output "client_id" {
  description = "App client id, exported to the client as HEXFALL_COGNITO_CLIENT_ID."
  value       = aws_cognito_user_pool_client.game.id
}
