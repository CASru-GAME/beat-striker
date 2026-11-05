<?php
declare(strict_types=1);

// Upload this file to www/beat-striker/. Persistent data stays outside www/.
const MAX_REQUEST_BYTES = 25 * 1024 * 1024;
const JSON_FLAGS = JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_THROW_ON_ERROR;

ini_set('display_errors', '0');
date_default_timezone_set('UTC');
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');
header('X-Content-Type-Options: nosniff');
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Methods: GET, POST, OPTIONS');
header('Access-Control-Allow-Headers: Content-Type, Authorization');

function respond(int $status, array $body): void
{
    http_response_code($status);
    echo json_encode($body, JSON_FLAGS);
    exit;
}

function database(): PDO
{
    static $connection;
    if ($connection instanceof PDO) return $connection;
    // With the documented FTP layout this resolves to ~/beat-striker-data/.
    $directory = dirname(__DIR__, 2) . '/beat-striker-data';
    if (!is_dir($directory) && !mkdir($directory, 0700, true) && !is_dir($directory)) {
        throw new RuntimeException('Cannot create the storage directory');
    }

    $storage = realpath($directory);
    $documentRoot = realpath($_SERVER['DOCUMENT_ROOT'] ?? '');
    if ($storage === false || $documentRoot === false) {
        throw new RuntimeException('Cannot resolve storage/document root');
    }
    $storage = str_replace('\\', '/', $storage);
    $documentRoot = rtrim(str_replace('\\', '/', $documentRoot), '/');
    if (strncasecmp($storage . '/', $documentRoot . '/', strlen($documentRoot) + 1) === 0) {
        throw new RuntimeException('Storage must be outside the public document root');
    }

    umask(0077);
    $db = new PDO('sqlite:' . $storage . '/history.sqlite3', null, null, [
        PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
        PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
        PDO::ATTR_TIMEOUT => 10,
    ]);
    $db->exec('PRAGMA busy_timeout = 10000');
    $db->exec('CREATE TABLE IF NOT EXISTS battle_histories (
        sequence INTEGER PRIMARY KEY AUTOINCREMENT,
        id TEXT NOT NULL UNIQUE,
        summary TEXT NOT NULL,
        detail TEXT NOT NULL,
        played_at TEXT NOT NULL
    )');
    if (!in_array('played_at', array_column($db->query('PRAGMA table_info(battle_histories)')->fetchAll(), 'name'), true)) {
        $db->exec('BEGIN IMMEDIATE');
        // Recheck after obtaining the write lock when multiple requests migrate together.
        if (!in_array('played_at', array_column($db->query('PRAGMA table_info(battle_histories)')->fetchAll(), 'name'), true)) {
            $db->exec('ALTER TABLE battle_histories ADD COLUMN played_at TEXT NOT NULL DEFAULT ""');
            $update = $db->prepare('UPDATE battle_histories SET played_at = ? WHERE id = ?');
            foreach ($db->query('SELECT id, summary FROM battle_histories')->fetchAll() as $row) {
                $summary = json_decode($row['summary'], false, 128, JSON_THROW_ON_ERROR);
                $update->execute([timestamp($summary->playedAt), $row['id']]);
            }
        }
        $db->exec('COMMIT');
    }
    $db->exec('CREATE INDEX IF NOT EXISTS battle_histories_played_order ON battle_histories(played_at, sequence)');
    $db->exec('CREATE TABLE IF NOT EXISTS scores (
        id TEXT PRIMARY KEY,
        name TEXT NOT NULL,
        score REAL NOT NULL,
        created_at TEXT NOT NULL
    )');
    $db->exec('CREATE TABLE IF NOT EXISTS players (
        sequence INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL UNIQUE COLLATE BINARY,
        grade INTEGER NOT NULL DEFAULT 0, acquired_at TEXT NOT NULL, created_at TEXT NOT NULL,
        last_opponent INTEGER NOT NULL DEFAULT -1, last_trial_at TEXT NOT NULL DEFAULT ""
    )');
    $db->exec('CREATE INDEX IF NOT EXISTS players_grade_order ON players(grade, acquired_at, sequence)');
    $db->exec('CREATE TABLE IF NOT EXISTS battle_requests (battle_id TEXT PRIMARY KEY, history_id TEXT NOT NULL UNIQUE)');
    $db->exec('CREATE TABLE IF NOT EXISTS history_players (history_id TEXT NOT NULL, player_name TEXT NOT NULL, PRIMARY KEY(history_id, player_name))');
    $db->exec('CREATE INDEX IF NOT EXISTS history_players_name ON history_players(player_name, history_id)');
    $connection = $db;
    return $connection;
}

function playerName($value): string
{
    if (!is_string($value) || !preg_match('//u', $value)) respond(400, ['error' => 'name must be UTF-8 text']);
    $name = preg_replace('/(*UCP)^\s+|\s+$/u', '', $value);
    if ($name === '' || preg_match('/\p{Cc}/u', $name) || preg_match_all('/\X/u', $name) > 10) {
        respond(400, ['error' => 'name must contain 1 to 10 characters']);
    }
    return $name;
}

function timestamp(string $value): string
{
    if (!preg_match('/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$/D', $value)) respond(400, ['error' => 'invalid timestamp']);
    // PHP accepts at most six fractional digits; Unity emits seven.
    $value = preg_replace('/(\.\d{6})\d(?=Z|[+-])/', '$1', $value);
    try { return (new DateTimeImmutable($value))->setTimezone(new DateTimeZone('UTC'))->format('Y-m-d\TH:i:s.u\Z'); }
    catch (Exception $exception) { respond(400, ['error' => 'invalid timestamp']); }
}

function ensurePlayer(PDO $db, string $name, string $createdAt): void
{
    $statement = $db->prepare('INSERT INTO players(name, acquired_at, created_at) VALUES(?, ?, ?)
        ON CONFLICT(name) DO UPDATE SET
            acquired_at = CASE WHEN players.grade = 0 THEN MIN(players.acquired_at, excluded.created_at) ELSE players.acquired_at END,
            created_at = MIN(players.created_at, excluded.created_at)');
    $statement->execute([$name, $createdAt, $createdAt]);
}

function profileRow(array $row): array
{
    return ['name' => $row['name'], 'grade' => (int)$row['grade'], 'acquiredAt' => $row['acquired_at'], 'createdAt' => $row['created_at'],
        'lastTrialOpponent' => (int)$row['last_opponent'], 'lastTrialAt' => $row['last_trial_at']];
}

function recordPlayers(PDO $db, stdClass $body): void
{
    if (!isset($body->participants)) return;
    foreach ($body->participants as $player) {
        if ($player->isCpu) continue;
        ensurePlayer($db, $player->name, timestamp($body->playedAt));
        $db->prepare('INSERT OR IGNORE INTO history_players(history_id, player_name) VALUES(?, ?)')->execute([$body->id, $player->name]);
    }
    if (($body->mode ?? '') !== 'Trial') return;
    $name = $body->participants[0]->name;
    $time = timestamp($body->playedAt);
    if ($body->winnerPlayerId === 0 && $body->trialTargetGrade >= 1) {
        $target = $body->trialTargetGrade;
        $db->prepare('UPDATE players SET grade = ?, acquired_at = ? WHERE name = ? AND grade < ?')->execute([$target, $time, $name, $target]);
        $db->prepare('UPDATE players SET acquired_at = ? WHERE name = ? AND grade = ? AND acquired_at > ?')->execute([$time, $name, $target, $time]);
    }
    $db->prepare('UPDATE players SET last_opponent = ?, last_trial_at = ? WHERE name = ? AND last_trial_at <= ?')
        ->execute([$body->strikerIds[1], $time, $name, $time]);
}

function readBody(): stdClass
{
    $contentType = strtolower(trim(explode(';', $_SERVER['CONTENT_TYPE'] ?? '')[0]));
    if ($contentType !== 'application/json') {
        respond(415, ['error' => 'Content-Type must be application/json']);
    }
    if ((int) ($_SERVER['CONTENT_LENGTH'] ?? 0) > MAX_REQUEST_BYTES) {
        respond(413, ['error' => 'request body exceeds 25 MiB']);
    }
    $raw = file_get_contents('php://input', false, null, 0, MAX_REQUEST_BYTES + 1);
    if ($raw === false) {
        throw new RuntimeException('Cannot read request body');
    }
    if (strlen($raw) > MAX_REQUEST_BYTES) {
        respond(413, ['error' => 'request body exceeds 25 MiB']);
    }
    try {
        $body = json_decode($raw, false, 128, JSON_THROW_ON_ERROR);
    } catch (JsonException $exception) {
        respond(400, ['error' => 'invalid JSON request body']);
    }
    if (!$body instanceof stdClass) {
        respond(400, ['error' => 'request body must be an object']);
    }
    return $body;
}

function validText($value, int $maxBytes, bool $allowEmpty = false): bool
{
    return is_string($value) && strlen($value) <= $maxBytes && ($allowEmpty || trim($value) !== '');
}

function validPair($value, bool $strings): bool
{
    return is_array($value) && count($value) === 2
        && ($strings
            ? validText($value[0], 512, true) && validText($value[1], 512, true)
            : is_int($value[0]) && is_int($value[1]));
}

function validateHistory(stdClass $body): void
{
    if (isset($body->participants)) {
        if (!is_array($body->participants) || count($body->participants) !== 2
            || !is_string($body->battleId ?? null) || !preg_match('/^[a-f0-9]{32}$/D', $body->battleId)
            || !in_array($body->mode ?? '', ['Local', 'Trial', 'Online'], true)) respond(400, ['error' => 'invalid battle participants or identity']);
        foreach ($body->participants as $i => $player) {
            if (!$player instanceof stdClass || !is_bool($player->isCpu ?? null)
                || !is_int($player->grade ?? null) || $player->grade < 0 || $player->grade > 5) respond(400, ['error' => 'invalid player identity']);
            $name = playerName($player->name ?? null);
            if ($name !== $player->name || ($body->playerNames[$i] ?? null) !== $name) respond(400, ['error' => 'participant names do not match']);
            if ($player->isCpu && (!is_int($player->cpuLevel ?? null) || $player->cpuLevel < 1 || $player->cpuLevel > 4)) respond(400, ['error' => 'invalid CPU level']);
        }
        if ($body->mode === 'Trial') {
            $start = $body->trialStartGrade ?? null;
            if (!is_int($start) || $start < 0 || $start > 5 || ($body->trialTargetGrade ?? null) !== ($start < 5 ? $start + 1 : -1)
                || $body->participants[0]->isCpu || !$body->participants[1]->isCpu || !validPair($body->strikerIds ?? null, false)) respond(400, ['error' => 'invalid trial progress']);
            // Opponent and difficulty rules belong to the game's editable TrialSetting.
        }
    }
    if (!validPair($body->playerNames ?? null, true)
        || !validPair($body->strikerNames ?? null, true)) {
        respond(400, ['error' => 'playerNames and strikerNames must contain two strings']);
    }
    foreach (['stage', 'musicId', 'appVersion'] as $field) {
        if (!validText($body->$field ?? null, 512)) {
            respond(400, ['error' => $field . ' is required (up to 512 bytes)']);
        }
    }
    foreach (['stageName', 'musicName'] as $field) {
        if (isset($body->$field) && !validText($body->$field, 512, true)) {
            respond(400, ['error' => $field . ' must be a string (up to 512 bytes)']);
        }
    }
    if (!is_int($body->winnerPlayerId ?? null) || !in_array($body->winnerPlayerId, [-1, 0, 1], true)) {
        respond(400, ['error' => 'winnerPlayerId must be -1, 0 or 1']);
    }
    foreach (['strikerIds', 'roundWinCounts'] as $field) {
        if (isset($body->$field) && !validPair($body->$field, false)) {
            respond(400, ['error' => $field . ' must contain two integers']);
        }
    }
    if (isset($body->playedAt) && $body->playedAt !== '') {
        if (!validText($body->playedAt, 64)
            || !preg_match('/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|[+-]\d{2}:\d{2})$/D', $body->playedAt)
            || strtotime($body->playedAt) === false) {
            respond(400, ['error' => 'playedAt must be an ISO 8601 timestamp with timezone']);
        }
    }
    if (!(($body->replayPayload ?? null) instanceof stdClass)) {
        respond(400, ['error' => 'replayPayload is required']);
    }
    $replay = $body->replayPayload;
    if (($replay->schemaVersion ?? null) !== 1
        || ($replay->stage ?? null) !== $body->stage
        || ($replay->musicId ?? null) !== $body->musicId
        || ($replay->appVersion ?? null) !== $body->appVersion
        || !validPair($replay->strikerIds ?? null, false)
        || !is_array($replay->rounds ?? null)) {
        respond(400, ['error' => 'replayPayload metadata or rounds are invalid']);
    }
}

try {
    $method = $_SERVER['REQUEST_METHOD'] ?? 'GET';
    if ($method === 'OPTIONS') {
        http_response_code(204);
        exit;
    }
    // Query routing works on shared hosting without mod_rewrite or PATH_INFO.
    $route = $_GET['route'] ?? '/';
    if (!is_string($route)) {
        respond(400, ['error' => 'route must be a string']);
    }

    if ($route === '/' && $method === 'GET') {
        database()->query('SELECT 1')->fetchColumn();
        respond(200, ['ok' => true, 'service' => 'beat-striker-api', 'storage' => 'sqlite']);
    }

    if ($route === '/players') {
        $db = database();
        if ($method === 'POST') {
            $body = readBody();
            $name = playerName($body->name ?? null);
            $created = timestamp(($body->createdAt ?? '') ?: gmdate('Y-m-d\TH:i:s\Z'));
            ensurePlayer($db, $name, $created);
            $statement = $db->prepare('SELECT * FROM players WHERE name = ?');
            $statement->execute([$name]);
            respond(200, profileRow($statement->fetch()));
        }
        if ($method === 'GET') {
            $grade = filter_var($_GET['grade'] ?? null, FILTER_VALIDATE_INT);
            if ($grade === false || $grade < 0 || $grade > 5) respond(400, ['error' => 'grade must be 0 to 5']);
            $cursor = null;
            if (($_GET['cursor'] ?? '') !== '') {
                $cursor = json_decode(base64_decode((string)$_GET['cursor'], true) ?: '', true);
                if (!is_array($cursor) || count($cursor) !== 2 || !is_string($cursor[0]) || !is_int($cursor[1])) respond(400, ['error' => 'invalid ranking cursor']);
            }
            $statement = $db->prepare('SELECT * FROM players WHERE grade = ?' . ($cursor ? ' AND (acquired_at > ? OR (acquired_at = ? AND sequence > ?))' : '') . ' ORDER BY acquired_at, sequence LIMIT 51');
            $statement->execute($cursor ? [$grade, $cursor[0], $cursor[0], $cursor[1]] : [$grade]);
            $rows = $statement->fetchAll();
            $more = count($rows) > 50;
            $rows = array_slice($rows, 0, 50);
            $last = $rows ? $rows[count($rows) - 1] : null;
            respond(200, ['items' => array_map('profileRow', $rows), 'nextCursor' => $more ? base64_encode(json_encode([$last['acquired_at'], (int)$last['sequence']], JSON_FLAGS)) : '']);
        }
        header('Allow: GET, POST, OPTIONS'); respond(405, ['error' => 'method not allowed']);
    }

    if ($route === '/battle-histories') {
        if ($method === 'POST') {
            $body = readBody();
            validateHistory($body);
            $db = database();
            $db->exec('BEGIN IMMEDIATE');
            if (isset($body->battleId)) {
                $existing = $db->prepare('SELECT history_id FROM battle_requests WHERE battle_id = ?');
                $existing->execute([$body->battleId]);
                $savedId = $existing->fetchColumn();
                if ($savedId !== false) { $db->exec('COMMIT'); respond(200, ['ok' => true, 'id' => $savedId]); }
            }
            $now = gmdate('Y-m-d\TH:i:s\Z');
            $body->id = bin2hex(random_bytes(16));
            $body->createdAt = $now;
            $body->playedAt = ($body->playedAt ?? '') ?: $now;
            $summary = [
                'id' => $body->id,
                'playerNames' => $body->playerNames,
                'stage' => $body->stage,
                'musicId' => $body->musicId,
                'musicName' => $body->musicName ?? '',
                'strikerNames' => $body->strikerNames,
                'winnerPlayerId' => $body->winnerPlayerId,
                'playedAt' => $body->playedAt,
                'appVersion' => $body->appVersion,
                'hasReplay' => count($body->replayPayload->rounds) > 0,
            ];
            $statement = database()->prepare('INSERT INTO battle_histories (id, summary, detail, played_at) VALUES (?, ?, ?, ?)');
            $statement->execute([$body->id, json_encode($summary, JSON_FLAGS), json_encode($body, JSON_FLAGS), timestamp($body->playedAt)]);
            if (isset($body->battleId)) $db->prepare('INSERT INTO battle_requests(battle_id, history_id) VALUES(?, ?)')->execute([$body->battleId, $body->id]);
            recordPlayers($db, $body);
            $db->exec('COMMIT');
            respond(200, ['ok' => true, 'id' => $body->id]);
        }
        if ($method === 'GET') {
            if (isset($_GET['player'])) {
                $name = playerName($_GET['player']);
                $cursor = null;
                if (($_GET['cursor'] ?? '') !== '') {
                    $cursor = json_decode(base64_decode((string)$_GET['cursor'], true) ?: '', true);
                    if (!is_array($cursor) || count($cursor) !== 2 || !is_string($cursor[0]) || !is_int($cursor[1]) || $cursor[1] < 1) respond(400, ['error' => 'invalid history cursor']);
                }
                $statement = database()->prepare('SELECT b.sequence, b.played_at, b.summary FROM battle_histories b JOIN history_players p ON p.history_id = b.id WHERE p.player_name = ?'
                    . ($cursor ? ' AND (b.played_at < ? OR (b.played_at = ? AND b.sequence < ?))' : '') . ' ORDER BY b.played_at DESC, b.sequence DESC LIMIT 51');
                $statement->execute($cursor ? [$name, $cursor[0], $cursor[0], $cursor[1]] : [$name]);
                $rows = $statement->fetchAll(); $more = count($rows) > 50; $rows = array_slice($rows, 0, 50);
                $last = $rows ? $rows[count($rows) - 1] : null;
                respond(200, ['items' => array_map(fn($row) => json_decode($row['summary'], false, 128, JSON_THROW_ON_ERROR), $rows),
                    'nextCursor' => $more ? base64_encode(json_encode([$last['played_at'], (int)$last['sequence']], JSON_FLAGS)) : '']);
            }
            $limit = filter_var($_GET['limit'] ?? null, FILTER_VALIDATE_INT);
            $limit = $limit !== false && $limit > 0 ? min($limit, 100) : 50;
            $statement = database()->prepare('SELECT summary FROM battle_histories ORDER BY sequence DESC LIMIT ?');
            $statement->bindValue(1, $limit, PDO::PARAM_INT);
            $statement->execute();
            $items = [];
            while ($row = $statement->fetch()) {
                $summary = json_decode($row['summary'], false, 128, JSON_THROW_ON_ERROR);
                // Older display-only sample records were saved with hasReplay=true.
                if (($summary->appVersion ?? '') === 'mock-display-only') {
                    $summary->hasReplay = false;
                }
                $items[] = $summary;
            }
            respond(200, ['items' => $items]);
        }
        header('Allow: GET, POST, OPTIONS');
        respond(405, ['error' => 'method not allowed']);
    }

    if (preg_match('#^/battle-histories/([a-f0-9]{32})$#D', $route, $matches)) {
        if ($method !== 'GET') {
            header('Allow: GET, OPTIONS');
            respond(405, ['error' => 'method not allowed']);
        }
        $statement = database()->prepare('SELECT detail FROM battle_histories WHERE id = ?');
        $statement->execute([$matches[1]]);
        $row = $statement->fetch();
        if ($row === false) {
            respond(404, ['error' => 'battle history not found']);
        }
        respond(200, (array) json_decode($row['detail'], false, 128, JSON_THROW_ON_ERROR));
    }

    if ($route === '/scores') {
        if ($method !== 'POST') {
            header('Allow: POST, OPTIONS');
            respond(405, ['error' => 'method not allowed']);
        }
        $body = readBody();
        if (!validText($body->name ?? null, 512) || (!is_int($body->score ?? null) && !is_float($body->score ?? null))) {
            respond(400, ['error' => 'name and numeric score are required']);
        }
        $id = bin2hex(random_bytes(16));
        $statement = database()->prepare('INSERT INTO scores (id, name, score, created_at) VALUES (?, ?, ?, ?)');
        $statement->execute([$id, $body->name, $body->score, gmdate('Y-m-d\TH:i:s\Z')]);
        respond(200, ['ok' => true, 'id' => $id]);
    }

    respond(404, ['error' => 'route not found']);
} catch (Throwable $exception) {
    error_log('[beat-striker-api] ' . $exception->getMessage());
    respond(500, ['error' => 'server storage error; check PHP error log and directory permissions']);
}
