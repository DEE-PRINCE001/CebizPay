import apiClient from './client';

export const cardsApi = {
  // List tokenized saved cards
  getSavedCards: async () => {
    return apiClient.get('/saved-cards');
  },

  getSavedCardById: async (id) => {
    return apiClient.get(`/saved-cards/${id}`);
  },

  setDefaultCard: async (id) => {
    return apiClient.post(`/saved-cards/${id}/default`);
  },

  revokeSavedCard: async (id) => {
    return apiClient.delete(`/saved-cards/${id}`);
  },

  // Initialize Card Funding
  initializeCardFunding: async (walletId, amount, currency = 'NGN', callbackUrl = window.location.href, provider = null) => {
    return apiClient.post('/funding/card/initialize', {
      walletId,
      amount,
      currency,
      callbackUrl,
      provider
    });
  },

  // Charge saved tokenized card
  chargeSavedCard: async (savedCardId, amount, currency = 'NGN', idempotencyKey = null) => {
    const headers = idempotencyKey ? { 'X-Idempotency-Key': idempotencyKey } : {};
    return apiClient.post('/funding/card/charge-saved', {
      savedCardId,
      amount,
      currency,
      idempotencyKey
    }, { headers });
  },

  // Reconcile card funding
  reconcileCardFunding: async (fundingTransactionId) => {
    return apiClient.post(`/funding/card/${fundingTransactionId}/reconcile`);
  }
};
