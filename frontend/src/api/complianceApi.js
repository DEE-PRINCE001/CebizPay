import apiClient from './client';

export const complianceApi = {
  // BVN Verification against NIBSS
  verifyBvn: async (data, idempotencyKey = null) => {
    const headers = idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : {};
    return apiClient.post('/compliance/kyc/bvn', data, { headers });
  },

  // NIN Verification against NIMC
  verifyNin: async (data, idempotencyKey = null) => {
    const headers = idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : {};
    return apiClient.post('/compliance/kyc/nin', data, { headers });
  },

  // Biometric Liveness & 1:1 Facial Match
  verifyBiometrics: async (data, idempotencyKey = null) => {
    const headers = idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : {};
    return apiClient.post('/compliance/kyc/biometrics', data, { headers });
  },

  // Government ID Document Verification (NIMC, Driver's license, Passport)
  verifyDocument: async (data, idempotencyKey = null) => {
    const headers = idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : {};
    return apiClient.post('/compliance/kyc/document', data, { headers });
  },

  // AML / PEP / Sanctions Screening
  screenAml: async (data, idempotencyKey = null) => {
    const headers = idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : {};
    return apiClient.post('/compliance/kyc/aml', data, { headers });
  },

  // Corporate CAC Verification
  verifyBusiness: async (data, idempotencyKey = null) => {
    const headers = idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : {};
    return apiClient.post('/compliance/kyb/business', data, { headers });
  },

  // Query Beneficial Owners & Directors
  getBeneficialOwners: async (data, idempotencyKey = null) => {
    const headers = idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : {};
    return apiClient.post('/compliance/kyb/beneficial-owners', data, { headers });
  },

  // Compliance Profile & Limits
  getProfile: async (params = {}) => {
    return apiClient.get('/compliance/profile', { params });
  },

  getRisk: async (params = {}) => {
    return apiClient.get('/compliance/risk', { params });
  },

  getRiskHistory: async (params = {}) => {
    return apiClient.get('/compliance/risk/history', { params });
  },

  checkEligibility: async (data) => {
    return apiClient.post('/compliance/eligibility/check', data);
  },

  // Enhanced Due Diligence (EDD)
  getEddCase: async (id) => {
    return apiClient.get(`/compliance/edd/${id}`);
  },

  submitEddInformation: async (id, submittedInformation) => {
    return apiClient.post(`/compliance/edd/${id}/submit`, { submittedInformation });
  },

  // Historical evidence
  getEvidence: async (params = {}) => {
    return apiClient.get('/compliance/evidence', { params });
  }
};
