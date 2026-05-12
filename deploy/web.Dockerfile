# syntax=docker/dockerfile:1.7
# FamilyNido frontend — Angular 21 PWA served by nginx.
# Multi-stage: build bundle with Node, then ship static files on nginx:alpine.

ARG NODE_VERSION=22
ARG NGINX_VERSION=1.27

# ─── Build stage ────────────────────────────────────────────────────────────
FROM node:${NODE_VERSION}-alpine AS build
WORKDIR /src

COPY web/familynido-web/package.json web/familynido-web/package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY web/familynido-web/ ./
RUN npx ng build --configuration production

# ─── Runtime stage ──────────────────────────────────────────────────────────
FROM nginx:${NGINX_VERSION}-alpine AS runtime

# Non-root nginx worker.
RUN rm -rf /usr/share/nginx/html/* /etc/nginx/conf.d/default.conf

COPY deploy/nginx/default.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist/familynido-web/browser/ /usr/share/nginx/html/

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -qO- http://localhost:8080/healthz || exit 1

CMD ["nginx", "-g", "daemon off;"]
