import apiClient from './client';

export const walletApi = {
  // Execute peer wallet transfer
  transferPeer: async ({ recipientIdentifier, amount, currency = 'NGN', transactionPin, idempotencyKey, organizationContext = null }) => {
    return apiClient.post('/wallet/transfer/peer', {
      recipientIdentifier,
      amount: parseFloat(amount),
      currency,
      transactionPin,
      idempotencyKey,
      organizationContext,
    });
  },

  // Execute outbound bank payout via NIP
  transferBank: async ({ destinationBankCode, destinationAccountNumber, amount, currency = 'NGN', transactionPin, idempotencyKey, organizationContext = null }) => {
    return apiClient.post('/wallet/transfer/bank', {
      destinationBankCode,
      destinationAccountNumber,
      amount: parseFloat(amount),
      currency,
      transactionPin,
      idempotencyKey,
      organizationContext,
    });
  },

  // Resolve NUBAN Bank Account name
  resolveBankAccount: async (bankCode, accountNumber) => {
    return apiClient.get('/wallet/transfer/resolve-account', {
      params: { bankCode, accountNumber },
    });
  },

  // Get external funding accounts
  getExternalFundingAccounts: async (params = {}) => {
    return apiClient.get('/wallet/external-accounts', { params });
  },

  // Provision Monnify virtual account
  provisionMonnifyAccount: async (params = {}) => {
    return apiClient.post('/wallet/external-accounts/monnify', null, { params });
  },

  // Provision dedicated virtual account
  provisionVirtualAccount: async (data = {}) => {
    return apiClient.post('/virtual-accounts/provision', data);
  },

  // Get primary dedicated virtual account (DVA)
  getPrimaryVirtualAccount: async (currency = 'NGN') => {
    return apiClient.get('/virtual-accounts/primary', { params: { currency } });
  },

  // Get funding transaction by ID
  getFundingTransaction: async (fundingId, organizationId = null) => {
    return apiClient.get(`/wallet/funding/${fundingId}`, {
      params: { organizationId },
    });
  },
};

export default walletApi;
