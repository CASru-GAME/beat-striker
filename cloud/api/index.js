import express from "express";
import { FieldValue, Firestore } from "@google-cloud/firestore";

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

app.post("/duel/prompts", async (req, res) => {
  const { duelSessionId, scene, state } = req.body;
  if (!duelSessionId || typeof duelSessionId !== "string") {
    return res.status(400).json({ error: "duelSessionId is required" });
  }

  const now = new Date();
  await db.collection("presence").doc(duelSessionId).set({
    scene: typeof scene === "string" ? scene : "",
    state: typeof state === "string" ? state : "Available",
    updatedAt: now,
    expiresAt: addSeconds(now, 120)
  }, { merge: true });

  const reservation = await findActiveReservationForSession(duelSessionId, now);
  if (reservation) {
    const opponentSessionId = reservation.playerSessionIds.find(id => id !== duelSessionId);
    let opponentPresence = null;
    if (opponentSessionId) {
      const oppDoc = await db.collection("presence").doc(opponentSessionId).get();
      if (oppDoc.exists && !isExpired(oppDoc.data().expiresAt, now)) {
        opponentPresence = toPresenceDto(oppDoc);
      }
    }
    return res.json({ reservation, opponentPresence });
  }

  const incomingInvite = await findIncomingInvite(duelSessionId, now);
  if (incomingInvite) {
    return res.json({ incomingInvite });
  }

  const candidate = await findDuelCandidate(duelSessionId, now);
  res.json({ candidate });
});

app.post("/invites", async (req, res) => {
  const { fromSessionId, toSessionId } = req.body;
  if (!fromSessionId || !toSessionId || fromSessionId === toSessionId) {
    return res.status(400).json({ error: "fromSessionId and toSessionId are required" });
  }

  const now = new Date();
  const toPresence = await db.collection("presence").doc(toSessionId).get();
  if (!toPresence.exists || isExpired(toPresence.data().expiresAt, now) || toPresence.data().state !== "Available") {
    return res.status(404).json({ error: "target presence not active" });
  }

  const existingReservation = await findActiveReservationForSession(toSessionId, now);
  if (existingReservation) {
    return res.status(409).json({ error: "reserved" });
  }

  const inviteRef = await db.collection("invites").add({
    fromSessionId,
    toSessionId,
    status: "pending",
    createdAt: now,
    expiresAt: addSeconds(now, 60)
  });

  const invite = await inviteRef.get();
  res.json({ invite: toInviteDto(invite) });
});

app.post("/invites/:id/accept", async (req, res) => {
  const { duelSessionId } = req.body;
  if (!duelSessionId || typeof duelSessionId !== "string") {
    return res.status(400).json({ error: "duelSessionId is required" });
  }

  const now = new Date();
  try {
    const result = await db.runTransaction(async (transaction) => {
      const inviteRef = db.collection("invites").doc(req.params.id);
      const inviteDoc = await transaction.get(inviteRef);
      if (!inviteDoc.exists) {
        return { status: 404, body: { error: "invite not found" } };
      }

      const invite = inviteDoc.data();
      if (invite.toSessionId !== duelSessionId) {
        return { status: 403, body: { error: "not invite target" } };
      }

      if (isExpired(invite.expiresAt, now)) {
        transaction.update(inviteRef, { status: "expired" });
        return { status: 410, body: { error: "invite expired" } };
      }

      if (invite.status === "accepted" && invite.reservationId) {
        const reservationDoc = await transaction.get(db.collection("reservations").doc(invite.reservationId));
        return { status: 200, body: { invite: toInviteDto(inviteDoc, invite), reservation: toReservationDto(reservationDoc) } };
      }

      if (invite.status !== "pending") {
        return { status: 409, body: { error: `invite is ${invite.status}` } };
      }

      const reservationRef = db.collection("reservations").doc();
      const reservation = {
        playerSessionIds: [invite.fromSessionId, invite.toSessionId],
        inviteId: inviteDoc.id,
        status: "reserved",
        createdAt: now,
        expiresAt: addSeconds(now, 180),
        consumedBy: []
      };

      transaction.set(reservationRef, reservation);
      transaction.update(inviteRef, {
        status: "accepted",
        reservationId: reservationRef.id,
        acceptedAt: now
      });

      return {
        status: 200,
        body: {
          invite: { id: inviteDoc.id, ...invite, status: "accepted", reservationId: reservationRef.id },
          reservation: { id: reservationRef.id, ...reservation }
        }
      };
    });

    res.status(result.status).json(result.body);
  }
  catch (error) {
    res.status(500).json({ error: error.message });
  }
});

app.post("/invites/:id/reject", async (req, res) => {
  await finishInvite(req, res, "rejected");
});

app.post("/invites/:id/cancel", async (req, res) => {
  await finishInvite(req, res, "canceled");
});

app.post("/reservations/:id/consume", async (req, res) => {
  const { duelSessionId } = req.body;
  if (!duelSessionId || typeof duelSessionId !== "string") {
    return res.status(400).json({ error: "duelSessionId is required" });
  }

  const now = new Date();
  const reservationRef = db.collection("reservations").doc(req.params.id);
  const reservationDoc = await reservationRef.get();
  if (!reservationDoc.exists) {
    return res.status(404).json({ error: "reservation not found" });
  }

  const reservation = reservationDoc.data();
  if (reservation.status !== "reserved" || isExpired(reservation.expiresAt, now)) {
    return res.status(410).json({ error: "reservation expired" });
  }

  if (!Array.isArray(reservation.playerSessionIds) || !reservation.playerSessionIds.includes(duelSessionId)) {
    return res.status(403).json({ error: "not reservation player" });
  }

  await reservationRef.update({
    consumedBy: FieldValue.arrayUnion(duelSessionId),
    updatedAt: now
  });

  const updated = await reservationRef.get();
  res.json({ reservation: toReservationDto(updated) });
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

async function finishInvite(req, res, status) {
  const now = new Date();
  const inviteRef = db.collection("invites").doc(req.params.id);
  const inviteDoc = await inviteRef.get();
  if (!inviteDoc.exists) {
    return res.status(404).json({ error: "invite not found" });
  }

  const invite = inviteDoc.data();
  if (isExpired(invite.expiresAt, now)) {
    if (invite.status === "pending") {
      await inviteRef.update({ status: "expired" });
    }
    return res.json({ ok: true, expired: true });
  }

  if (invite.status === "pending") {
    await inviteRef.update({ status, updatedAt: now });
  }

  res.json({ ok: true });
}

async function findIncomingInvite(duelSessionId, now) {
  const snapshot = await db.collection("invites")
    .where("toSessionId", "==", duelSessionId)
    .where("status", "==", "pending")
    .where("expiresAt", ">", now)
    .orderBy("expiresAt", "asc")
    .limit(1)
    .get();

  if (snapshot.empty) return null;
  return toInviteDto(snapshot.docs[0]);
}

async function findDuelCandidate(duelSessionId, now) {
  const snapshot = await db.collection("presence")
    .where("state", "==", "Available")
    .where("expiresAt", ">", now)
    .limit(20)
    .get();

  for (const doc of snapshot.docs) {
    if (doc.id === duelSessionId) continue;
    const reservation = await findActiveReservationForSession(doc.id, now);
    if (!reservation) {
      return toPresenceDto(doc);
    }
  }

  return null;
}

async function findActiveReservationForSession(duelSessionId, now) {
  const snapshot = await db.collection("reservations")
    .where("playerSessionIds", "array-contains", duelSessionId)
    .where("status", "==", "reserved")
    .where("expiresAt", ">", now)
    .limit(1)
    .get();

  if (snapshot.empty) return null;
  return toReservationDto(snapshot.docs[0]);
}

function toInviteDto(doc, data = null) {
  const value = data || doc.data();
  return {
    id: doc.id,
    fromSessionId: value.fromSessionId || "",
    toSessionId: value.toSessionId || "",
    status: value.status || ""
  };
}

function toPresenceDto(doc) {
  const value = doc.data();
  return {
    duelSessionId: doc.id,
    scene: value.scene || "",
    state: value.state || ""
  };
}

function toReservationDto(doc) {
  const value = doc.data();
  return {
    id: doc.id,
    inviteId: value.inviteId || "",
    status: value.status || "",
    playerSessionIds: Array.isArray(value.playerSessionIds) ? value.playerSessionIds : [],
    expiresAt: value.expiresAt ? (typeof value.expiresAt.toDate === "function" ? value.expiresAt.toDate().toISOString() : new Date(value.expiresAt).toISOString()) : ""
  };
}

function addSeconds(date, seconds) {
  return new Date(date.getTime() + seconds * 1000);
}

function isExpired(value, now) {
  if (!value) return true;
  const date = typeof value.toDate === "function" ? value.toDate() : new Date(value);
  return date.getTime() <= now.getTime();
}

const port = process.env.PORT || 8080;
app.listen(port, () => {
  console.log(`listening on ${port}`);
});
