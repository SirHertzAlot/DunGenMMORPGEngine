What I created

- `docker-compose.yml` — compose for ScyllaDB, Redis, Postgres, RabbitMQ, Prometheus, Grafana
- `config/prometheus.yml` — minimal Prometheus config

Prerequisites (Windows):

1. Install Docker Desktop: https://www.docker.com/products/docker-desktop
   - Enable WSL2 backend if prompted.
2. Ensure `docker` and `docker-compose` are available in PowerShell.

Quick start (from repo root):

```powershell
# start services (first run will download images)
docker compose up -d

# view logs
docker compose logs -f

# stop and remove
docker compose down -v
```

Notes:
- Scylla is resource-intensive; adjust `--memory` and `--smp` in `docker-compose.yml` for low-RAM machines.
- If you prefer Podman, translate the compose file or run images individually.
- I couldn't pull images here because Docker CLI isn't installed on this machine; run the commands above locally to download images.
