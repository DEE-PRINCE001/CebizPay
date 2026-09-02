import apiClient from './client';

export const complianceApi = {
  // Individual KYC BVN Verification
  verifyBvn: async (bvnData) => {
    return apiClient.post('/compliance/kyc/bvn', bvnData);
  },

  // Individual KYC NIN Verification
  verifyNin: async (ninData) => {
    return apiClient.post('/compliance/kyc/nin', ninData);
  },

  // Biometric Liveness Facial Match
  verifyBiometrics: async (bioData) => {
    return apiClient.post('/compliance/kyc/biometrics', bioData);
  },

  // Document OCR Upload
  verifyDocument: async (docData) => {
    return apiClient.post('/compliance/kyc/document', docData);
  },

  // AML / PEP Watchlist Screening
  screenAml: async (amlData) => {
    return apiClient.post('/compliance/screening/aml', amlData);
  },

  // Corporate CAC Verification (KYB)
  verifyCac: async (cacData) => {
    return apiClient.post('/compliance/kyb/cac', cacData);
  },

  // Corporate TIN Verification (KYB)
  verifyTin: async (tinData) => {
    return apiClient.post('/compliance/kyb/tin', tinData);
  },

  // Get Individual KYC Document Submissions
  getIndividualKycDocuments: async (userId) => {
    return apiClient.get(`/individuals/${userId}/kyc-documents`);
  },

  // Submit Individual KYC Document
  submitIndividualKycDocument: async (userId, docData) => {
    return apiClient.post(`/individuals/${userId}/kyc-documents`, docData);
  },

  // Get Compliance Evidence / Audit Trail
  getComplianceEvidence: async (params = {}) => {
    return apiClient.get('/compliance/evidence', { params });
  },

  // Get Risk Profile & Explainable Findings
  getRiskAssessment: async (subjectType, subjectId, organizationId = null) => {
    return apiClient.get(`/admin/compliance/assessments/${subjectType}/${subjectId}`, {
      params: { organizationId },
    });
  },

  // Get EDD Cases Queue
  getEddCases: async (params = {}) => {
    return apiClient.get('/admin/compliance/edd-cases', { params });
  },
};

export default complianceApi;
