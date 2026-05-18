# Deployment

Two compose files live here, for two distinct scenarios:

| File | Purpose |
|---|---|
| `docker-compose.prod.yml` | Pulls pre-built images from GHCR. This is what runs on the home server. |
| `docker-compose.dev.yml` | Dev stack with Postgres only — auth runs on local credentials. |

Images are built by `.github/workflows/build-and-push.yml` and
`release.yml`, and published to:

- `ghcr.io/<GHCR_OWNER>/familynido-api:<tag>`
- `ghcr.io/<GHCR_OWNER>/familynido-web:<tag>`

Tags produced:

| Trigger | Tag(s) | Meaning |
|---|---|---|
| SemVer tag (`v0.1.0`) pushed | `0.1.0` + `0.1` + `latest` | Stable, released. **Use these in prod.** |
| Push to `main` | `main` + immutable `sha-<shortsha>` | Bleeding-edge dev build |
| Pull request | `pr-<number>` (overwritten per push) | Preview of an in-review change |

Pick the channel via the `IMAGE_TAG` env var (default `latest`).
The right default for a home server is `latest` — it follows tagged
releases, never moves under your feet unless a new release is cut. See
*Updating*, *Trying a PR build* and *Rolling back* below.

## First-time server setup

Prerequisites on the server (Ubuntu, Docker, Traefik on the
`${TRAEFIK_NETWORK}` network with a Let's Encrypt cert resolver).

1. **Create a Personal Access Token** on GitHub with scope `read:packages`.
   GHCR is private for private repos, so the server needs to authenticate.

2. **Log in to GHCR on the server** (one-time, the credentials are cached
   in `~/.docker/config.json`):

   ```bash
   echo <PAT> | docker login ghcr.io -u <github-username> --password-stdin
   ```

3. **Prepare the deploy folder** (e.g. `/opt/familynido/`):

   ```bash
   sudo mkdir -p /opt/familynido
   sudo chown $USER:$USER /opt/familynido
   cd /opt/familynido
   ```

   Copy `deploy/docker-compose.prod.yml` and `deploy/.env.example` from
   this repo into that folder, rename the example to `.env`, and fill it in
   with the Postgres credentials. **OIDC is optional** — see the box below.

4. **Pull and start**:

   ```bash
   docker compose -f docker-compose.prod.yml pull
   docker compose -f docker-compose.prod.yml up -d
   ```

5. Traefik picks the service up via the labels and serves it at the
   `${TRAEFIK_HOST}` you configured once Let's Encrypt issues the cert.

6. **Create the first admin.** The first time you load the site in a
   browser you'll be routed to `/setup` — a one-page form that collects
   the family name + timezone + your admin email + a password, creates
   the family and your account in one shot, and logs you straight in.
   No SQL, no OIDC needed. Once you're in, invite the rest of the family
   from **Nido → Añadir miembro**.

> **OIDC client id.** The default `OIDC_CLIENT_ID` is `familynido`. If you
> use PocketID or another upstream IdP, create a client with that id (or
> change the value here to match what your IdP exposes).

## Google Calendar credentials

The calendar module mirrors events from Google Calendar (read-only). The
backend needs an OAuth client to drive the consent flow:

1. Create a project at https://console.cloud.google.com and enable the
   **Google Calendar API**.
2. In **APIs & Services → OAuth consent screen**, configure an external app
   and add the scope `https://www.googleapis.com/auth/calendar.readonly`.
   Point the privacy/terms URLs at the pages your deployment serves under
   `/legal/privacidad.html` and `/legal/condiciones.html`. Both live in
   `web/familynido-web/public/legal/` in the repo and are deployed
   automatically with the web image.
3. In **APIs & Services → Credentials**, create an **OAuth 2.0 Client ID** of
   type *Web application*. Set the authorized redirect URI to exactly
   `https://<your-host>/api/calendar/google/callback`.
4. Copy the client id and secret into `.env`:

   ```env
   GOOGLE_CLIENT_ID=...
   GOOGLE_CLIENT_SECRET=...
   GOOGLE_OAUTH_REDIRECT_URI=https://<your-host>/api/calendar/google/callback
   ```

5. Restart the API container so the new env vars are picked up:

   ```bash
   docker compose -f docker-compose.prod.yml up -d --force-recreate api
   ```

The callback path is already proxied by the existing `/api/` block in
`deploy/nginx/default.conf`, so no extra reverse-proxy config is needed.

## Updating to the latest release

Default deploy follows `latest`, which moves only when a new SemVer
release is cut (see [CHANGELOG.md](../CHANGELOG.md) for what each
release contains). Updates are a manual pull:

```bash
cd /opt/familynido
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d
```

To pin to an exact version instead of following `latest`, set
`IMAGE_TAG=0.1.0` (or any minor channel like `0.1`) in `.env`.

### Tracking `main` between releases

If you want to deploy unreleased work from the main branch — typically
because a PR you care about just merged and you don't want to wait for
the next release — switch the channel:

```bash
IMAGE_TAG=main docker compose -f docker-compose.prod.yml pull
IMAGE_TAG=main docker compose -f docker-compose.prod.yml up -d
```

Snap back to released versions by re-running without `IMAGE_TAG`.

## Trying a PR build before merging

Every pull request also publishes a build to GHCR under the tag
`pr-<number>` (overwritten on each push to the PR branch, so it always
reflects the latest commit). To run that build on this server:

```bash
cd /opt/familynido
IMAGE_TAG=pr-14 docker compose -f docker-compose.prod.yml pull
IMAGE_TAG=pr-14 docker compose -f docker-compose.prod.yml up -d
```

Set `IMAGE_TAG` persistently by uncommenting it in `.env` if you want to
sit on a PR build for a while; otherwise the inline form is enough — when
you're done testing, run the regular `pull` + `up -d` without `IMAGE_TAG`
to snap back to `latest`.

> **Cleanup is automatic.** When the PR closes (merged or not), the
> `Clean up PR images` workflow drops the `pr-N` tag from both packages.
> If you need to remove a tag while the PR is still open (e.g. to free
> the name for a force-push), delete it manually from
> `https://github.com/<owner>?tab=packages` → *familynido-api* / *-web*
> → Versions → *pr-N* → Delete.
>
> The auto-cleanup relies on the `GHCR_DELETE_TOKEN` repo secret — a
> classic PAT with `read:packages` + `delete:packages` scope. The
> default `GITHUB_TOKEN` cannot delete container versions on its own.

## Rolling back

For released versions, the cleanest rollback is to pin to the previous
SemVer tag (immutable, won't move):

```bash
cd /opt/familynido
IMAGE_TAG=0.0.9 docker compose -f docker-compose.prod.yml pull
IMAGE_TAG=0.0.9 docker compose -f docker-compose.prod.yml up -d
```

For an unreleased commit, pin to its short SHA — those tags are
immutable too:

```bash
IMAGE_TAG=sha-a57d52e docker compose -f docker-compose.prod.yml pull
IMAGE_TAG=sha-a57d52e docker compose -f docker-compose.prod.yml up -d
```

When you're ready to come back to the regular release stream, drop
`IMAGE_TAG` and re-run the pull + up.

## Database migrations

Not yet automated. After a deploy that includes a new EF migration, run
them manually from the repo on the dev machine pointed at the prod DB, or
(TODO) add `db.Database.Migrate()` to `Program.cs` gated by config.
