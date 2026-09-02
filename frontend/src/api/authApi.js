import apiClient from './client';

export const authApi = {
  // Email & Password login (Super Admin, Org Admin, Individual)
  login: async (email, password) => {
    return apiClient.post('/auth/login', { email, password });
  },

  // Verify MFA Challenge Code
  verifyMfa: async (challengeId, code) => {
    return apiClient.post('/auth/mfa/verify', { challengeId, code });
  },

  // Toggle MFA on/off
  toggleMfa: async (enable) => {
    return apiClient.post('/auth/mfa/toggle', { enable });
  },

  // Initiate Phone registration (Mobile Consumer)
  registerPhone: async (phone, deviceId = 'web-browser-client') => {
    return apiClient.post('/auth/register/phone', { phone, deviceId });
  },

  // Verify OTP & complete user creation
  verifyOtp: async ({ phone, code, email, password, firstName, lastName }) => {
    return apiClient.post('/auth/register/otp/verify', {
      phone,
      code,
      email,
      password,
      firstName,
      lastName
    });
  },

  // Change Password
  changePassword: async (currentPassword, newPassword) => {
    return apiClient.post('/auth/change-password', { currentPassword, newPassword });
  }
};

export default authApi;
