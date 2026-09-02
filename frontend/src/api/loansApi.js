import apiClient from './client';

export const loansApi = {
  // Corporate Loan Plans (B2B Org)
  getCorporateLoanPlans: async (params = {}) => {
    return apiClient.get('/org/loan-plans', { params });
  },

  getCorporateLoanPlanById: async (id) => {
    return apiClient.get(`/org/loan-plans/${id}`);
  },

  createCorporateLoanPlan: async (planData) => {
    return apiClient.post('/org/loan-plans', planData);
  },

  updateCorporateLoanPlan: async (id, planData) => {
    return apiClient.put(`/org/loan-plans/${id}`, planData);
  },

  updateLoanPlanStatus: async (id, status) => {
    return apiClient.patch(`/org/loan-plans/${id}/status`, { status });
  },

  // Org Staff Loan Applications Queue (B2B Org Review)
  getOrgLoanApplications: async (params = {}) => {
    return apiClient.get('/org/loans/applications', { params });
  },

  reviewLoanApplication: async (id, decisionData) => {
    return apiClient.post(`/org/loans/applications/${id}/review`, decisionData);
  },

  // Staff Personal Loan Operations (B2C Staff)
  previewStaffLoan: async (previewData) => {
    return apiClient.post('/work/loans/preview', previewData);
  },

  submitStaffLoanApplication: async (applicationData) => {
    return apiClient.post('/work/loans/applications', applicationData);
  },

  getMyStaffLoanApplications: async () => {
    return apiClient.get('/work/loans/applications');
  },

  getMyStaffLoanContracts: async () => {
    return apiClient.get('/work/loans/contracts');
  },

  getStaffLoanContractById: async (id) => {
    return apiClient.get(`/work/loans/contracts/${id}`);
  },
};

export default loansApi;
