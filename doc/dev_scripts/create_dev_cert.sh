#!/usr/bin/env bash
set -euo pipefail

# This script generates a development HTTPS certificate for Kestrel.
# It relies on the .NET SDK's dev-certs tooling.
#
# Usage:
#   ./doc/dev_scripts/create_dev_cert.sh [password]
#
# The certificate will be written to ./certificates/dev-https.pfx

PASSWORD="${1:-${DEV_CERT_PASSWORD:-localdevpass}}"
CERT_DIR="$(dirname "${BASH_SOURCE[0]}")/../../certificates"
PFX_PATH="${CERT_DIR}/dev-https.pfx"
CRT_PATH="${CERT_DIR}/dev-https.crt"
KEY_PATH="${CERT_DIR}/dev-https.key"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet SDK is required but not found on PATH." >&2
  exit 1
fi

mkdir -p "${CERT_DIR}"

echo "Generating development HTTPS certificate at ${PFX_PATH}"
dotnet dev-certs https --export-path "${PFX_PATH}" --password "${PASSWORD}"

if command -v openssl >/dev/null 2>&1; then
  echo "Exporting PEM files for nginx..."
  openssl pkcs12 -in "${PFX_PATH}" -clcerts -nokeys -out "${CRT_PATH}" -passin pass:"${PASSWORD}" >/dev/null 2>&1
  openssl pkcs12 -in "${PFX_PATH}" -nocerts -out "${KEY_PATH}.tmp" -passin pass:"${PASSWORD}" -passout pass:"${PASSWORD}" >/dev/null 2>&1
  openssl rsa -in "${KEY_PATH}.tmp" -out "${KEY_PATH}" -passin pass:"${PASSWORD}" >/dev/null 2>&1
  rm -f "${KEY_PATH}.tmp"
  echo "PEM certificate exported to ${CRT_PATH}"
  echo "PEM key exported to ${KEY_PATH}"
else
  echo "openssl not found; skipping PEM export. Install openssl if you need nginx-compatible certs."
fi

echo "Remember to set DEV_CERT_PASSWORD=${PASSWORD} in your .env file."
