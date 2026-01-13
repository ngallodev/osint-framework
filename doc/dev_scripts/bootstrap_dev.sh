#!/usr/bin/env bash
set -euo pipefail

# Bootstrap local development environment:
# 1. Ensure .env exists (copy from example if missing)
# 2. Generate dev HTTPS cert (if not present)
# 3. Start docker compose stack

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
ENV_FILE="${ROOT_DIR}/.env"
ENV_EXAMPLE="${ROOT_DIR}/.env.example"
CERT_FILE="${ROOT_DIR}/certificates/dev-https.pfx"

if [ ! -f "${ENV_FILE}" ]; then
  echo ".env not found, creating from .env.example"
  if [ ! -f "${ENV_EXAMPLE}" ]; then
    echo "Missing .env.example. Aborting." >&2
    exit 1
  fi
  cp "${ENV_EXAMPLE}" "${ENV_FILE}"
  echo "Update ${ENV_FILE} with the correct values before continuing."
fi

if [ ! -f "${CERT_FILE}" ]; then
  echo "Generating development certificate..."
  "${ROOT_DIR}/doc/dev_scripts/create_dev_cert.sh"
fi

echo "Starting docker compose stack..."
docker compose up --build -d

echo "Services are starting. Use 'docker compose logs -f' to monitor."
