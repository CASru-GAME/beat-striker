"""HTTP integration tests using a real PHP process and a temporary SQLite database."""

import argparse
import copy
from contextlib import closing
import json
from pathlib import Path
import shutil
import socket
import sqlite3
import subprocess
import tempfile
import time
import unittest
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode
from urllib.request import Request, urlopen


PHP = ["php"]
SOURCE = Path(__file__).resolve().parents[1] / "www" / "beat-striker" / "index.php"


def history():
    return {
        "playerNames": ["ゲスト", "プレイヤー２"],
        "stage": "Street",
        "stageName": "ストリート",
        "musicId": "music-test",
        "musicName": "テスト曲",
        "strikerNames": ["ストライカー", "ストライカー２"],
        "strikerIds": [0, 1],
        "winnerPlayerId": 0,
        "roundWinCounts": [2, 1],
        "playedAt": "2026-11-03T15:00:00.1234567Z",
        "appVersion": "1.0",
        "replayPayload": {
            "schemaVersion": 1,
            "stage": "Street",
            "musicId": "music-test",
            "strikerIds": [0, 1],
            "appVersion": "1.0",
            "rounds": [{
                "roundNumber": 1,
                "beatNotifications": [{"playerId": 0, "beatIndex": 1, "time": 0.5,
                                       "kind": 0, "zone": 1, "button": 0,
                                       "directionX": 0.5, "directionY": -1.0}],
                "preBeatStates": [{"playerId": 0, "applyBeatIndex": 1,
                                   "hitPoint": 100, "specialPoint": 0,
                                   "position": {"x": 1.25, "y": 0, "z": -1},
                                   "statePathId": "Idle", "playbackTime": 0.45}],
            }],
        },
    }


class SakuraApiTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="beat-striker-api-")
        self.root = Path(self.temporary.name)
        self.public = self.root / "www"
        self.api = self.public / "beat-striker"
        self.api.mkdir(parents=True)
        shutil.copy2(SOURCE, self.api / "index.php")
        with socket.socket() as sock:
            sock.bind(("127.0.0.1", 0))
            self.port = sock.getsockname()[1]
        self.base = f"http://127.0.0.1:{self.port}/beat-striker/index.php"
        self.server = None
        self.start_server()

    def start_server(self):
        self.server = subprocess.Popen(
            PHP + ["-d", "post_max_size=32M", "-d", "memory_limit=256M",
                   "-S", f"127.0.0.1:{self.port}", "-t", str(self.public)],
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
        )
        for _ in range(100):
            if self.server.poll() is not None:
                self.fail("PHP server exited; check PHP and pdo_sqlite installation")
            try:
                status, _, _ = self.request()
                if status == 200:
                    return
                self.fail("API health check failed; check pdo_sqlite installation")
            except URLError:
                time.sleep(0.05)
        self.fail("PHP server did not start")

    def stop_server(self):
        if self.server is not None:
            self.server.terminate()
            self.server.wait(timeout=10)
            self.server = None

    def tearDown(self):
        self.stop_server()
        self.temporary.cleanup()

    def request(self, route="/", method="GET", body=None, query=None, raw=None,
                content_type="application/json"):
        data = raw
        if body is not None:
            data = json.dumps(body, ensure_ascii=False).encode("utf-8")
        parameters = {"route": route, **(query or {})}
        request = Request(self.base + "?" + urlencode(parameters), data=data,
                          method=method, headers={"Content-Type": content_type})
        try:
            response = urlopen(request, timeout=20)
        except HTTPError as error:
            response = error
        with response:
            raw_response = response.read()
            result = json.loads(raw_response) if raw_response else None
            return response.status, result, response.headers

    def save(self, body=None):
        status, response, _ = self.request("/battle-histories", "POST", body or history())
        self.assertEqual(status, 200, response)
        self.assertTrue(response["ok"])
        self.assertRegex(response["id"], r"^[a-f0-9]{32}$")
        return response["id"]

    def test_health_initializes_private_database_and_empty_list(self):
        status, response, headers = self.request()
        self.assertEqual(status, 200)
        self.assertEqual(response["storage"], "sqlite")
        self.assertEqual(headers["Cache-Control"], "no-store")
        self.assertTrue((self.root / "beat-striker-data" / "history.sqlite3").is_file())
        self.assertFalse(any(self.public.rglob("*.sqlite3")))
        self.assertEqual(self.request("/battle-histories")[1], {"items": []})

    def test_save_list_detail_and_restart_preserve_replay(self):
        body = history()
        record_id = self.save(body)
        status, result, _ = self.request("/battle-histories")
        self.assertEqual(status, 200)
        summary = result["items"][0]
        self.assertEqual(summary["id"], record_id)
        self.assertEqual(summary["playerNames"], body["playerNames"])
        self.assertTrue(summary["hasReplay"])
        self.assertNotIn("replayPayload", summary)
        self.stop_server()
        self.start_server()
        status, detail, _ = self.request("/battle-histories/" + record_id)
        self.assertEqual(status, 200)
        for key, value in body.items():
            self.assertEqual(detail[key], value, key)
        self.assertEqual(detail["id"], record_id)
        self.assertTrue(detail["createdAt"].endswith("Z"))

    def test_order_and_limit_are_by_upload_order(self):
        ids = []
        for _ in range(105):
            ids.append(self.save())
        for limit, expected in [(1, 1), (2, 2), (999, 100), (0, 50), (-1, 50), ("bad", 50)]:
            with self.subTest(limit=limit):
                status, result, _ = self.request("/battle-histories", query={"limit": limit})
                self.assertEqual(status, 200)
                self.assertEqual([item["id"] for item in result["items"]], ids[::-1][:expected])
        self.assertEqual(len(self.request("/battle-histories")[1]["items"]), 50)

    def test_default_played_at_and_client_supplied_id_cannot_overwrite(self):
        body = history()
        body["playedAt"] = ""
        body["id"] = "forged-id"
        record_id = self.save(body)
        detail = self.request("/battle-histories/" + record_id)[1]
        self.assertEqual(detail["id"], record_id)
        self.assertNotEqual(detail["playedAt"], "")

    def test_invalid_json_body_and_content_type(self):
        for raw in [b"{", b"[]", b"null", b"42", b""]:
            with self.subTest(raw=raw):
                self.assertEqual(self.request("/battle-histories", "POST", raw=raw)[0], 400)
        self.assertEqual(self.request("/battle-histories", "POST", body=history(),
                                      content_type="text/plain")[0], 415)

    def test_invalid_history_is_rejected_without_writing(self):
        mutations = [
            ("playerNames", ["one"]), ("playerNames", [None, "two"]),
            ("strikerNames", "striker"), ("stage", ""), ("musicId", None),
            ("appVersion", 1), ("winnerPlayerId", 2), ("winnerPlayerId", "0"),
            ("winnerPlayerId", True), ("playedAt", "yesterday"),
            ("playedAt", []), ("strikerIds", [0, "1"]),
            ("replayPayload", []), ("musicName", "x" * 513),
        ]
        for field, value in mutations:
            with self.subTest(field=field, value=value):
                body = history()
                body[field] = value
                self.assertEqual(self.request("/battle-histories", "POST", body)[0], 400)
        self.assertEqual(self.request("/battle-histories")[1]["items"], [])

    def test_invalid_replay_metadata_is_rejected(self):
        for field, value in [("schemaVersion", 2), ("stage", "Other"),
                             ("musicId", "Other"), ("appVersion", "Other"),
                             ("strikerIds", []), ("rounds", {})]:
            body = history()
            body["replayPayload"][field] = value
            with self.subTest(field=field):
                self.assertEqual(self.request("/battle-histories", "POST", body)[0], 400)

    def test_missing_id_invalid_route_and_methods(self):
        for route in ["/missing", "/battle-histories/" + "0" * 32,
                      "/battle-histories/../../history.sqlite3", "/battle-histories/' OR 1=1"]:
            self.assertEqual(self.request(route)[0], 404)
        for route, method in [("/battle-histories", "DELETE"), ("/scores", "GET")]:
            status, _, headers = self.request(route, method)
            self.assertEqual(status, 405)
            self.assertIn("OPTIONS", headers["Allow"])
        self.assertEqual(self.request(query={"route[]": "bad"})[0], 400)

    def test_cors_preflight(self):
        status, body, headers = self.request("/battle-histories", "OPTIONS")
        self.assertEqual(status, 204)
        self.assertIsNone(body)
        self.assertEqual(headers["Access-Control-Allow-Origin"], "*")
        self.assertIn("POST", headers["Access-Control-Allow-Methods"])
        self.assertIn("Content-Type", headers["Access-Control-Allow-Headers"])

    def test_large_replay_is_preserved_but_not_in_summary(self):
        body = history()
        notification = body["replayPayload"]["rounds"][0]["beatNotifications"][0]
        body["replayPayload"]["rounds"][0]["beatNotifications"] = [copy.copy(notification) for _ in range(12000)]
        self.assertGreater(len(json.dumps(body)), 1024 * 1024)
        record_id = self.save(body)
        self.assertEqual(self.request("/battle-histories/" + record_id)[1]["replayPayload"], body["replayPayload"])
        self.assertLess(len(json.dumps(self.request("/battle-histories")[1])), 2000)

    def test_request_size_limit(self):
        raw = b'{"padding":"' + b"x" * (25 * 1024 * 1024) + b'"}'
        self.assertEqual(self.request("/battle-histories", "POST", raw=raw)[0], 413)

    def test_scores_compatibility(self):
        status, result, _ = self.request("/scores", "POST", {"name": "ゲスト", "score": 123.5})
        self.assertEqual(status, 200)
        self.assertTrue(result["ok"])
        self.assertEqual(self.request("/scores", "POST", {"name": "guest", "score": "123"})[0], 400)

    def test_storage_error_returns_json_without_exposing_paths(self):
        database = self.root / "beat-striker-data" / "history.sqlite3"
        with closing(sqlite3.connect(database)) as connection, connection:
            connection.execute("DROP TABLE battle_histories")
            connection.execute("CREATE TABLE battle_histories (wrong_column TEXT)")
        status, result, _ = self.request("/battle-histories")
        self.assertEqual(status, 500)
        self.assertIn("storage error", result["error"])
        self.assertNotIn(str(self.root), json.dumps(result))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--php", default="php", help="PHP executable with pdo_sqlite enabled")
    parser.add_argument("--php-ini", help="Optional PHP ini file")
    args, remaining = parser.parse_known_args()
    PHP = [args.php] + (["-c", args.php_ini] if args.php_ini else [])
    unittest.main(argv=[__file__] + remaining, verbosity=2)
