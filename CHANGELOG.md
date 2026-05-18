# Changelog

All notable changes to FamilyNido are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
While the version is below `1.0.0`, minor releases may include breaking
changes; patch releases (`0.x.Y`) stay backwards compatible.

## [Unreleased]

## [0.1.0] - 2026-05-18

First public release. FamilyNido is a self-hostable PWA for running a single
family — shared calendar, chores, meals, school agenda, health records,
a family wall, weather, a tablet kiosk mode, and a versioned `/api/v1/**`
public API for integrations (Home Assistant works out of the box).

### Highlights

- **Single-tenant by design.** One Docker Compose stack = one family. No
  multi-tenant, no SaaS, no telemetry.
- **Auth.** OIDC (tested with Dex and PocketID) or local credentials, with
  per-member roles (Admin / Adult / Child).
- **Realtime.** SignalR pushes wall updates, score changes and task
  completions across devices without polling.
- **Stack.** .NET 10 + EF Core 10 + PostgreSQL 16 on the backend,
  Angular 21 (standalone, signals, zoneless) + Tailwind v4 on the
  frontend.

### Released alongside this version

- Share-link button on each wall post (`/wall?messageId=…` deep links,
  family-scoped, with scroll + highlight on arrival).
- Paginated and searchable "Todas" tasks tab (10 per page by default,
  ILIKE search over title/description/category, state mirrored to the
  URL so refresh/share preserves the view).

See [README.md](./README.md) for the full module overview, stack details
and deployment instructions, and [deploy/README.md](./deploy/README.md)
for how to pin to this version on the home server.

[Unreleased]: https://github.com/pablitofernandez/FamilyNido/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/pablitofernandez/FamilyNido/releases/tag/v0.1.0
