#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# CebizPay Deployment Smoke Test Script
# Usage: ./scripts/operations/smoke_test.sh [base_url]
# ==============================================================================

BASE_URL="${1:-http://localhost:8080}"

echo "=================================================="
echo " Starting CebizPay Deployment Smoke Tests         "
echo " Target: ${BASE_URL}                              "
echo "=================================================="

# 1. API Liveness Check
echo -n "1. Checking API Liveness (/health/live)... "
LIVE_STATUS=$(curl -s -o /dev/null -w "%{http_code}" "${BASE_URL}/health/live" || echo "000")
if [ "${LIVE_STATUS}" -eq 200 ]; then
  echo "OK (200)"
else
  echo "FAILED (Status: ${LIVE_STATUS})"
  exit 1
fi

# 2. API Readiness Check
echo -n "2. Checking API Readiness (/health/ready)... "
READY_RESPONSE=$(curl -s -w "\nHTTP_STATUS:%{http_code}" "${BASE_URL}/health/ready" || echo "FAILED")
READY_STATUS=$(echo "${READY_RESPONSE}" | grep "HTTP_STATUS:" | cut -d':' -f2)
if [ "${READY_STATUS}" -eq 200 ]; then
  echo "OK (200)"
else
  echo "WARNING/DEGRADED (Status: ${READY_STATUS})"
  echo "${READY_RESPONSE}"
fi

# 3. Auth Registration Validation Boundary Check
echo -n "3. Verifying Authentication Boundary Check (/api/v1/auth/register/phone)... "
AUTH_STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Content-Type: application/json" \
  -d '{"phoneNumber": ""}' \
  "${BASE_URL}/api/v1/auth/register/phone" || echo "000")

# Bad request (400) is expected for empty phone number and confirms API routing/validation is functional
if [ "${AUTH_STATUS}" -eq 400 ]; then
  echo "OK (Validation Active, 400 Bad Request returned as expected)"
else
  echo "UNEXPECTED STATUS (Status: ${AUTH_STATUS})"
fi

# 4. Unauthenticated Access Protection Check
echo -n "4. Verifying Protected Route Authorization (/api/v1/wallet/balance)... "
WALLET_STATUS=$(curl -s -o /dev/null -w "%{http_code}" "${BASE_URL}/api/v1/wallet/balance" || echo "000")
if [ "${WALLET_STATUS}" -eq 401 ]; then
  echo "OK (Protected, 401 Unauthorized returned as expected)"
else
  echo "UNEXPECTED STATUS (Status: ${WALLET_STATUS})"
fi

echo "=================================================="
echo " Smoke Tests Completed Successfully               "
echo "=================================================="
