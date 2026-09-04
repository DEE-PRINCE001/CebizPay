#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# CebizPay Production Database Restore & Financial Verification Script
# Usage: ./scripts/operations/restore_database.sh <backup_file> <target_db_name>
# ==============================================================================

if [ "$#" -lt 2 ]; then
  echo "Usage: $0 <backup_file> <target_db_name>"
  exit 1
fi

BACKUP_FILE="$1"
TARGET_DB="$2"
POSTGRES_USER="${POSTGRES_USER:-cebizpay}"

if [ ! -f "${BACKUP_FILE}" ]; then
  echo "Error: Backup file '${BACKUP_FILE}' not found."
  exit 1
fi

# Verify checksum if present
if [ -f "${BACKUP_FILE}.sha256" ]; then
  echo "Verifying SHA-256 checksum..."
  sha256sum -c "${BACKUP_FILE}.sha256"
fi

echo "[$(date -u)] Preparing target database '${TARGET_DB}'..."

if command -v docker >/dev/null 2>&1 && docker ps | grep -q cebizpay-postgres; then
  docker exec cebizpay-postgres dropdb -U "${POSTGRES_USER}" --if-exists "${TARGET_DB}"
  docker exec cebizpay-postgres createdb -U "${POSTGRES_USER}" "${TARGET_DB}"
  echo "[$(date -u)] Restoring data into '${TARGET_DB}'..."
  docker exec -i cebizpay-postgres pg_restore -U "${POSTGRES_USER}" -d "${TARGET_DB}" < "${BACKUP_FILE}"

  echo "[$(date -u)] Running post-restore financial integrity check..."
  docker exec cebizpay-postgres psql -U "${POSTGRES_USER}" -d "${TARGET_DB}" -c "
    SELECT 
      (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public') AS total_tables,
      (SELECT COUNT(*) FROM \"__EFMigrationsHistory\") AS migrations_count,
      (SELECT COUNT(*) FROM \"LedgerAccounts\") AS ledger_accounts_count,
      (SELECT COUNT(*) FROM \"LedgerTransactions\") AS ledger_transactions_count,
      (SELECT COUNT(*) FROM \"LedgerEntries\") AS ledger_entries_count,
      (SELECT COALESCE(SUM(\"Amount\"), 0) FROM \"LedgerEntries\" WHERE \"Direction\" = 1) AS total_debit,
      (SELECT COALESCE(SUM(\"Amount\"), 0) FROM \"LedgerEntries\" WHERE \"Direction\" = 2) AS total_credit,
      (SELECT COUNT(*) FROM \"Wallets\") AS wallets_count,
      (SELECT COALESCE(SUM(\"AvailableBalance\"), 0) FROM \"Wallets\") AS total_wallet_balance,
      (SELECT COUNT(*) FROM \"OutboxMessages\") AS outbox_count,
      (SELECT COUNT(*) FROM \"WebhookEvents\") AS webhooks_count,
      (SELECT COUNT(*) FROM \"AuditLogs\") AS audit_logs_count;
  "
else
  dropdb -U "${POSTGRES_USER}" --if-exists "${TARGET_DB}"
  createdb -U "${POSTGRES_USER}" "${TARGET_DB}"
  pg_restore -U "${POSTGRES_USER}" -d "${TARGET_DB}" "${BACKUP_FILE}"
fi

echo "[$(date -u)] Database restore and integrity verification completed successfully."
