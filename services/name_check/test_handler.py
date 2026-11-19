import json
import unittest

from name_check import handler as h

TABLE = "hexfall-dev-wizard-names"


class FakeDynamo:
    def __init__(self, names=()):
        self.items = {n.lower(): {"name_key": {"S": n.lower()}} for n in names}
        self.calls = []

    def get_item(self, TableName, Key, **kwargs):
        self.calls.append((TableName, Key, kwargs))
        item = self.items.get(Key["name_key"]["S"])
        return {"Item": item} if item else {}


def event(path, method="GET", query=None):
    return {
        "rawPath": path,
        "requestContext": {"http": {"method": method}},
        "pathParameters": None,
        "queryStringParameters": query,
    }


def run(ev, names=()):
    dynamo = FakeDynamo(names)
    response = h.process(ev, dynamo, TABLE)
    return response, dynamo


def body(response):
    return json.loads(response["body"])


class NameCheckTests(unittest.TestCase):
    def test_free_name_is_available(self):
        response, _ = run(event("/names/Merlin"))
        self.assertEqual(response["statusCode"], 200)
        self.assertEqual(body(response), {"name": "Merlin", "available": True})

    def test_registered_name_is_taken(self):
        response, _ = run(event("/names/Merlin"), names=["Merlin"])
        self.assertEqual(response["statusCode"], 200)
        self.assertEqual(body(response), {"name": "Merlin", "available": False})

    def test_check_is_case_insensitive(self):
        response, dynamo = run(event("/names/mERLIN"), names=["Merlin"])
        self.assertFalse(body(response)["available"])
        self.assertEqual(dynamo.calls[0][1], {"name_key": {"S": "merlin"}})

    def test_queries_the_configured_table_with_consistent_read(self):
        _, dynamo = run(event("/names/Merlin"))
        table, _, kwargs = dynamo.calls[0]
        self.assertEqual(table, TABLE)
        self.assertTrue(kwargs.get("ConsistentRead"))

    def test_responses_are_json(self):
        response, _ = run(event("/names/Merlin"))
        self.assertEqual(response["headers"]["Content-Type"], "application/json")

    def test_invalid_names_are_rejected_without_a_lookup(self):
        for name in ["ab", "a" * 17, "bad%20name", "bad-name", "n%C3%A9"]:
            with self.subTest(name=name):
                response, dynamo = run(event("/names/" + name))
                self.assertEqual(response["statusCode"], 400)
                self.assertEqual(body(response), {"error": "InvalidWizardName"})
                self.assertEqual(dynamo.calls, [])

    def test_boundary_lengths_are_valid(self):
        for name in ["abc", "a" * 16]:
            response, _ = run(event("/names/" + name))
            self.assertEqual(response["statusCode"], 200)

    def test_query_string_form_is_not_supported(self):
        response, _ = run(event("/names", query={"name": "Merlin"}))
        self.assertEqual(response["statusCode"], 404)

    def test_unknown_paths_are_404(self):
        for path in ["/", "/names", "/names/", "/names/Merlin/extra", "/other/Merlin"]:
            with self.subTest(path=path):
                response, dynamo = run(event(path))
                self.assertEqual(response["statusCode"], 404)
                self.assertEqual(dynamo.calls, [])

    def test_other_methods_are_405(self):
        for method in ["POST", "PUT", "DELETE", "PATCH"]:
            with self.subTest(method=method):
                response, dynamo = run(event("/names/Merlin", method=method))
                self.assertEqual(response["statusCode"], 405)
                self.assertEqual(response["headers"]["Allow"], "GET")
                self.assertEqual(dynamo.calls, [])

    def test_missing_http_context_is_405(self):
        response, _ = run({"rawPath": "/names/Merlin"})
        self.assertEqual(response["statusCode"], 405)


if __name__ == "__main__":
    unittest.main()
