# Changelog

All notable changes to FamilyNido are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
While the version is below `1.0.0`, minor releases may include breaking
changes; patch releases (`0.x.Y`) stay backwards compatible.

## [Unreleased]

## [0.1.2] - 2026-05-19

### Added

- **First-run setup wizard at `/setup`** for fresh self-hosted deployments.
  When the SPA detects zero users on the instance it routes the visitor to
  a one-page form that collects the family name, timezone, admin name,
  email and password — creates the four rows that a working instance
  needs (Family + admin User + linked FamilyMember + local credential) in
  a single transaction, then auto-logs the admin in so they land on the
  dashboard without a second password prompt. Anonymous, one-shot,
  refuses with 409 the moment any user already exists. Removes the
  previous catch-22 where bootstrapping required either an OIDC provider
  configured upfront or a manual SQL insert. (#20)

## [0.1.1] - 2026-05-18

### Added

- **Time format and temperature unit are now per-user preferences**, joining
  the existing per-user language picker on `/account` → *Formato de hora y
  temperatura*. Each user can independently force 12H / 24H or Celsius /
  Fahrenheit regardless of the UI language. Defaults to `Automático` —
  which honours the active i18n bundle's native conventions (en-US → 12H +
  °F, es-ES → 24H + °C) — so existing users keep what they have without
  any action. Backed by two new endpoints (`PUT /api/auth/me/time-format`,
  `PUT /api/auth/me/temperature-unit`) and surfaced in `/api/auth/me`.
- Every hour-of-day display in the app now honours both the locale's hour
  cycle and the explicit override above: dashboard widgets, calendar
  events, tablet clock + events + tasks + recent messages, the wall
  (message and comment timestamps), tasks list, school timetables,
  member detail, member agenda, weather sunrise/sunset, account API-key
  "last used", calendar sync status. (#12)

### Fixed

- All-day Google Calendar events no longer display one day early for families
  whose timezone is west of UTC (e.g. Christmas Day appearing on December 24
  in `America/New_York`). All-day dates are now interpreted in the family's
  timezone instead of falling back to UTC, and the API exposes a
  `startDate` / `endDate` (`YYYY-MM-DD`) pair the SPA renders verbatim — no
  more browser-side timezone shifting. Existing rows correct themselves on
  the next Google Calendar sync; no manual action required. (#13)

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

[Unreleased]: https://github.com/pablitofernandez/FamilyNido/compare/v0.1.2...HEAD
[0.1.2]: https://github.com/pablitofernandez/FamilyNido/releases/tag/v0.1.2
[0.1.1]: https://github.com/pablitofernandez/FamilyNido/releases/tag/v0.1.1
[0.1.0]: https://github.com/pablitofernandez/FamilyNido/releases/tag/v0.1.0
