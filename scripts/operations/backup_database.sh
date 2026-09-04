#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# CebizPay Production Database Backup Script
# Usage: ./scripts/operations/backup_database.sh [backup_dir]
# ==============================================================================

BACKUP_DIR="${1:-./backups}"
TIMESTAMP=$(date -u +"%Y%m%d_%H%M%SZ")
BACKUP_FILE="${BACKUP_DIR}/cebizpay_backup_${TIMESTAMP}.dump"
CHECKSUM_FILE="${BACKUP_FILE}.sha256"

mkdir -p "${BACKUP_DIR}"

echo "[$(date -u)] Starting CebizPay PostgreSQL logical backup..."

if command -v docker >/dev/null 2>&1 && docker ps | grep -q cebizpay-postgres; then
  docker exec cebizpay-postgres pg_dump -U "${POSTGRES_USER:-cebizpay}" -Fc "${POSTGRES_DB:-cebizpay}" > "${BACKUP_FILE}"
else
  pg_dump -U "${POSTGRES_USER:-cebizpay}" -d "${POSTGRES_DB:-cebizpay}" -Fc -f "${BACKUP_FILE}"
fi

sha256sum "${BACKUP_FILE}" > "${CHECKSUM_FILE}"

echo "[$(date -u)] Backup completed successfully."
echo "  Artifact: ${BACKUP_FILE} ($(du -h "${BACKUP_FILE}" | cut -f1))"
echo "  Checksum: $(cat "${CHECKSUM_FILE}")"
