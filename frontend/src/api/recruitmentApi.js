import apiClient from './client';

export const recruitmentApi = {
  // Org Job Postings
  getJobPostings: async (params = {}) => {
    return apiClient.get('/org/recruitment/jobs', { params });
  },

  getJobPostingById: async (id) => {
    return apiClient.get(`/org/recruitment/jobs/${id}`);
  },

  createJobPosting: async (data) => {
    return apiClient.post('/org/recruitment/jobs', data);
  },

  updateJobPosting: async (id, data) => {
    return apiClient.put(`/org/recruitment/jobs/${id}`, data);
  },

  publishJobPosting: async (id) => {
    return apiClient.post(`/org/recruitment/jobs/${id}/publish`);
  },

  closeJobPosting: async (id) => {
    return apiClient.post(`/org/recruitment/jobs/${id}/close`);
  },

  cancelJobPosting: async (id) => {
    return apiClient.post(`/org/recruitment/jobs/${id}/cancel`);
  },

  // Org Candidate Applications
  getJobApplications: async (jobId, params = {}) => {
    return apiClient.get(`/org/recruitment/jobs/${jobId}/applications`, { params });
  },

  getApplicationById: async (id) => {
    return apiClient.get(`/org/recruitment/applications/${id}`);
  },

  reviewApplication: async (id, notes = '') => {
    return apiClient.post(`/org/recruitment/applications/${id}/review`, { notes });
  },

  shortlistApplication: async (id, notes = '') => {
    return apiClient.post(`/org/recruitment/applications/${id}/shortlist`, { notes });
  },

  rejectApplication: async (id, rejectionReason, notes = '') => {
    return apiClient.post(`/org/recruitment/applications/${id}/reject`, { rejectionReason, notes });
  },

  acceptApplication: async (id, notes = '') => {
    return apiClient.post(`/org/recruitment/applications/${id}/accept`, { notes });
  },

  // Public Candidate Portal
  getPublicJobs: async (params = {}) => {
    return apiClient.get('/recruitment/jobs', { params });
  },

  getPublicJobById: async (id) => {
    return apiClient.get(`/recruitment/jobs/${id}`);
  },

  submitApplication: async (jobId, data) => {
    return apiClient.post(`/recruitment/jobs/${jobId}/applications`, data);
  },

  withdrawApplication: async (id) => {
    return apiClient.post(`/recruitment/applications/${id}/withdraw`);
  }
};
