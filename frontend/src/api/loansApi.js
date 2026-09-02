import apiClient from './client';

export const loansApi = {
  // Corporate Loan Plans (Organization level)
  getLoanPlans: async (activeOnly = false) => {
    return apiClient.get('/org/loan-plans', { params: { activeOnly } });
  },

  getLoanPlanById: async (id) => {
    return apiClient.get(`/org/loan-plans/${id}`);
  },

  createLoanPlan: async (planData) => {
    return apiClient.post('/org/loan-plans', planData);
  },

  updateLoanPlan: async (id, planData) => {
    return apiClient.put(`/org/loan-plans/${id}`, planData);
  },

  // Organization Loan Applications Review
  getOrgApplications: async () => {
    return apiClient.get('/org/loans/applications');
  },

  getOrgApplicationById: async (id) => {
    return apiClient.get(`/org/loans/applications/${id}`);
  },

  approveApplication: async (id) => {
    return apiClient.post(`/org/loans/applications/${id}/approve`);
  },

  declineApplication: async (id, reason) => {
    return apiClient.post(`/org/loans/applications/${id}/decline`, { reason });
  },

  getOrgContracts: async () => {
    return apiClient.get('/org/loans/contracts');
  },

  getOrgContractById: async (id) => {
    return apiClient.get(`/org/loans/contracts/${id}`);
  },

  convertTerminatedStaffLoans: async (staffUserId, reason) => {
    return apiClient.post(`/org/loans/staff/${staffUserId}/convert-offboarding`, { reason });
  },

  // Staff / Work Loans (Self-Service)
  previewLoan: async (previewData) => {
    return apiClient.post('/work/loans/preview', previewData);
  },

  submitStaffLoanApplication: async (applicationData) => {
    return apiClient.post('/work/loans/applications', applicationData);
  },

  getMyApplications: async () => {
    return apiClient.get('/work/loans/applications');
  },

  getMyContracts: async () => {
    return apiClient.get('/work/loans/contracts');
  },

  getMyContractById: async (id) => {
    return apiClient.get(`/work/loans/contracts/${id}`);
  }
};
