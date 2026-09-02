import apiClient from './client';

export const walletApi = {
  // Peer wallet transfer
  peerTransfer: async (data, idempotencyKey = null) => {
    const headers = idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : {};
    return apiClient.post('/wallet/transfer/peer', data, { headers });
  },

  // Outbound bank transfer
  bankTransfer: async (data, idempotencyKey = null) => {
    const headers = idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : {};
    return apiClient.post('/wallet/transfer/bank', data, { headers });
  },

  // Resolve beneficiary bank account name
  resolveBankAccount: async (bankCode, accountNumber) => {
    return apiClient.get('/wallet/transfer/resolve-account', {
      params: { bankCode, accountNumber }
    });
  },

  // Get external funding accounts attached to wallet
  getExternalAccounts: async (organizationId = null, currency = null) => {
    return apiClient.get('/wallet/external-accounts', {
      params: { organizationId, currency }
    });
  },

  // Provision Monnify reserved account
  provisionMonnifyAccount: async (organizationId = null, currency = 'NGN') => {
    return apiClient.post('/wallet/external-accounts/monnify', null, {
      params: { organizationId, currency }
    });
  },

  // Set primary external funding account
  setPrimaryAccount: async (accountId, organizationId = null) => {
    return apiClient.post(`/wallet/external-accounts/${accountId}/primary`, null, {
      params: { organizationId }
    });
  },

  // Deactivate account
  deactivateAccount: async (accountId, organizationId = null) => {
    return apiClient.delete(`/wallet/external-accounts/${accountId}`, {
      params: { organizationId }
    });
  },

  // Virtual Accounts controller
  provisionVirtualAccount: async (currency = 'NGN', provider = null) => {
    return apiClient.post('/virtual-accounts/provision', { currency, provider });
  },

  getPrimaryVirtualAccount: async (currency = 'NGN') => {
    return apiClient.get('/virtual-accounts/primary', {
      params: { currency }
    });
  },

  // Get funding transaction status
  getFundingTransaction: async (id, organizationId = null) => {
    return apiClient.get(`/wallet/funding/${id}`, {
      params: { organizationId }
    });
  }
};
