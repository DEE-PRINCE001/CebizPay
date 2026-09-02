import apiClient from './client';

export const thriftApi = {
  createGroup: async (groupData) => {
    return apiClient.post('/work/thrift', groupData);
  },

  getMyGroups: async () => {
    return apiClient.get('/work/thrift');
  },

  getGroupById: async (id) => {
    return apiClient.get(`/work/thrift/${id}`);
  },

  inviteMember: async (groupId, email, proposedPosition = null) => {
    return apiClient.post(`/work/thrift/${groupId}/invite`, { email, proposedPosition });
  },

  joinGroup: async (invitationCode) => {
    return apiClient.post('/work/thrift/join', { invitationCode });
  },

  selectPosition: async (groupId, position) => {
    return apiClient.post(`/work/thrift/${groupId}/position`, { position });
  },

  getGroupMembers: async (groupId) => {
    return apiClient.get(`/work/thrift/${groupId}/members`);
  },

  getGroupCycles: async (groupId) => {
    return apiClient.get(`/work/thrift/${groupId}/cycles`);
  },

  lockPositions: async (groupId) => {
    return apiClient.post(`/work/thrift/${groupId}/lock`);
  },

  leaveAndReimburse: async (groupId, memberId, reason = '') => {
    return apiClient.post(`/work/thrift/${groupId}/members/${memberId}/leave`, { reason });
  }
};
