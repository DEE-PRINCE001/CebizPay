import apiClient from './client';

export const orgApi = {
  // KYB Registration
  registerStep1: async (data) => {
    return apiClient.post('/org/kyb/register-step1', data);
  },

  registerStep2: async (data) => {
    return apiClient.post('/org/kyb/register-step2', data);
  },

  updateOrgStatus: async (orgId, status, reason = null) => {
    return apiClient.patch(`/organizations/${orgId}/status`, { status, reason });
  },

  // Departments
  getDepartments: async (params = {}) => {
    return apiClient.get('/org/departments', { params });
  },

  getDepartmentById: async (id) => {
    return apiClient.get(`/org/departments/${id}`);
  },

  createDepartment: async (data) => {
    return apiClient.post('/org/departments', data);
  },

  updateDepartment: async (id, data) => {
    return apiClient.put(`/org/departments/${id}`, data);
  },

  deleteDepartment: async (id) => {
    return apiClient.delete(`/org/departments/${id}`);
  },

  // Workforce Roles
  getRoles: async (params = {}) => {
    return apiClient.get('/org/roles', { params });
  },

  getRoleById: async (id) => {
    return apiClient.get(`/org/roles/${id}`);
  },

  createRole: async (data) => {
    return apiClient.post('/org/roles', data);
  },

  updateRole: async (id, data) => {
    return apiClient.put(`/org/roles/${id}`, data);
  },

  deleteRole: async (id) => {
    return apiClient.delete(`/org/roles/${id}`);
  },

  // Salary Levels
  getSalaryLevels: async (params = {}) => {
    return apiClient.get('/org/levels', { params });
  },

  getSalaryLevelById: async (id) => {
    return apiClient.get(`/org/levels/${id}`);
  },

  createSalaryLevel: async (data) => {
    return apiClient.post('/org/levels', data);
  },

  updateSalaryLevel: async (id, data) => {
    return apiClient.put(`/org/levels/${id}`, data);
  },

  deleteSalaryLevel: async (id) => {
    return apiClient.delete(`/org/levels/${id}`);
  },

  // Staff Management
  getStaffDirectory: async (params = {}) => {
    return apiClient.get('/org/staff', { params });
  },

  getStaffProfile: async (id) => {
    return apiClient.get(`/org/staff/${id}`);
  },

  createStaffDirect: async (data) => {
    return apiClient.post('/org/staff/create', data);
  },

  inviteStaffSingle: async (email) => {
    return apiClient.post('/org/staff/invite', { email });
  },

  inviteStaffBulk: async (emails) => {
    return apiClient.post('/org/staff/invite-bulk', { emails });
  },

  acceptStaffInvitation: async (command) => {
    return apiClient.post('/org/staff/accept', command);
  },

  assignStaffWorkforce: async (id, data) => {
    return apiClient.put(`/org/staff/${id}/assign`, data);
  },

  suspendStaff: async (id, reason) => {
    return apiClient.patch(`/org/staff/${id}/suspend`, { reason });
  },

  reactivateStaff: async (id) => {
    return apiClient.patch(`/org/staff/${id}/reactivate`);
  },

  terminateStaff: async (id, reason) => {
    return apiClient.post(`/org/staff/${id}/terminate`, { reason });
  },
};

export default orgApi;
