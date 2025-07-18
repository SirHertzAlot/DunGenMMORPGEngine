
# MMORPG Backend MVP

A production-ready, scalable, DRY, event-driven MMORPG backend designed for high concurrency, strong separation of concerns, granular region-based logic, and seamless observability.

## Architecture Overview

This project implements a multi-layered architecture:

- **API Gateway/ETL Layer**: Next.js-based API servers with validation and throttling
- **Event Bus**: Redis pub/sub for real-time operations
- **Unification Layer**: Unity ECS-based authoritative state machine
- **Persistence**: MongoDB with sharding for horizontal scalability
- **Monitoring**: Prometheus + Grafana for observability
- **Admin Dashboard**: Next.js + Shadcn UI for operations

## Quick Start

1. Install dependencies: `npm install`
2. Configure environment variables (see `.env.example`)
3. Run the development server: `npm run dev`

## Documentation

- [Architecture](./docs/architecture.md)
- [Data Flows](./docs/dataflows.md)
- [Operations](./docs/ops.md)
- [Admin Guide](./docs/admin.md)
- [Integration Testing](./docs/integration.md)

## Technology Stack

- **API Framework**: Next.js 15+
- **Validation**: Zod
- **Database**: MongoDB with Mongoose
- **Cache/Queue**: Redis
- **Monitoring**: Prometheus + Grafana
- **State Management**: Unity ECS (DOTS)
