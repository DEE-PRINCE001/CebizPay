/**
 * RFC 7807 ProblemDetails Parser & Domain Error Normalizer
 * 
 * Intercepts and transforms backend error responses matching ASP.NET Core
 * ProblemDetails and GlobalExceptionHandler into clean, structured user-facing errors.
 */

// User-friendly messages for known backend domain error codes
const DOMAIN_ERROR_MESSAGES = {
  INSUFFICIENT_FUNDS: 'Your wallet balance is insufficient to complete this transaction.',
  INVALID_PIN: 'The transaction PIN entered is incorrect. Please verify and try again.',
  PIN_LOCKED: 'Your transaction PIN has been locked due to multiple incorrect attempts. Please reset your PIN.',
  PIN_REQUIRED: 'A 4-digit transaction PIN is required to authorize this financial operation.',
  IDEMPOTENCY_CONFLICT: 'A transaction with this reference is already processing or completed. Please check your history.',
  WALLET_NOT_ACTIVE: 'Your wallet account is inactive or restricted by compliance policy.',
  CURRENCY_MISMATCH: 'The selected currency does not match the target account currency.',
  SELF_TRANSFER: 'You cannot transfer funds to your own wallet or account.',
  TRANSFER_NOT_AUTHORIZED: 'You are not authorized to perform this fund transfer.',
  COMPLIANCE_RESTRICTED: 'This operation is restricted based on your current KYC/KYB compliance tier.',
  VAS_DUPLICATE_PURCHASE: 'A duplicate recharge purchase was detected. Please verify your transaction history.',
  VAS_LIMIT_EXCEEDED: 'This recharge exceeds your daily Value-Added Services compliance limit.',
  VAS_INVALID_PRODUCT: 'The requested airtime or data plan product is currently unavailable.'
};

/**
 * Standard Normalized Error Object Structure
 * @typedef {Object} NormalizedError
 * @property {number} status - HTTP status code
 * @property {string} title - High-level error title
 * @property {string} message - User-friendly detailed message
 * @property {string|null} code - Backend domain error code (e.g. INSUFFICIENT_FUNDS)
 * @property {string|null} traceId - Distributed tracing identifier
 * @property {string|null} instance - Request path where error occurred
 * @property {Record<string, string[]>} fieldErrors - Field-level validation error map
 * @property {boolean} isNetworkError - Whether error is due to network disconnection
 * @property {boolean} isTimeout - Whether error is due to request timeout
 * @property {boolean} isAuthError - Whether error is 401 Unauthorized
 * @property {boolean} isForbidden - Whether error is 403 Forbidden
 * @property {boolean} isRateLimited - Whether error is 429 Too Many Requests
 * @property {boolean} isLocked - Whether error is 423 Locked (e.g. PIN locked)
 */

/**
 * Parses an Axios error or raw error response into a NormalizedError.
 * @param {any} error - The caught Axios or network error
 * @returns {NormalizedError}
 */
export function parseProblemDetails(error) {
  // 1. Network Disconnection / Server Unreachable
  if (!error.response && error.code === 'ERR_NETWORK') {
    return {
      status: 0,
      title: 'Network Connection Error',
      message: 'Unable to connect to the CebizPay server. Please check your internet connection and try again.',
      code: 'NETWORK_ERROR',
      traceId: null,
      instance: null,
      fieldErrors: {},
      isNetworkError: true,
      isTimeout: false,
      isAuthError: false,
      isForbidden: false,
      isRateLimited: false,
      isLocked: false
    };
  }

  // 2. Timeout Error
  if (!error.response && (error.code === 'ECONNABORTED' || error.message?.includes('timeout'))) {
    return {
      status: 408,
      title: 'Request Timed Out',
      message: 'The server took too long to respond. For financial operations, please check your transaction history before retrying.',
      code: 'TIMEOUT',
      traceId: null,
      instance: null,
      fieldErrors: {},
      isNetworkError: false,
      isTimeout: true,
      isAuthError: false,
      isForbidden: false,
      isRateLimited: false,
      isLocked: false
    };
  }

  const response = error.response || {};
  const status = response.status || 500;
  const data = response.data || {};

  // Extract RFC 7807 ProblemDetails properties
  const title = data.title || getDefaultTitleForStatus(status);
  const code = data.code || (data.extensions && data.extensions.code) || null;
  const traceId = data.traceId || (data.extensions && data.extensions.traceId) || null;
  const instance = data.instance || null;

  // Extract field-level validation errors
  let fieldErrors = {};
  if (data.errors && typeof data.errors === 'object') {
    fieldErrors = data.errors;
  } else if (data.extensions && data.extensions.errors && typeof data.extensions.errors === 'object') {
    fieldErrors = data.extensions.errors;
  }

  // Derive user-friendly display message
  let message = '';
  if (code && DOMAIN_ERROR_MESSAGES[code]) {
    message = DOMAIN_ERROR_MESSAGES[code];
  } else if (data.detail && typeof data.detail === 'string' && !data.detail.includes('System.') && !data.detail.includes('Exception')) {
    message = data.detail;
  } else if (data.message && typeof data.message === 'string') {
    message = data.message;
  } else if (Object.keys(fieldErrors).length > 0) {
    const firstField = Object.keys(fieldErrors)[0];
    const firstMsg = fieldErrors[firstField]?.[0] || 'Invalid input parameter.';
    message = `${firstField}: ${firstMsg}`;
  } else {
    message = getDefaultMessageForStatus(status);
  }

  return {
    status,
    title,
    message,
    code,
    traceId,
    instance,
    fieldErrors,
    isNetworkError: false,
    isTimeout: status === 408,
    isAuthError: status === 401,
    isForbidden: status === 403,
    isRateLimited: status === 429,
    isLocked: status === 423 || code === 'PIN_LOCKED'
  };
}

function getDefaultTitleForStatus(status) {
  switch (status) {
    case 400: return 'Invalid Request';
    case 401: return 'Authentication Required';
    case 403: return 'Access Denied';
    case 404: return 'Resource Not Found';
    case 409: return 'Conflict Detected';
    case 422: return 'Unprocessable Entity';
    case 423: return 'Account / Resource Locked';
    case 429: return 'Too Many Requests';
    case 500:
    case 502:
    case 503:
    case 504: return 'Server Error';
    default: return 'Operation Failed';
  }
}

function getDefaultMessageForStatus(status) {
  switch (status) {
    case 400: return 'One or more required parameters were missing or invalid.';
    case 401: return 'Your session has expired or authentication is required. Please sign in again.';
    case 403: return 'You do not have permission to perform this action in the current organization.';
    case 404: return 'The requested resource or record could not be found.';
    case 409: return 'A resource conflict occurred. This action may already have been processed.';
    case 422: return 'The request could not be processed due to business policy restrictions.';
    case 423: return 'This operation is locked due to multiple invalid authorization attempts.';
    case 429: return 'You have exceeded the allowed request limit. Please wait a moment before trying again.';
    case 500:
    default: return 'An unexpected server error occurred. Please try again later.';
  }
}
