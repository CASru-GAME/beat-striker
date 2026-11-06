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
import uuid
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
                content_type="application/json", html=False):
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
            if html:
                return response.status, raw_response.decode("utf-8"), response.headers
            result = json.loads(raw_response) if raw_response else None
            return response.status, result, response.headers

    def save(self, body=None):
        status, response, _ = self.request("/battle-histories", "POST", body or history())
        self.assertEqual(status, 200, response)
        self.assertTrue(response["ok"])
        self.assertRegex(response["id"], r"^[a-f0-9]{32}$")
        return response["id"]

    def profile(self, name, **extra):
        status, result, _ = self.request('/players', 'POST', {
            'name': name, 'createdAt': '2026-11-01T00:00:00Z', **extra})
        self.assertEqual(status, 200, result)
        return result

    def trial_history(self, name='挑戦者', start=0, winner=0, played='2026-11-03T15:00:00Z'):
        body = history()
        body.update(battleId=uuid.uuid4().hex, mode='Trial', trialStartGrade=start,
                    trialTargetGrade=start + 1 if start < 5 else -1,
                    participants=[{'name': name, 'grade': start, 'isCpu': False, 'cpuLevel': 0},
                                  {'name': 'CPU', 'grade': min(start + 1, 5), 'isCpu': True,
                                   'cpuLevel': [1, 2, 3, 4, 4, 4][start]}],
                    playerNames=[name, 'CPU'], playedAt=played, winnerPlayerId=winner,
                    strikerIds=[0, 4 if start == 4 else 1])
        body['replayPayload']['strikerIds'] = body['strikerIds']
        return body

    def test_player_names_initial_grade_and_identity(self):
        first = self.profile('  プレイヤー  ', grade=5)
        self.assertEqual(first['name'], 'プレイヤー')
        self.assertEqual(first['grade'], 0)
        self.assertEqual(self.profile('プレイヤー'), first)
        self.assertEqual(self.profile('あ' * 10)['name'], 'あ' * 10)
        for invalid in ['', '  ', 'あ' * 11, '名前\n改行']:
            self.assertEqual(self.request('/players', 'POST', {'name': invalid})[0], 400)
        self.profile('ABC')
        self.profile('abc')
        names = [item['name'] for item in self.request('/players', query={'grade': 0})[1]['items']]
        self.assertIn('ABC', names)
        self.assertIn('abc', names)

    def test_feedback_is_immutable_per_name_and_persists(self):
        route = '/feedback'
        self.assertFalse(self.request(route, query={'name': '奏者'})[1]['submitted'])
        comment = '🎵' * 200
        self.assertEqual(self.request(route, 'POST', {'name': '奏者', 'comment': comment})[0], 201)
        self.assertEqual(self.request(route, 'POST', {'name': ' 奏者 ', 'comment': '上書き'})[0], 409)
        for method in ['PUT', 'PATCH', 'DELETE']:
            self.assertEqual(self.request(route, method, {'name': '奏者', 'comment': '上書き'})[0], 405)

        self.assertEqual(self.request(route, 'POST', {'name': '別の奏者', 'comment': ''})[0], 201)
        self.assertEqual(self.request(route, 'POST', {'name': '別の奏者', 'comment': '後から送信'})[0], 409)
        for invalid in [None, [], 'あ' * 201, '\n' * 201]:
            self.assertEqual(self.request(route, 'POST', {'name': '無効', 'comment': invalid})[0], 400)
        self.assertEqual(self.request(route, 'POST', {'name': 'ゲスト', 'comment': ''})[0], 400)
        self.assertFalse(self.request(route, query={'name': '無効'})[1]['submitted'])

        database = self.root / 'beat-striker-data' / 'history.sqlite3'
        with closing(sqlite3.connect(database)) as connection, connection:
            for statement in ["UPDATE player_feedback SET comment = ''", "DELETE FROM player_feedback"]:
                with self.assertRaisesRegex(sqlite3.IntegrityError, 'feedback is immutable'):
                    connection.execute(statement)
        self.stop_server()
        self.start_server()
        self.assertTrue(self.request(route, query={'name': '奏者'})[1]['submitted'])
        self.assertTrue(self.request(route, query={'name': '別の奏者'})[1]['submitted'])
        _, page, _ = self.request('/feedback-list', html=True)
        self.assertIn(comment, page)
        self.assertIn('（本文なし）', page)
        self.assertNotIn('上書き', page)

    def test_feedback_list_escapes_html_and_paginates(self):
        route = '/feedback-list'
        status, page, headers = self.request(route, html=True)
        self.assertEqual(status, 200)
        self.assertIn('text/html', headers['Content-Type'])
        self.assertIn('表示する感想はありません', page)
        self.assertEqual(self.request('/feedback', 'POST', {'name': '<b>', 'comment': '<script>例</script>\n感想'})[0], 201)
        for index in range(50):
            self.assertEqual(self.request('/feedback', 'POST', {'name': f'奏者{index}', 'comment': ''})[0], 201)
        _, page, _ = self.request(route, html=True)
        self.assertEqual(page.count('<article>'), 50)
        self.assertIn('before=2', page)
        self.assertNotIn('&lt;b&gt;', page)
        _, page, _ = self.request(route, query={'before': 2}, html=True)
        self.assertEqual(page.count('<article>'), 1)
        self.assertIn('&lt;b&gt;', page)
        self.assertIn('&lt;script&gt;例&lt;/script&gt;\n感想', page)
        self.assertNotIn('<script>', page)

    def test_trial_promotion_idempotency_and_losses(self):
        self.profile('挑戦者')
        battle = self.trial_history()
        saved = self.save(battle)
        self.assertEqual(self.save(battle), saved)
        promoted = self.profile('挑戦者')
        self.assertEqual(promoted['grade'], 1)
        self.save(self.trial_history(played='2026-11-04T00:00:00Z'))
        self.assertEqual(self.profile('挑戦者')['acquiredAt'], promoted['acquiredAt'])
        self.save(self.trial_history(start=1, winner=1, played='2026-11-05T00:00:00Z'))
        self.assertEqual(self.profile('挑戦者')['grade'], 1)
        self.save(self.trial_history(start=1, winner=-1, played='2026-11-06T00:00:00Z'))
        self.assertEqual(self.profile('挑戦者')['grade'], 1)
        items = self.request('/battle-histories', query={'player': '挑戦者'})[1]['items']
        self.assertEqual(len(items), 4)
        self.assertEqual(self.request('/battle-histories', query={'player': '別人'})[1]['items'], [])
        self.assertEqual(self.request('/players', query={'grade': 0})[1]['items'], [])

    def test_same_name_merges_earliest_creation_without_changing_promoted_acquisition(self):
        self.profile('同名', createdAt='2026-11-02T00:00:00Z')
        merged = self.profile('同名', createdAt='2026-11-01T00:00:00Z')
        self.assertEqual(merged['createdAt'], '2026-11-01T00:00:00.000000Z')
        self.assertEqual(merged['acquiredAt'], merged['createdAt'])
        self.save(self.trial_history(name='同名', played='2026-11-03T00:00:00Z'))
        promoted = self.profile('同名', createdAt='2026-10-31T00:00:00Z')
        self.assertEqual(promoted['createdAt'], '2026-10-31T00:00:00.000000Z')
        self.assertEqual(promoted['acquiredAt'], '2026-11-03T00:00:00.000000Z')
        self.assertEqual(promoted['grade'], 1)

    def test_offline_results_merge_without_demotion_and_preserve_first_acquisition(self):
        self.profile('挑戦者')
        self.save(self.trial_history(start=2))
        self.save(self.trial_history(start=0, played='2026-11-02T00:00:00Z'))
        self.assertEqual(self.profile('挑戦者')['grade'], 3)
        self.save(self.trial_history(start=4, played='2026-11-04T00:00:00Z'))
        acquired = self.profile('挑戦者')['acquiredAt']
        self.save(self.trial_history(start=5, played='2026-11-05T00:00:00Z'))
        self.assertEqual(self.profile('挑戦者')['acquiredAt'], acquired)
        self.assertEqual(self.profile('挑戦者')['grade'], 5)
        self.assertEqual(self.profile('挑戦者')['lastTrialOpponent'], 1)
        self.assertEqual(self.request('/players', query={'grade': 3})[1]['items'], [])

    def test_grade_ranking_and_player_history_pagination(self):
        for i in range(51):
            self.profile(f'参加者{i:02}')
        page = self.request('/players', query={'grade': 0})[1]
        self.assertEqual([p['name'] for p in page['items']], [f'参加者{i:02}' for i in range(50)])
        next_page = self.request('/players', query={'grade': 0, 'cursor': page['nextCursor']})[1]
        self.assertEqual([p['name'] for p in next_page['items']], ['参加者50'])
        self.assertEqual(next_page['nextCursor'], '')
        for _ in range(51):
            self.save(self.trial_history(winner=1))
        page = self.request('/battle-histories', query={'player': '挑戦者'})[1]
        self.assertEqual(len(page['items']), 50)
        next_page = self.request('/battle-histories', query={'player': '挑戦者', 'cursor': page['nextCursor']})[1]
        self.assertEqual(len(next_page['items']), 1)
        self.assertEqual(next_page['nextCursor'], '')
        self.assertNotIn(next_page['items'][0]['id'], [item['id'] for item in page['items']])

    def test_invalid_trial_metadata_does_not_create_players_or_history(self):
        variants = []
        wrong_level = self.trial_history()
        wrong_level['participants'][1]['cpuLevel'] = 5
        variants.append(wrong_level)
        wrong_kind = self.trial_history()
        wrong_kind['participants'][1]['isCpu'] = False
        variants.append(wrong_kind)
        wrong_start = self.trial_history()
        wrong_start['trialStartGrade'] = -1
        variants.append(wrong_start)
        wrong_target = self.trial_history()
        wrong_target['trialTargetGrade'] = 5
        variants.append(wrong_target)
        for body in variants:
            self.assertEqual(self.request('/battle-histories', 'POST', body)[0], 400)
        self.assertEqual(self.request('/players', query={'grade': 0})[1]['items'], [])
        self.assertEqual(self.request('/battle-histories')[1]['items'], [])

    def test_trial_opponents_and_levels_follow_client_configuration(self):
        body = self.trial_history(start=1)
        body['participants'][1]['cpuLevel'] = 1
        body['strikerIds'][1] = body['replayPayload']['strikerIds'][1] = 4
        self.save(body)
        self.assertEqual(self.profile('挑戦者')['grade'], 2)

    def test_player_history_uses_completion_time_with_stable_pagination(self):
        older = self.save(self.trial_history(winner=1, played='2026-11-02T23:59:59.9999999Z'))
        tied = [self.save(self.trial_history(winner=1, played='2026-11-03T09:00:00+09:00')) for _ in range(51)]
        newest = self.save(self.trial_history(winner=1, played='2026-11-03T00:00:01Z'))
        delayed = self.save(self.trial_history(winner=1, played='2026-11-01T00:00:00Z'))
        page = self.request('/battle-histories', query={'player': '挑戦者'})[1]
        more = self.request('/battle-histories', query={'player': '挑戦者', 'cursor': page['nextCursor']})[1]
        self.assertEqual([item['id'] for item in page['items'] + more['items']], [newest] + tied[::-1] + [older, delayed])
        self.assertEqual(more['nextCursor'], '')
        self.assertEqual(self.request('/battle-histories', query={'player': '挑戦者', 'cursor': '123'})[0], 400)

    def test_existing_history_schema_migrates_completion_order(self):
        newest = self.save(self.trial_history(winner=1, played='2026-11-05T00:00:00Z'))
        oldest = self.save(self.trial_history(winner=1, played='2026-11-01T09:00:00+09:00'))
        self.stop_server()
        with sqlite3.connect(self.root / 'beat-striker-data' / 'history.sqlite3') as db:
            db.execute('DROP INDEX battle_histories_played_order')
            db.execute('ALTER TABLE battle_histories RENAME TO migrated_histories')
            db.execute('CREATE TABLE battle_histories (sequence INTEGER PRIMARY KEY AUTOINCREMENT, id TEXT NOT NULL UNIQUE, summary TEXT NOT NULL, detail TEXT NOT NULL)')
            db.execute('INSERT INTO battle_histories SELECT sequence, id, summary, detail FROM migrated_histories')
            db.execute('DROP TABLE migrated_histories')
        self.start_server()
        page = self.request('/battle-histories', query={'player': '挑戦者'})[1]
        self.assertEqual([item['id'] for item in page['items']], [newest, oldest])
        self.assertEqual(self.request('/battle-histories/' + oldest)[0], 200)

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

    def test_empty_replay_is_unavailable_including_old_mock_records(self):
        body = history()
        body["replayPayload"]["rounds"] = []
        body["appVersion"] = body["replayPayload"]["appVersion"] = "mock-display-only"
        record_id = self.save(body)
        self.assertFalse(self.request("/battle-histories")[1]["items"][0]["hasReplay"])
        db_path = self.root / "beat-striker-data" / "history.sqlite3"
        with sqlite3.connect(db_path) as db:
            summary = json.loads(db.execute("SELECT summary FROM battle_histories WHERE id = ?", (record_id,)).fetchone()[0])
            summary["hasReplay"] = True
            db.execute("UPDATE battle_histories SET summary = ? WHERE id = ?", (json.dumps(summary), record_id))
        self.assertFalse(self.request("/battle-histories")[1]["items"][0]["hasReplay"])

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
