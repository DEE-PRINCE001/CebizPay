import apiClient from './client';

export const savingsApi = {
  // Org-Sponsored Savings Plans
  createOrgSavingsPlan: async (planData) => {
    return apiClient.post('/org/savings/plans', planData);
  },

  getOrgSavingsPlans: async () => {
    return apiClient.get('/org/savings/plans');
  },

  getOrgSavingsPlanById: async (id) => {
    return apiClient.get(`/org/savings/plans/${id}`);
  },

  getOrgSavingsParticipants: async (id) => {
    return apiClient.get(`/org/savings/plans/${id}/participants`);
  },

  // Staff / User Savings Accounts
  previewSavings: async (previewData) => {
    return apiClient.post('/work/savings/preview', previewData);
  },

  openSavingsAccount: async (data, idempotencyKey = null) => {
    const headers = idempotencyKey ? { 'X-Idempotency-Key': idempotencyKey } : {};
    return apiClient.post('/work/savings', data, { headers });
  },

  getMySavingsAccounts: async () => {
    return apiClient.get('/work/savings');
  },

  getSavingsAccountById: async (id) => {
    return apiClient.get(`/work/savings/${id}`);
  },

  contributeToSavings: async (id, amount, idempotencyKey = null) => {
    const headers = idempotencyKey ? { 'X-Idempotency-Key': idempotencyKey } : {};
    return apiClient.post(`/work/savings/${id}/contribute`, { amount }, { headers });
  },

  previewWithdrawal: async (id) => {
    return apiClient.post(`/work/savings/${id}/withdraw/preview`);
  },

  withdrawFromSavings: async (id, idempotencyKey = null) => {
    const headers = idempotencyKey ? { 'X-Idempotency-Key': idempotencyKey } : {};
    return apiClient.post(`/work/savings/${id}/withdraw`, {}, { headers });
  }
};
