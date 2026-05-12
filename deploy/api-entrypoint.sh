#!/bin/sh
# Entrypoint for the FamilyNido API container.
#
# Runs briefly as root, normalizes ownership of the writable mount points
# and drops privileges via `su-exec`. Two scenarios are supported:
#
#   1. Docker named volume (default in our docker-compose.prod.yml). The
#      mount point is created as root on first start; we chown it to the
#      `familynido` user that the image ships with (UID 100, GID 101 on
#      Alpine).
#
#   2. Host bind mount (e.g. `/home/dan/.../files:/app/data/files`). The
#      operator likely wants the files to remain owned by their host user
#      so they can browse/back them up without sudo. To support that, set
#      PUID/PGID env vars to the host user's IDs (commonly 1000:1000); the
#      script recreates the in-container `familynido` user with those IDs
#      before chowning.
set -e

CURRENT_UID=$(id -u familynido)
CURRENT_GID=$(id -g familynido)
TARGET_UID="${PUID:-$CURRENT_UID}"
TARGET_GID="${PGID:-$CURRENT_GID}"

if [ "$TARGET_UID" != "$CURRENT_UID" ] || [ "$TARGET_GID" != "$CURRENT_GID" ]; then
    # Drop the existing user/group and recreate them with the requested IDs.
    deluser familynido 2>/dev/null || true
    delgroup familynido 2>/dev/null || true
    addgroup -g "$TARGET_GID" familynido
    adduser -u "$TARGET_UID" -G familynido -h /home/familynido -D familynido

    # Re-stamp the published .NET binaries and the home dir so the new
    # familynido (different UID/GID) can read everything.
    chown -R familynido:familynido /app /home/familynido
fi

# Idempotent ownership normalization for the writable mount points. Works
# for both named volumes (start out root:root) and bind mounts (whatever
# the host owner is).
for dir in /app/data/files /home/familynido/.aspnet/DataProtection-Keys; do
    if [ -d "$dir" ]; then
        chown -R familynido:familynido "$dir"
    fi
done

exec su-exec familynido:familynido "$@"
