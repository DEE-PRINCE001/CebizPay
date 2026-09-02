import apiClient from './client';

export const authApi = {
  // Email & Password login (Admin, Org, Individual)
  login: async (email, password) => {
    return apiClient.post('/auth/login', { email, password });
  },

  // Verify MFA
  verifyMfa: async (userId, mfaCode) => {
    return apiClient.post('/auth/mfa/verify', { userId, mfaCode });
  },

  // Toggle MFA on/off
  toggleMfa: async (enable) => {
    return apiClient.post('/auth/mfa/toggle', { enable });
  },

  // Initiate Phone registration (Mobile Consumer)
  registerPhone: async (phoneNumber, firstName, lastName, email) => {
    return apiClient.post('/auth/register/phone', { phoneNumber, firstName, lastName, email });
  },

  // Verify OTP
  verifyOtp: async (phoneNumber, otpCode, password) => {
    return apiClient.post('/auth/register/otp/verify', { phoneNumber, otpCode, password });
  },

  // Change Password
  changePassword: async (currentPassword, newPassword) => {
    return apiClient.post('/auth/change-password', { currentPassword, newPassword });
  },
};
