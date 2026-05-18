# Changelog

All notable changes to FamilyNido are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
While the version is below `1.0.0`, minor releases may include breaking
changes; patch releases (`0.x.Y`) stay backwards compatible.

## [Unreleased]

## [0.1.0] - 2026-05-18

First public release.

FamilyNido is a self-hostable PWA for running a single family. One Docker
Compose stack = one family, no SaaS, no telemetry. Ships a shared
calendar, chores with points, weekly meal planner, school agenda, health
records, a family wall, weather, a tablet kiosk mode, and a versioned
`/api/v1/**` public API for integrations (Home Assistant works out of the
box).

Stack: .NET 10 + EF Core 10 + PostgreSQL 16 + SignalR on the backend,
Angular 21 (standalone, signals, zoneless) + Tailwind v4 on the
frontend. Auth via OIDC (tested with Dex and PocketID) or local
credentials.

See [README.md](./README.md) for the full module overview and
[deploy/README.md](./deploy/README.md) for how to pin to this version on
the home server.

[Unreleased]: https://github.com/pablitofernandez/FamilyNido/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/pablitofernandez/FamilyNido/releases/tag/v0.1.0
