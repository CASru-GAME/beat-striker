import express from "express";
import { Firestore } from "@google-cloud/firestore";

const app = express();
const db = new Firestore();

app.use(express.json());

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

const port = process.env.PORT || 8080;
app.listen(port, () => {
  console.log(`listening on ${port}`);
});