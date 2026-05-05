import express from "express";
import { Firestore } from "@google-cloud/firestore";

const app = express();
const db = new Firestore();

app.use(express.json({ limit: "25mb" }));

app.get("/", (req, res) => {
  res.json({ ok: true, service: "beat-striker-api" });
});

app.post("/scores", async (req, res) => {
  const { name, score } = req.body;

  if (!name || typeof score !== "number") {
    return res.status(400).json({ error: "name and numeric score are required" });
  }

  const doc = await db.collection("scores").add({
    name,
    score,
    createdAt: new Date()
  });

  res.json({ ok: true, id: doc.id });
});

app.post("/battle-histories", async (req, res) => {
  const validationError = validateBattleHistory(req.body);
  if (validationError) {
    return res.status(400).json({ error: validationError });
  }

  const now = new Date();
  const payload = {
    ...req.body,
    createdAt: now,
    playedAt: req.body.playedAt || now.toISOString()
  };

  const doc = await db.collection("battleHistories").add(payload);
  res.json({ ok: true, id: doc.id });
});

app.get("/battle-histories", async (req, res) => {
  const limit = clampLimit(Number.parseInt(req.query.limit, 10), 50, 100);
  const snapshot = await db.collection("battleHistories")
    .orderBy("createdAt", "desc")
    .limit(limit)
    .get();

  const items = snapshot.docs.map((doc) => {
    const data = doc.data();
    return {
      id: doc.id,
      playerNames: data.playerNames || ["ゲスト", "ゲスト"],
      stage: data.stage || "",
      musicId: data.musicId || "",
      musicName: data.musicName || "",
      strikerNames: data.strikerNames || [],
      winnerPlayerId: typeof data.winnerPlayerId === "number" ? data.winnerPlayerId : -1,
      playedAt: normalizeDateText(data.playedAt),
      appVersion: data.appVersion || "",
      hasReplay: !!data.replayPayload
    };
  });

  res.json({ items });
});

app.get("/battle-histories/:id", async (req, res) => {
  const doc = await db.collection("battleHistories").doc(req.params.id).get();
  if (!doc.exists) {
    return res.status(404).json({ error: "battle history not found" });
  }

  res.json({ id: doc.id, ...doc.data() });
});

function validateBattleHistory(body) {
  if (!body || typeof body !== "object") return "request body is required";
  if (!Array.isArray(body.playerNames) || body.playerNames.length < 2) return "playerNames[2] is required";
  if (!body.stage || typeof body.stage !== "string") return "stage is required";
  if (!body.musicId || typeof body.musicId !== "string") return "musicId is required";
  if (!Array.isArray(body.strikerNames) || body.strikerNames.length < 2) return "strikerNames[2] is required";
  if (typeof body.winnerPlayerId !== "number") return "winnerPlayerId is required";
  if (!body.appVersion || typeof body.appVersion !== "string") return "appVersion is required";
  if (!body.replayPayload || typeof body.replayPayload !== "object") return "replayPayload is required";
  return "";
}

function clampLimit(value, fallback, max) {
  if (!Number.isFinite(value) || value <= 0) return fallback;
  return Math.min(value, max);
}

function normalizeDateText(value) {
  if (!value) return "";
  if (typeof value === "string") return value;
  if (typeof value.toDate === "function") return value.toDate().toISOString();
  return String(value);
}

const port = process.env.PORT || 8080;
app.listen(port, () => {
  console.log(`listening on ${port}`);
});
