import apiClient from './client';

export const payrollApi = {
  // Calculate preview dry-run
  calculatePayroll: async (currency = 'NGN', criteria = {}) => {
    return apiClient.post('/org/payroll/calculate', { currency, criteria });
  },

  // Execute live payroll batch
  executePayroll: async (currency, periodStart, periodEnd, criteria = {}) => {
    return apiClient.post('/org/payroll/execute', { currency, periodStart, periodEnd, criteria });
  },

  // Get batch progress and line items
  getBatchProgress: async (batchId, pageNumber = 1, pageSize = 50) => {
    return apiClient.get(`/org/payroll/${batchId}`, {
      params: { pageNumber, pageSize }
    });
  },

  // Retry failed items in batch
  retryFailedItems: async (batchId) => {
    return apiClient.post(`/org/payroll/${batchId}/retry-failed`);
  },

  // Cancel pending batch
  cancelBatch: async (batchId) => {
    return apiClient.post(`/org/payroll/${batchId}/cancel`);
  },

  // Get single Payment Voucher
  getVoucherById: async (voucherId) => {
    return apiClient.get(`/org/payroll/vouchers/${voucherId}`);
  },

  // Update voucher metadata (Bank, Remarks, Description)
  updateVoucherMetadata: async (voucherId, data) => {
    return apiClient.put(`/org/payroll/vouchers/${voucherId}`, data);
  }
};
