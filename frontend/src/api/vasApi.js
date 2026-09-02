import apiClient from './client';

export const vasApi = {
  // Buy Airtime (protected by 120s duplicate prevention)
  purchaseAirtime: async (data, idempotencyKey = null) => {
    const headers = idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : {};
    return apiClient.post('/vas/airtime', data, { headers });
  },

  // Buy Data Bundle (protected by 120s duplicate prevention)
  purchaseData: async (data, idempotencyKey = null) => {
    const headers = idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : {};
    return apiClient.post('/vas/data', data, { headers });
  },

  // Get Data Bundle Catalog
  getDataBundles: async (network = null) => {
    return apiClient.get('/vas/data/bundles', { params: { network } });
  },

  // Auto-detect Telco operator from Nigerian phone number
  detectOperator: async (phoneNumber) => {
    return apiClient.get('/vas/operators/detect', { params: { phoneNumber } });
  },

  // Get VAS Transaction by ID
  getTransactionById: async (id) => {
    return apiClient.get(`/vas/transactions/${id}`);
  }
};
