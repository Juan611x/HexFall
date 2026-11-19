"""Public wizard name availability check, served through a Lambda Function URL.

GET /names/<name> answers whether a wizard name is free for a guest. A name is
taken when a reservation exists, even a reclaimable one (the conservative
answer for guests; DD-018 owns reclaiming). CORS is answered by the Function
URL itself, so the handler adds no CORS headers.
"""

import json
import os
import re

NAME_PATTERN = re.compile(r"[A-Za-z0-9_]{3,16}")
_PATH_PREFIX = "/names/"


def _respond(status, payload, extra_headers=None):
    headers = {"Content-Type": "application/json"}
    if extra_headers:
        headers.update(extra_headers)
    return {"statusCode": status, "headers": headers, "body": json.dumps(payload)}


def process(event, dynamodb, table):
    method = event.get("requestContext", {}).get("http", {}).get("method")
    if method != "GET":
        return _respond(405, {"error": "MethodNotAllowed"}, {"Allow": "GET"})

    path = event.get("rawPath") or ""
    if not path.startswith(_PATH_PREFIX) or "/" in path[len(_PATH_PREFIX):] or path == _PATH_PREFIX:
        return _respond(404, {"error": "NotFound"})

    name = path[len(_PATH_PREFIX):]
    if not NAME_PATTERN.fullmatch(name):
        return _respond(400, {"error": "InvalidWizardName"})

    found = dynamodb.get_item(
        TableName=table,
        Key={"name_key": {"S": name.lower()}},
        ConsistentRead=True,
    )
    return _respond(200, {"name": name, "available": "Item" not in found})


def handler(event, context):
    import boto3  # Provided by the Lambda runtime; imported here so tests need no AWS SDK.

    return process(event, boto3.client("dynamodb"), os.environ["NAMES_TABLE"])
