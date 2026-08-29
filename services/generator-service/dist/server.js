import cors from "cors";
import express from "express";
import { generateWorldArtifact } from "./lib/worldGenerator.js";
const app = express();
app.use(cors());
app.use(express.json({ limit: "1mb" }));
app.get("/healthz", (_req, res) => {
    res.json({ status: "healthy", service: "generator-service" });
});
app.get("/api/generators/catalog", (_req, res) => {
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
app.post("/api/generators/world-pipeline", (req, res) => {
    const artifact = generateWorldArtifact(req.body);
    res.json(artifact);
});
const port = Number(process.env.PORT ?? 3000);
app.listen(port, () => {
    console.log(`[generator-service] listening on ${port}`);
});
