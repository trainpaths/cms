# syntax=docker/dockerfile:1

# ── Stage 1: Build .NET API ─────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /source
# Restore first so the package layer is cached until the csproj changes.
COPY api-backend/api-backend.csproj api-backend/
RUN dotnet restore api-backend/api-backend.csproj
COPY api-backend/ api-backend/
# No OpenAPI generation here: the frontend package ships the committed client (CI checks drift).
RUN dotnet build api-backend/api-backend.csproj -c Release --no-restore -p:OpenApiGenerateDocumentsOnBuild=false && \
    dotnet publish api-backend/api-backend.csproj -c Release -o /app/api --no-build

# ── Stage 2: Build the playground instance (admin SPA + public site + SSR bundle) ──
# pnpm workspace: frontend/ = the @trainpaths/cms package, playground/ = the app that imports it.
FROM node:26-alpine AS frontend-build
WORKDIR /app
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY frontend/package.json frontend/
COPY playground/package.json playground/
# Node 26 ships no corepack; pin pnpm to package.json's packageManager
RUN npm install -g "$(node -p "require('./package.json').packageManager")"
RUN --mount=type=cache,id=pnpm-store,target=/pnpm-store \
    pnpm install --frozen-lockfile --store-dir /pnpm-store --filter cms-playground...
COPY frontend/ frontend/
COPY playground/ playground/
# vue-tsc -b && vite build (dist/) && vite build --ssr (dist-ssr/)
RUN pnpm --filter cms-playground build

# ── Stage 3: API runtime (the published image: no instance config, no frontend) ──
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
# Backups (Services/Backup) run pg_dump / pg_restore / psql: client 18 from the PostgreSQL apt repo (the distro's is
# older, and the client must not be older than the server). /backups = Backup:Directory, writable by the app user.
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates curl \
    && install -d /usr/share/postgresql-common/pgdg \
    && curl -fsSo /usr/share/postgresql-common/pgdg/apt.postgresql.org.asc https://www.postgresql.org/media/keys/ACCC4CF8.asc \
    && . /etc/os-release \
    && echo "deb [signed-by=/usr/share/postgresql-common/pgdg/apt.postgresql.org.asc] https://apt.postgresql.org/pub/repos/apt ${VERSION_CODENAME}-pgdg main" \
       > /etc/apt/sources.list.d/pgdg.list \
    && apt-get update && apt-get install -y --no-install-recommends postgresql-client-18 \
    && apt-get purge -y --auto-remove curl && rm -rf /var/lib/apt/lists/* \
    && install -d -o "$APP_UID" -g "$APP_UID" /backups
WORKDIR /app
COPY --from=api-build /app/api ./
# release builds pass the tag version (recorded in backup manifests)
ARG CMS_VERSION=dev
ENV Cms__Version=$CMS_VERSION
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "api-backend.dll"]

# ── Stage 4: Renderer (server-renders public pages for the API, internal only) ─
# Needs only the public.html template, the self-contained SSR bundle and the server script (no node_modules).
FROM node:26-alpine AS renderer
WORKDIR /app
RUN echo '{"type":"module"}' > package.json
COPY --from=frontend-build /app/playground/dist/public.html dist/public.html
COPY --from=frontend-build /app/playground/dist-ssr dist-ssr
COPY frontend/server/render-server.js frontend/server/template.js server/
ENV NODE_ENV=production PORT=8080
USER node
EXPOSE 8080
CMD ["node", "server/render-server.js"]

# ── Stage 5: Frontend runtime (unprivileged nginx on :8080) ──────────────────
FROM nginxinc/nginx-unprivileged:alpine AS frontend
COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=frontend-build /app/playground/dist /usr/share/nginx/html
EXPOSE 8080
