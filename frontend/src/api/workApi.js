import apiClient from './client';

export const workApi = {
  // Join organization via invitation code
  joinOrganization: async (invitationCode) => {
    return apiClient.post('/work/organisation/join', { invitationCode });
  }
};
