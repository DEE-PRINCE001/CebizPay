import apiClient from './client';

export const adminApi = {
  // Compliance Profiles & List
  getComplianceProfiles: async (params = {}) => {
    return apiClient.get('/admin/compliance/profiles', { params });
  },

  getComplianceProfileById: async (id) => {
    return apiClient.get(`/admin/compliance/profiles/${id}`);
  },

  // KYC/KYB Direct Review
  reviewKyc: async (userId, status, reason = '') => {
    return apiClient.post('/admin/kyc/review', { userId, status, reason });
  },

  reviewKyb: async (organizationId, status, reason = '') => {
    return apiClient.post('/admin/kyb/review', { organizationId, status, reason });
  },

  updateOrgStatus: async (orgId, status, reason = '') => {
    return apiClient.patch(`/organizations/${orgId}/status`, { status, reason });
  },

  // EDD Cases
  decideEddCase: async (caseId, decision, notes = '') => {
    return apiClient.post(`/admin/compliance/edd-cases/${caseId}/decision`, { decision, notes });
  },

  // Restrictions
  addRestriction: async (profileId, restrictionData) => {
    return apiClient.post(`/admin/compliance/profiles/${profileId}/restrictions`, restrictionData);
  },

  removeRestriction: async (profileId, restrictionId) => {
    return apiClient.delete(`/admin/compliance/profiles/${profileId}/restrictions/${restrictionId}`);
  },

  // Fee Policies
  getActivePeerFeePolicy: async () => {
    return apiClient.get('/admin/fees/peer-transfer/active');
  },

  getAllPeerFeePolicies: async () => {
    return apiClient.get('/admin/fees/peer-transfer');
  },

  createPeerFeePolicy: async (policyData) => {
    return apiClient.post('/admin/fees/peer-transfer', policyData);
  },

  getActiveBankFeePolicy: async () => {
    return apiClient.get('/admin/fees/bank-transfer/active');
  },

  getAllBankFeePolicies: async () => {
    return apiClient.get('/admin/fees/bank-transfer');
  },

  createBankFeePolicy: async (policyData) => {
    return apiClient.post('/admin/fees/bank-transfer', policyData);
  },

  getActivePlatformPolicy: async (operationType) => {
    return apiClient.get('/admin/fees/platform/active', { params: { operationType } });
  },

  getAllPlatformPolicies: async (operationType) => {
    return apiClient.get('/admin/fees/platform', { params: { operationType } });
  },

  createPlatformPolicy: async (policyData) => {
    return apiClient.post('/admin/fees/platform', policyData);
  },

  // Savings Interest Policies
  getSavingsInterestPolicies: async () => {
    return apiClient.get('/admin/savings/interest-policies');
  },

  createSavingsInterestPolicy: async (policyData) => {
    return apiClient.post('/admin/savings/interest-policies', policyData);
  },

  // Reconciliation Control Plane
  getReconciliationRecords: async (params = {}) => {
    return apiClient.get('/admin/reconciliation/records', { params });
  },

  getOutstandingRecoveries: async (params = {}) => {
    return apiClient.get('/admin/reconciliation/recoveries', { params });
  },

  requeryTransactionStatus: async (reference) => {
    return apiClient.post('/admin/reconciliation/requery', { reference });
  },

  retryWebhookEvent: async (eventId, isCompliance = false) => {
    return apiClient.post(`/admin/reconciliation/events/${eventId}/retry`, null, { params: { isCompliance } });
  },

  submitManualReview: async (recordId, decision, reviewerNotes) => {
    return apiClient.post(`/admin/reconciliation/records/${recordId}/review`, { decision, reviewerNotes });
  },

  // Audit Logs
  getAuditLogs: async (params = {}) => {
    return apiClient.get('/admin/audit-logs', { params });
  },

  // Permissions & Governance
  grantAdminPermission: async (adminUserId, permission) => {
    return apiClient.post('/admin/permissions/grant', { adminUserId, permission });
  },

  revokeAdminPermission: async (adminUserId, permission) => {
    return apiClient.post('/admin/permissions/revoke', { adminUserId, permission });
  },

  // Org Payroll Analytics rollup
  getOrgPayrollAnalytics: async (orgId) => {
    return apiClient.get(`/admin/organizations/${orgId}/payroll-analytics`);
  }
};
