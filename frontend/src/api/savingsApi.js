import apiClient from './client';

export const savingsApi = {
  // Org Sponsored Schemes (B2B Org)
  getOrgSponsoredSchemes: async (params = {}) => {
    return apiClient.get('/org/savings/schemes', { params });
  },

  createOrgSponsoredScheme: async (schemeData) => {
    return apiClient.post('/org/savings/schemes', schemeData);
  },

  // Staff Personal Savings Operations (B2C Staff)
  previewSavings: async (previewData) => {
    return apiClient.post('/work/savings/preview', previewData);
  },

  openSavingsAccount: async (accountData) => {
    return apiClient.post('/work/savings', accountData);
  },

  getMySavingsAccounts: async () => {
    return apiClient.get('/work/savings');
  },

  getSavingsAccountById: async (id) => {
    return apiClient.get(`/work/savings/${id}`);
  },

  contributeSavings: async (id, amount, idempotencyKey = null) => {
    return apiClient.post(`/work/savings/${id}/contribute`, { amount, idempotencyKey });
  },

  previewSavingsWithdrawal: async (id) => {
    return apiClient.post(`/work/savings/${id}/withdraw/preview`);
  },

  withdrawSavings: async (id, idempotencyKey = null) => {
    return apiClient.post(`/work/savings/${id}/withdraw`, { idempotencyKey });
  },
};

export default savingsApi;
