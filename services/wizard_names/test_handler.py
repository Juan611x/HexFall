import unittest
from datetime import datetime, timedelta, timezone

from wizard_names import handler as h

NOW = datetime(2026, 11, 19, 12, 0, tzinfo=timezone.utc)
POOL = "eu-west-1_pool"
TABLE = "hexfall-dev-wizard-names"


class UserNotFound(Exception):
    pass


class ConditionalCheckFailed(Exception):
    pass


class FakeDynamo:
    """Evaluates only the two condition expressions the handler uses."""

    class exceptions:
        ConditionalCheckFailedException = ConditionalCheckFailed

    def __init__(self):
        self.items = {}
        self.put_calls = 0

    def put_item(self, TableName, Item, ConditionExpression, ExpressionAttributeValues):
        self.put_calls += 1
        key = Item["name_key"]["S"]
        current = self.items.get(key)
        values = ExpressionAttributeValues
        if "attribute_not_exists" in ConditionExpression:
            ok = current is None or current["owner_email"]["S"] == values[":email"]["S"]
        else:
            ok = current is not None and current["owner_email"]["S"] == values[":owner"]["S"]
        if not ok:
            raise ConditionalCheckFailed()
        self.items[key] = Item

    def get_item(self, TableName, Key, ConsistentRead):
        item = self.items.get(Key["name_key"]["S"])
        return {"Item": item} if item else {}


class FakeCognito:
    class exceptions:
        UserNotFoundException = UserNotFound

    def __init__(self):
        self.users = {}
        self.deleted = []
        self.on_get = None

    def admin_get_user(self, UserPoolId, Username):
        if self.on_get:
            self.on_get()
        if Username not in self.users:
            raise UserNotFound()
        status, created = self.users[Username]
        return {"UserStatus": status, "UserCreateDate": created}

    def admin_delete_user(self, UserPoolId, Username):
        self.deleted.append((UserPoolId, Username))
        del self.users[Username]


def event(name="Merlin", email="new@example.com"):
    return {
        "userPoolId": POOL,
        "userName": "x",
        "request": {"userAttributes": {"email": email, "preferred_username": name}},
        "response": {},
    }


def owner_item(name, email):
    return {
        "name_key": {"S": name.lower()},
        "name": {"S": name},
        "owner_email": {"S": email},
        "created_at": {"S": "2026-11-01T00:00:00+00:00"},
    }


class ReserveTests(unittest.TestCase):
    def setUp(self):
        self.db = FakeDynamo()
        self.idp = FakeCognito()

    def run_event(self, ev):
        return h.process(ev, self.db, self.idp, TABLE, lambda: NOW)

    def test_new_name_is_reserved_with_lowercase_key(self):
        ev = event("Merlin", "New@Example.com")
        self.assertIs(self.run_event(ev), ev)
        item = self.db.items["merlin"]
        self.assertEqual(item["name"]["S"], "Merlin")
        self.assertEqual(item["owner_email"]["S"], "new@example.com")
        self.assertEqual(item["created_at"]["S"], NOW.isoformat())

    def test_name_taken_by_confirmed_account_is_rejected(self):
        self.db.items["merlin"] = owner_item("Merlin", "old@example.com")
        self.idp.users["old@example.com"] = ("CONFIRMED", NOW - timedelta(days=30))
        with self.assertRaisesRegex(Exception, "WizardNameTaken"):
            self.run_event(event("Merlin"))
        self.assertEqual(self.db.items["merlin"]["owner_email"]["S"], "old@example.com")

    def test_same_email_retry_is_allowed(self):
        self.db.items["merlin"] = owner_item("Merlin", "new@example.com")
        self.run_event(event("Merlin", "new@example.com"))
        self.assertEqual(self.idp.deleted, [])

    def test_clash_is_case_insensitive(self):
        self.db.items["merlin"] = owner_item("Merlin", "old@example.com")
        self.idp.users["old@example.com"] = ("CONFIRMED", NOW - timedelta(days=30))
        with self.assertRaisesRegex(Exception, "WizardNameTaken"):
            self.run_event(event("MERLIN"))

    def test_owner_not_found_is_reclaimed(self):
        self.db.items["merlin"] = owner_item("Merlin", "gone@example.com")
        self.run_event(event("merlin"))
        self.assertEqual(self.db.items["merlin"]["owner_email"]["S"], "new@example.com")
        self.assertEqual(self.idp.deleted, [])

    def test_stale_unconfirmed_owner_is_deleted_then_reclaimed(self):
        self.db.items["merlin"] = owner_item("Merlin", "stale@example.com")
        self.idp.users["stale@example.com"] = ("UNCONFIRMED", NOW - timedelta(hours=25))
        self.run_event(event("Merlin"))
        self.assertEqual(self.idp.deleted, [(POOL, "stale@example.com")])
        self.assertEqual(self.db.items["merlin"]["owner_email"]["S"], "new@example.com")

    def test_recent_unconfirmed_owner_keeps_the_name(self):
        self.db.items["merlin"] = owner_item("Merlin", "fresh@example.com")
        self.idp.users["fresh@example.com"] = ("UNCONFIRMED", NOW - timedelta(hours=1))
        with self.assertRaisesRegex(Exception, "WizardNameTaken"):
            self.run_event(event("Merlin"))
        self.assertEqual(self.idp.deleted, [])

    def test_invalid_format_is_rejected_without_table_write(self):
        for bad in ["ab", "a" * 17, "bad name", "tilde~", "", None]:
            with self.subTest(name=bad):
                with self.assertRaisesRegex(Exception, "InvalidWizardName"):
                    self.run_event(event(bad))
        self.assertEqual(self.db.put_calls, 0)

    def test_reclaim_race_lost_is_rejected(self):
        self.db.items["merlin"] = owner_item("Merlin", "stale@example.com")
        self.idp.users["stale@example.com"] = ("UNCONFIRMED", NOW - timedelta(hours=25))

        def other_player_wins():
            self.db.items["merlin"] = owner_item("Merlin", "rival@example.com")

        self.idp.on_get = other_player_wins
        with self.assertRaisesRegex(Exception, "WizardNameTaken"):
            self.run_event(event("Merlin"))
        self.assertEqual(self.db.items["merlin"]["owner_email"]["S"], "rival@example.com")


if __name__ == "__main__":
    unittest.main()
