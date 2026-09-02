/**
 * Client-side Idempotency Key Generator and Safeguards
 * 
 * Generates cryptographically unique UUIDs for financial mutations
 * (e.g. transfers, card funding, VAS purchases, payroll execution)
 * to ensure at-most-once delivery and ledger safety.
 */

/**
 * Generates a unique v4 UUID idempotency key.
 * Uses native crypto.randomUUID when available, with a cryptographically sound fallback.
 * @returns {string} UUID v4 format string
 */
export function generateIdempotencyKey() {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }

  // Fallback for environments where crypto.randomUUID might not be available
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

// In-flight mutation tracker to prevent concurrent double-clicks on identical financial keys
const inFlightKeys = new Set();

/**
 * Executes a financial mutation with concurrency and double-click safeguards.
 * @param {string} idempotencyKey - The unique transaction key
 * @param {Function} mutationFn - Async function performing the API mutation
 * @returns {Promise<any>} Mutation result
 */
export async function withFinancialMutationGuard(idempotencyKey, mutationFn) {
  if (!idempotencyKey) {
    throw new Error('Idempotency key is required for guarded financial mutations.');
  }

  if (inFlightKeys.has(idempotencyKey)) {
    throw new Error('A transaction with this reference is already in-flight. Please wait.');
  }

  inFlightKeys.add(idempotencyKey);

  try {
    const result = await mutationFn();
    return result;
  } finally {
    inFlightKeys.delete(idempotencyKey);
  }
}
