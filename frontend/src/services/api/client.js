import axios from 'axios';
import { parseProblemDetails } from '../../utils/problemDetails';
import { generateIdempotencyKey, withFinancialMutationGuard } from '../../utils/idempotency';

/**
 * Centralized CebizPay Axios API Client
 * 
 * Features:
 * - JWT Bearer header injection
 * - Multi-tenant X-Organization-Id header injection
 * - Automated silent refresh token rotation (POST /api/v1/auth/refresh-token)
 * - RFC 7807 ProblemDetails error normalization
 * - Idempotency-Key headers for financial mutations
 * - Concurrency safeguards for financial operations
 * - Centralized 401 session expiration handling
 */

// In-memory token & tenant state (can be synchronized with AuthContext / OrgContext / localStorage)
let currentAccessToken = null;
let currentRefreshToken = null;
let currentOrgId = null;

let isRefreshing = false;
let failedQueue = [];

const unauthorizedHandlers = new Set();
const tokenUpdateHandlers = new Set();

/**
 * Updates the in-memory JWT access token.
 * @param {string|null} token
 */
export function setAuthToken(token) {
  currentAccessToken = token;
}

/**
 * Retrieves the active in-memory JWT access token.
 * @returns {string|null}
 */
export function getAuthToken() {
  return currentAccessToken;
}

/**
 * Updates the in-memory refresh token.
 * @param {string|null} token
 */
export function setRefreshToken(token) {
  currentRefreshToken = token;
}

/**
 * Retrieves the active refresh token.
 * @returns {string|null}
 */
export function getRefreshToken() {
  return currentRefreshToken;
}

/**
 * Clears all active tokens.
 */
export function clearAuthTokens() {
  currentAccessToken = null;
  currentRefreshToken = null;
}

/**
 * Updates the active Organization/Tenant context identifier.
 * @param {string|null} orgId
 */
export function setOrganizationId(orgId) {
  currentOrgId = orgId;
}

/**
 * Retrieves the active Organization/Tenant context identifier.
 * @returns {string|null}
 */
export function getOrganizationId() {
  return currentOrgId;
}

/**
 * Registers a listener for 401 Unauthorized session expirations.
 * @param {Function} handler
 * @returns {Function} Unsubscribe function
 */
export function onAuthUnauthorized(handler) {
  unauthorizedHandlers.add(handler);
  return () => unauthorizedHandlers.delete(handler);
}

/**
 * Registers a listener for token refresh updates.
 * @param {Function} handler ({ accessToken, refreshToken }) => void
 * @returns {Function} Unsubscribe function
 */
export function onTokenUpdate(handler) {
  tokenUpdateHandlers.add(handler);
  return () => tokenUpdateHandlers.delete(handler);
}

const processQueue = (error, token = null) => {
  failedQueue.forEach((prom) => {
    if (error) {
      prom.reject(error);
    } else {
      prom.resolve(token);
    }
  });
  failedQueue = [];
};

// Base Axios instance
const axiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api/v1',
  timeout: 30000,
  headers: {
    'Content-Type': 'application/json',
    'Accept': 'application/json, application/problem+json'
  }
});

// Request Interceptor
axiosInstance.interceptors.request.use(
  (config) => {
    // 1. Inject JWT Bearer Token if available
    if (currentAccessToken && !config.headers.Authorization) {
      config.headers.Authorization = `Bearer ${currentAccessToken}`;
    }

    // 2. Inject Tenant Header if available
    if (currentOrgId && !config.headers['X-Organization-Id']) {
      config.headers['X-Organization-Id'] = currentOrgId;
    }

    // 3. Inject Idempotency Headers for designated mutations
    if (config.idempotent) {
      const idempotencyKey = config.idempotencyKey || generateIdempotencyKey();
      config.headers['Idempotency-Key'] = idempotencyKey;
      config.headers['X-Idempotency-Key'] = idempotencyKey;
      config.idempotencyKey = idempotencyKey;
    }

    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// Response Interceptor
axiosInstance.interceptors.response.use(
  (response) => {
    // Direct unwrap of response data for clean caller code
    return response.data;
  },
  async (error) => {
    const originalRequest = error.config || {};
    const normalized = parseProblemDetails(error);
    error.problemDetails = normalized;

    // Check if error is 401 Unauthorized and not already retried
    const isAuthEndpoint = originalRequest.url?.includes('/auth/login') ||
                           originalRequest.url?.includes('/auth/refresh-token') ||
                           originalRequest.url?.includes('/auth/mfa/verify');

    if (normalized.isAuthError && !originalRequest._retry && !isAuthEndpoint && currentRefreshToken) {
      if (isRefreshing) {
        return new Promise((resolve, reject) => {
          failedQueue.push({ resolve, reject });
        })
          .then((token) => {
            originalRequest.headers.Authorization = `Bearer ${token}`;
            return axiosInstance(originalRequest);
          })
          .catch((err) => {
            return Promise.reject(err);
          });
      }

      originalRequest._retry = true;
      isRefreshing = true;

      try {
        // Exchange refresh token for new access token & rotated refresh token
        const refreshResponse = await axios.post(
          `${axiosInstance.defaults.baseURL}/auth/refresh-token`,
          { refreshToken: currentRefreshToken }
        );

        const data = refreshResponse.data;
        if (data.succeeded && data.accessToken) {
          currentAccessToken = data.accessToken;
          if (data.refreshToken) {
            currentRefreshToken = data.refreshToken;
          }

          // Notify token update listeners (e.g. AuthContext)
          tokenUpdateHandlers.forEach((handler) => {
            try {
              handler({
                accessToken: data.accessToken,
                refreshToken: data.refreshToken
              });
            } catch (hErr) {
              console.error('Error in token update handler:', hErr);
            }
          });

          processQueue(null, data.accessToken);
          originalRequest.headers.Authorization = `Bearer ${data.accessToken}`;
          return axiosInstance(originalRequest);
        } else {
          throw new Error(data.errorMessage || 'Token refresh failed.');
        }
      } catch (refreshErr) {
        processQueue(refreshErr, null);
        clearAuthTokens();

        unauthorizedHandlers.forEach((handler) => {
          try {
            handler(normalized);
          } catch (hErr) {
            console.error('Error in unauthorized handler:', hErr);
          }
        });

        return Promise.reject(error);
      } finally {
        isRefreshing = false;
      }
    }

    // Direct 401 without refresh token
    if (normalized.isAuthError && !isAuthEndpoint) {
      unauthorizedHandlers.forEach((handler) => {
        try {
          handler(normalized);
        } catch (hErr) {
          console.error('Error in unauthorized handler:', hErr);
        }
      });
    }

    return Promise.reject(error);
  }
);

/**
 * Unified API Client Wrapper
 */
export const apiClient = {
  get: (url, config = {}) => axiosInstance.get(url, config),
  post: (url, data, config = {}) => axiosInstance.post(url, data, config),
  put: (url, data, config = {}) => axiosInstance.put(url, data, config),
  patch: (url, data, config = {}) => axiosInstance.patch(url, data, config),
  delete: (url, config = {}) => axiosInstance.delete(url, config),

  /**
   * Executes a financial mutation with guaranteed client-side UUID idempotency
   * and in-flight concurrency protection.
   * @param {string} url - Target financial endpoint
   * @param {any} data - Request payload
   * @param {Object} [config={}] - Additional Axios configuration
   * @returns {Promise<any>}
   */
  postFinancial: async (url, data, config = {}) => {
    const idempotencyKey = config.idempotencyKey || generateIdempotencyKey();

    return withFinancialMutationGuard(idempotencyKey, () => {
      return axiosInstance.post(url, data, {
        ...config,
        idempotent: true,
        idempotencyKey
      });
    });
  },

  /**
   * Explicitly calls token refresh endpoint.
   * @param {string} refreshToken
   * @returns {Promise<{ succeeded: boolean, accessToken: string, refreshToken: string, userId: string }>}
   */
  refreshToken: async (refreshToken) => {
    const tokenToUse = refreshToken || currentRefreshToken;
    return axiosInstance.post('/auth/refresh-token', { refreshToken: tokenToUse });
  },

  /**
   * Explicitly revokes a refresh token on logout.
   * @param {string} refreshToken
   * @returns {Promise<{ succeeded: boolean, message: string }>}
   */
  revokeToken: async (refreshToken) => {
    const tokenToUse = refreshToken || currentRefreshToken;
    return axiosInstance.post('/auth/revoke-token', { refreshToken: tokenToUse });
  }
};

export default apiClient;
