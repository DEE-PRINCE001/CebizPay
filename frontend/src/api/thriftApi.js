import apiClient from './client';

export const thriftApi = {
  // Create Thrift / Ajo / Esusu group
  createThriftGroup: async (groupData) => {
    return apiClient.post('/work/thrift', groupData);
  },

  // Get user's active thrift groups
  getMyThriftGroups: async () => {
    return apiClient.get('/work/thrift');
  },

  // Get specific thrift group details
  getThriftGroupById: async (id) => {
    return apiClient.get(`/work/thrift/${id}`);
  },

  // Issue invitation code
  inviteThriftMember: async (id, inviteData) => {
    return apiClient.post(`/work/thrift/${id}/invite`, inviteData);
  },

  // Join thrift group with invitation code
  joinThriftGroup: async (joinData) => {
    return apiClient.post('/work/thrift/join', joinData);
  },

  // Select rotation payout position
  selectThriftPosition: async (id, positionData) => {
    return apiClient.post(`/work/thrift/${id}/position`, positionData);
  },

  // Get thrift group members roster
  getThriftMembers: async (id) => {
    return apiClient.get(`/work/thrift/${id}/members`);
  },

  // Get thrift group cycles schedule
  getThriftCycles: async (id) => {
    return apiClient.get(`/work/thrift/${id}/cycles`);
  },

  // Authoritatively lock rotation positions
  lockThriftPositions: async (id) => {
    return apiClient.post(`/work/thrift/${id}/lock`);
  },

  // Leave thrift group and claim reimbursement
  leaveThriftGroup: async (id, memberId, requestData = {}) => {
    return apiClient.post(`/work/thrift/${id}/members/${memberId}/leave`, requestData);
  },
};

export default thriftApi;
