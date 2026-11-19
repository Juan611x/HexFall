"""Cognito Pre Sign-up trigger that reserves wizard names.

A name is reserved with a conditional DynamoDB write, so two simultaneous
sign-ups for the same name cannot both win. Raising an exception rejects the
sign-up; Cognito returns its text to the client in UserLambdaValidationException.
"""

import os
import re
from datetime import datetime, timedelta, timezone

NAME_PATTERN = re.compile(r"[A-Za-z0-9_]{3,16}")
STALE_AFTER = timedelta(hours=24)

# Free if absent, or already held by this same email (a sign-up retry).
_FREE_OR_MINE = "attribute_not_exists(name_key) OR owner_email = :email"
# Reclaim only while the previous owner still holds the name.
_STILL_OWNED_BY = "owner_email = :owner"


def _s(value):
    return {"S": value}


def _reserve(dynamodb, table, key, name, email, now, condition, extra_values):
    values = {":email": _s(email)}
    values.update(extra_values)
    try:
        dynamodb.put_item(
            TableName=table,
            Item={
                "name_key": _s(key),
                "name": _s(name),
                "owner_email": _s(email),
                "created_at": _s(now.isoformat()),
            },
            ConditionExpression=condition,
            ExpressionAttributeValues=values,
        )
        return True
    except dynamodb.exceptions.ConditionalCheckFailedException:
        return False


def _owner_is_reclaimable(cognito, pool_id, owner_email, now):
    """Delete the owner when it is a stale unconfirmed account. True if the name is free to take."""
    try:
        user = cognito.admin_get_user(UserPoolId=pool_id, Username=owner_email)
    except cognito.exceptions.UserNotFoundException:
        return True
    created = user["UserCreateDate"]
    if user["UserStatus"] == "UNCONFIRMED" and now - created > STALE_AFTER:
        cognito.admin_delete_user(UserPoolId=pool_id, Username=owner_email)
        return True
    return False


def process(event, dynamodb, cognito, table, clock):
    attributes = event["request"]["userAttributes"]
    name = attributes.get("preferred_username")
    if not isinstance(name, str) or not NAME_PATTERN.fullmatch(name):
        raise Exception("InvalidWizardName")

    email = attributes.get("email", "").lower()
    key = name.lower()
    now = clock()

    if _reserve(dynamodb, table, key, name, email, now, _FREE_OR_MINE, {}):
        return event

    current = dynamodb.get_item(TableName=table, Key={"name_key": _s(key)}, ConsistentRead=True)
    item = current.get("Item")
    if item is None:
        # Released between the two calls: one more attempt.
        if _reserve(dynamodb, table, key, name, email, now, _FREE_OR_MINE, {}):
            return event
        raise Exception("WizardNameTaken")

    owner = item["owner_email"]["S"]
    if _owner_is_reclaimable(cognito, event["userPoolId"], owner, now):
        if _reserve(dynamodb, table, key, name, email, now, _STILL_OWNED_BY, {":owner": _s(owner)}):
            return event
    raise Exception("WizardNameTaken")


def handler(event, context):
    import boto3  # Provided by the Lambda runtime; imported here so tests need no AWS SDK.

    return process(
        event,
        boto3.client("dynamodb"),
        boto3.client("cognito-idp"),
        os.environ["NAMES_TABLE"],
        lambda: datetime.now(timezone.utc),
    )
