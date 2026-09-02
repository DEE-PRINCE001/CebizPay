import apiClient from './client';

export const payrollApi = {
  // Compute deterministic payroll preview calculation
  calculatePayroll: async ({ currency = 'NGN', criteria = {} } = {}) => {
    return apiClient.post('/org/payroll/calculate', { currency, criteria });
  },

  // Create & enqueue live payroll batch execution
  executePayroll: async ({ currency = 'NGN', periodStart, periodEnd, criteria = {} } = {}) => {
    return apiClient.post('/org/payroll/execute', {
      currency,
      periodStart,
      periodEnd,
      criteria,
    });
  },

  // Get payroll batch progress & lines
  getBatchProgress: async (batchId, pageNumber = 1, pageSize = 50) => {
    return apiClient.get(`/org/payroll/${batchId}`, {
      params: { pageNumber, pageSize },
    });
  },

  // Retry eligible failed items in batch
  retryFailedItems: async (batchId) => {
    return apiClient.post(`/org/payroll/${batchId}/retry-failed`);
  },

  // Cancel pending batch
  cancelBatch: async (batchId) => {
    return apiClient.post(`/org/payroll/${batchId}/cancel`);
  },

  // Get issued Payment Voucher by ID
  getVoucherById: async (voucherId) => {
    return apiClient.get(`/org/payroll/vouchers/${voucherId}`);
  },

  // Update safe non-financial voucher metadata
  updateVoucherMetadata: async (voucherId, data) => {
    return apiClient.put(`/org/payroll/vouchers/${voucherId}`, data);
  },
};

export default payrollApi;
