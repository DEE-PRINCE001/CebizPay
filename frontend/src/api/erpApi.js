import apiClient from './client';

export const erpApi = {
  // Inventory Items
  getInventoryItems: async (params = {}) => {
    return apiClient.get('/org/inventory/items', { params });
  },

  getInventoryItemById: async (id) => {
    return apiClient.get(`/org/inventory/items/${id}`);
  },

  createInventoryItem: async (data) => {
    return apiClient.post('/org/inventory/items', data);
  },

  updateInventoryItem: async (id, data) => {
    return apiClient.put(`/org/inventory/items/${id}`, data);
  },

  deleteInventoryItem: async (id) => {
    return apiClient.delete(`/org/inventory/items/${id}`);
  },

  restockInventoryItem: async (id, quantity, unitCost, notes = '') => {
    return apiClient.post(`/org/inventory/items/${id}/restock`, { quantity, unitCost, notes });
  },

  // Services Catalog
  getServices: async (params = {}) => {
    return apiClient.get('/org/services', { params });
  },

  getServiceById: async (id) => {
    return apiClient.get(`/org/services/${id}`);
  },

  createService: async (data) => {
    return apiClient.post('/org/services', data);
  },

  updateService: async (id, data) => {
    return apiClient.put(`/org/services/${id}`, data);
  },

  deleteService: async (id) => {
    return apiClient.delete(`/org/services/${id}`);
  },

  // Customer CRM
  getCustomers: async (params = {}) => {
    return apiClient.get('/org/customers', { params });
  },

  getCustomerById: async (id) => {
    return apiClient.get(`/org/customers/${id}`);
  },

  createCustomer: async (data) => {
    return apiClient.post('/org/customers', data);
  },

  updateCustomer: async (id, data) => {
    return apiClient.put(`/org/customers/${id}`, data);
  },

  deleteCustomer: async (id) => {
    return apiClient.delete(`/org/customers/${id}`);
  },

  // Supplier CRM
  getSuppliers: async (params = {}) => {
    return apiClient.get('/org/suppliers', { params });
  },

  getSupplierById: async (id) => {
    return apiClient.get(`/org/suppliers/${id}`);
  },

  createSupplier: async (data) => {
    return apiClient.post('/org/suppliers', data);
  },

  updateSupplier: async (id, data) => {
    return apiClient.put(`/org/suppliers/${id}`, data);
  },

  deleteSupplier: async (id) => {
    return apiClient.delete(`/org/suppliers/${id}`);
  },

  // Purchase Orders
  getPurchaseOrders: async (params = {}) => {
    return apiClient.get('/org/orders/purchase', { params });
  },

  getPurchaseOrderById: async (id) => {
    return apiClient.get(`/org/orders/purchase/${id}`);
  },

  createPurchaseOrder: async (data) => {
    return apiClient.post('/org/orders/purchase', data);
  },

  confirmPurchaseOrder: async (id) => {
    return apiClient.post(`/org/orders/purchase/${id}/confirm`);
  },

  receivePurchaseOrderItem: async (orderId, itemId, quantityReceived) => {
    return apiClient.post(`/org/orders/purchase/${orderId}/items/${itemId}/receive`, { quantityReceived });
  },

  cancelPurchaseOrder: async (id) => {
    return apiClient.post(`/org/orders/purchase/${id}/cancel`);
  },

  // Sales Orders
  getSalesOrders: async (params = {}) => {
    return apiClient.get('/org/orders/sales', { params });
  },

  getSalesOrderById: async (id) => {
    return apiClient.get(`/org/orders/sales/${id}`);
  },

  createSalesOrder: async (data) => {
    return apiClient.post('/org/orders/sales', data);
  },

  confirmSalesOrder: async (id) => {
    return apiClient.post(`/org/orders/sales/${id}/confirm`);
  },

  fulfillSalesOrderItem: async (orderId, itemId, quantityFulfilled) => {
    return apiClient.post(`/org/orders/sales/${orderId}/items/${itemId}/fulfill`, { quantityFulfilled });
  },

  cancelSalesOrder: async (id) => {
    return apiClient.post(`/org/orders/sales/${id}/cancel`);
  },

  // Operating Expenses
  getExpenses: async (params = {}) => {
    return apiClient.get('/org/expenses', { params });
  },

  getExpenseById: async (id) => {
    return apiClient.get(`/org/expenses/${id}`);
  },

  createExpense: async (data) => {
    return apiClient.post('/org/expenses', data);
  },

  deleteExpense: async (id) => {
    return apiClient.delete(`/org/expenses/${id}`);
  },

  // Invoices & Billing
  getInvoices: async (params = {}) => {
    return apiClient.get('/org/invoices', { params });
  },

  getInvoiceById: async (id) => {
    return apiClient.get(`/org/invoices/${id}`);
  },

  createInvoice: async (data) => {
    return apiClient.post('/org/invoices', data);
  },

  issueInvoice: async (id) => {
    return apiClient.post(`/org/invoices/${id}/issue`);
  },

  recordInvoicePayment: async (id, paymentData) => {
    return apiClient.post(`/org/invoices/${id}/payments`, paymentData);
  },

  cancelInvoice: async (id) => {
    return apiClient.post(`/org/invoices/${id}/cancel`);
  },

  // Payment Receipts
  getReceipts: async (params = {}) => {
    return apiClient.get('/org/receipts', { params });
  },

  getReceiptById: async (id) => {
    return apiClient.get(`/org/receipts/${id}`);
  },

  getReceiptByInvoiceId: async (invoiceId) => {
    return apiClient.get(`/org/receipts/by-invoice/${invoiceId}`);
  },

  // Company Payment Vouchers
  getCompanyVouchers: async (params = {}) => {
    return apiClient.get('/org/company-vouchers', { params });
  },

  getCompanyVoucherById: async (id) => {
    return apiClient.get(`/org/company-vouchers/${id}`);
  },

  createCompanyVoucher: async (data) => {
    return apiClient.post('/org/company-vouchers', data);
  },

  approveCompanyVoucher: async (id) => {
    return apiClient.post(`/org/company-vouchers/${id}/approve`);
  },

  payCompanyVoucher: async (id, paymentData) => {
    return apiClient.post(`/org/company-vouchers/${id}/pay`, paymentData);
  },

  cancelCompanyVoucher: async (id) => {
    return apiClient.post(`/org/company-vouchers/${id}/cancel`);
  },

  // Financial Reports
  getSalesReport: async (params = {}) => {
    return apiClient.get('/org/reports/sales', { params });
  },

  getPurchaseReport: async (params = {}) => {
    return apiClient.get('/org/reports/purchases', { params });
  },

  getSettlementReport: async (params = {}) => {
    return apiClient.get('/org/reports/settlements', { params });
  },

  getProfitLossReport: async (params = {}) => {
    return apiClient.get('/org/reports/profit-loss', { params });
  }
};
