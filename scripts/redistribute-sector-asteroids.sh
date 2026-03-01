#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="${ROOT_DIR}/.env"

if [[ -f "${ENV_FILE}" ]]; then
  set -a
  source "${ENV_FILE}"
  set +a
fi

SECTOR_ID="${1:-}"
if [[ -z "${SECTOR_ID}" ]]; then
  echo "Usage: $0 <sector_id>"
  exit 1
fi

if ! [[ "${SECTOR_ID}" =~ ^[0-9]+$ ]]; then
  echo "Error: <sector_id> must be a positive integer"
  exit 1
fi

PGHOST="${POSTGRES_HOST:-${PGHOST:-localhost}}"
PGPORT="${POSTGRES_PORT:-${PGPORT:-5432}}"
PGUSER="${POSTGRES_USER:-${PGUSER:-belter}}"
PGDATABASE="${POSTGRES_DB:-${PGDATABASE:-belterlife}}"
PGPASSWORD="${POSTGRES_PASSWORD:-${PGPASSWORD:-changeme}}"

read -r -d '' SQL <<EOF || true
BEGIN;

WITH reroll AS (
    SELECT
        id,
        radius::double precision AS radius,
        random() * 2 * pi() AS angle,
    (150000.0 + radius::double precision) AS min_center_dist,
    GREATEST(
      0.0,
      LEAST(
        (150000.0 + radius::double precision) + 450000.0,
        (25000000.0 - radius::double precision - 3000000.0)
      ) - (150000.0 + radius::double precision)
    ) AS dist_range,
        (250.0 + random() * 1500.0) AS drift_speed
    FROM asteroids
    WHERE sector_id = ${SECTOR_ID}
      AND is_destroyed = false
),
coords AS (
    SELECT
        id,
        angle,
        (min_center_dist + random() * dist_range) AS dist,
        drift_speed
    FROM reroll
)
UPDATE asteroids a
SET
    x = round(cos(c.angle) * c.dist)::bigint,
    y = round(sin(c.angle) * c.dist)::bigint,
    velocity_x = (cos(c.angle + pi()/2) * c.drift_speed)::real,
    velocity_y = (sin(c.angle + pi()/2) * c.drift_speed)::real
FROM coords c
WHERE a.id = c.id;

COMMIT;
EOF

echo "Redistributing asteroids in sector ${SECTOR_ID} on ${PGHOST}:${PGPORT}/${PGDATABASE} as ${PGUSER}"

SQL_SINGLE_LINE="${SQL//$'\n'/ }"

printf '\\q\n' | TERM=dumb PGPASSWORD="${PGPASSWORD}" pgcli \
  -h "${PGHOST}" \
  -p "${PGPORT}" \
  -U "${PGUSER}" \
  -d "${PGDATABASE}" \
  -w \
  --less-chatty \
  --prompt '' \
  --init-command "${SQL_SINGLE_LINE}"

echo "Done."
