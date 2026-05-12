# Contributing to FamilyNido

FamilyNido is a personal side project maintained in evenings and weekends.
It is scoped on purpose to be a self-hosted PWA for **a single household per
instance** — multi-tenant SaaS, commercial hosting, white-labelling and
anything whose primary value is serving someone else's customers is **out of
scope**. Not because those things are bad, but because supporting them would
break the simplicity that makes this project maintainable for one person.
The MIT license is exactly the right tool if you want to fork and go that way.

Bug reports and small, focused PRs are welcome. For non-trivial changes
please open an issue first to align on scope before you write code — I would
rather say *"this won't be merged because X"* before you spend a weekend on it
than after. PR review cadence is best-effort: this is a hobby project, so
family and work come first; expect days, sometimes weeks. If you touch the
API please add at least one integration test under `tests/FamilyNido.Tests/`;
if you touch the UI please verify the change in a browser before submitting
(`docker compose -f deploy/docker-compose.dev.yml up -d`, `dotnet watch run`
in `src/FamilyNido.Api`, `npm start` in `web/familynido-web`). Match the
existing style — the `.editorconfig` and the Angular CLI schematics already
encode it.

For security problems do **not** open a public issue. Use the *Report a
vulnerability* button on the GitHub **Security** tab — the full policy lives
in [`SECURITY.md`](./SECURITY.md). For everything else, be kind, assume good
intent and keep the discussion technical.
