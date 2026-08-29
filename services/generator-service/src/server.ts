import cors from "cors";
import express, { type Request, type Response } from "express";
import { generateWorldArtifact } from "./lib/worldGenerator.js";
import type { WorldGenerationRequest } from "./types.js";

const app = express();
app.use(cors());
app.use(express.json({ limit: "1mb" }));

app.get("/healthz", (_req: Request, res: Response) => {
  res.json({ status: "healthy", service: "generator-service" });
});

app.get("/api/generators/catalog", (_req: Request, res: Response) => {
  res.json([
    {
      generatorId: "world-pipeline",
      name: "World Pipeline Generator",
      description: "Deterministic world generation service built from extracted graph/runtime concepts.",
      inputMode: "yaml+parameters",
      outputMode: "world-artifact",
      requiresActivePipeline: true,
    },
  ]);
});

app.post("/api/generators/world-pipeline", (req: Request, res: Response) => {
  const artifact = generateWorldArtifact(req.body as WorldGenerationRequest);
  res.json(artifact);
});

const port = Number(process.env.PORT ?? 3000);
app.listen(port, () => {
  console.log(`[generator-service] listening on ${port}`);
});