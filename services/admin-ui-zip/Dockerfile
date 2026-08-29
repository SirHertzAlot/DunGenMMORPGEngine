FROM node:20-alpine AS build
WORKDIR /app

# Copy original zip frontend toolchain files unchanged.
COPY ./ported-from-zip-unmodified/build-template/src/frontend/ /app/

# Overlay original zip app source files unchanged.
COPY ./ported-from-zip-unmodified/frontend/index.html /app/index.html
COPY ./ported-from-zip-unmodified/frontend/tailwind.config.js /app/tailwind.config.js
COPY ./ported-from-zip-unmodified/frontend/public/ /app/public/
COPY ./ported-from-zip-unmodified/frontend/src/ /app/src/
COPY ./services/admin-ui-zip/shims/RuntimeSystem.tsx /app/src/pages/RuntimeSystem.tsx
COPY ./services/admin-ui-zip/shims/DiagnosticLogsPage.tsx /app/src/pages/DiagnosticLogsPage.tsx
COPY ./services/admin-ui-zip/shims/AgentPage.tsx /app/src/pages/AgentPage.tsx
COPY ./services/admin-ui-zip/shims/useInternetIdentity.tsx /app/src/hooks/useInternetIdentity.tsx
COPY ./services/admin-ui-zip/shims/backend.ts /app/src/backend.ts
COPY ./services/admin-ui-zip/shims/App.tsx /app/src/App.tsx
COPY ./services/admin-ui-zip/shims/Header.tsx /app/src/components/Header.tsx

RUN corepack enable \
    && pnpm install --no-frozen-lockfile \
    && pnpm run build:skip-bindings

FROM nginx:1.27-alpine AS runtime
COPY ./services/admin-ui-zip/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
