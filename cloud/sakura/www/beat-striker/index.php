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
        detail TEXT NOT NULL
    )');
    $db->exec('CREATE TABLE IF NOT EXISTS scores (
        id TEXT PRIMARY KEY,
        name TEXT NOT NULL,
        score REAL NOT NULL,
        created_at TEXT NOT NULL
    )');
    return $db;
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

    if ($route === '/battle-histories') {
        if ($method === 'POST') {
            $body = readBody();
            validateHistory($body);
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
            $statement = database()->prepare('INSERT INTO battle_histories (id, summary, detail) VALUES (?, ?, ?)');
            $statement->execute([$body->id, json_encode($summary, JSON_FLAGS), json_encode($body, JSON_FLAGS)]);
            respond(200, ['ok' => true, 'id' => $body->id]);
        }
        if ($method === 'GET') {
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
