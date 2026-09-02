import axios from 'axios';

// Base API URL pointing to the versioned API
const API_BASE_URL = '/api/v1';

export const apiClient = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
  timeout: 30000,
});

// Request interceptor to attach JWT, Organization context, and Idempotency key
apiClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('cebizpay_token');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }

    const activeOrgId = localStorage.getItem('cebizpay_active_org_id');
    if (activeOrgId) {
      config.headers['X-Organization-Id'] = activeOrgId;
    }

    // Auto-generate Idempotency-Key for financial mutating requests if missing
    if (
      ['post', 'put', 'patch'].includes(config.method?.toLowerCase()) &&
      !config.headers['Idempotency-Key'] &&
      !config.headers['X-Idempotency-Key']
    ) {
      const uniqueKey = 'idemp_' + Math.random().toString(36).substring(2, 12) + Date.now().toString(36);
      config.headers['Idempotency-Key'] = uniqueKey;
    }

    return config;
  },
  (error) => Promise.reject(error)
);

// Response interceptor for unified ProblemDetails extraction
apiClient.interceptors.response.use(
  (response) => response.data,
  (error) => {
    let errorMessage = 'An unexpected error occurred. Please try again.';

    if (error.response?.data) {
      const data = error.response.data;
      if (typeof data === 'string') {
        errorMessage = data;
      } else if (data.detail) {
        errorMessage = data.detail;
      } else if (data.message) {
        errorMessage = data.message;
      } else if (data.errors) {
        if (Array.isArray(data.errors)) {
          errorMessage = data.errors.join(', ');
        } else if (typeof data.errors === 'object') {
          const flat = Object.values(data.errors).flat();
          errorMessage = flat.join(', ');
        }
      } else if (data.title) {
        errorMessage = data.title;
      }
    } else if (error.message) {
      errorMessage = error.message;
    }

    const customError = new Error(errorMessage);
    customError.status = error.response?.status;
    customError.data = error.response?.data;
    customError.originalError = error;

    return Promise.reject(customError);
  }
);

export default apiClient;
