import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import apiClient, {
  setAuthToken,
  setRefreshToken,
  clearAuthTokens,
  setOrganizationId,
  onAuthUnauthorized,
  onTokenUpdate
} from '../services/api/client';

const STORAGE_KEYS = {
  ACCESS_TOKEN: 'cebizpay_access_token',
  REFRESH_TOKEN: 'cebizpay_refresh_token',
  USER: 'cebizpay_user',
  ORGANIZATION_ID: 'cebizpay_org_id'
};

/**
 * Safely decodes a JWT token payload without third-party dependencies.
 */
function decodeJwtPayload(token) {
  try {
    if (!token || typeof token !== 'string') return {};
    const parts = token.split('.');
    if (parts.length !== 3) return {};
    const base64Url = parts[1];
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    );
    return JSON.parse(jsonPayload);
  } catch {
    return {};
  }
}

export const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [accessToken, setAccessToken] = useState(null);
  const [refreshTokenState, setRefreshTokenState] = useState(null);
  const [mfaChallenge, setMfaChallenge] = useState(null);
  const [loading, setLoading] = useState(true);

  // Initialize session from storage on app load
  useEffect(() => {
    try {
      const storedToken = localStorage.getItem(STORAGE_KEYS.ACCESS_TOKEN);
      const storedRefreshToken = localStorage.getItem(STORAGE_KEYS.REFRESH_TOKEN);
      const storedUser = localStorage.getItem(STORAGE_KEYS.USER);
      const storedOrgId = localStorage.getItem(STORAGE_KEYS.ORGANIZATION_ID);

      if (storedToken && storedUser) {
        const parsedUser = JSON.parse(storedUser);
        setUser(parsedUser);
        setAccessToken(storedToken);
        setRefreshTokenState(storedRefreshToken);

        setAuthToken(storedToken);
        setRefreshToken(storedRefreshToken);

        if (storedOrgId) {
          setOrganizationId(storedOrgId);
        } else if (parsedUser.organizationId) {
          setOrganizationId(parsedUser.organizationId);
        }
      }
    } catch (e) {
      console.error('Failed to restore authentication session:', e);
      clearStorage();
    } finally {
      setLoading(false);
    }
  }, []);

  const clearStorage = () => {
    localStorage.removeItem(STORAGE_KEYS.ACCESS_TOKEN);
    localStorage.removeItem(STORAGE_KEYS.REFRESH_TOKEN);
    localStorage.removeItem(STORAGE_KEYS.USER);
    localStorage.removeItem(STORAGE_KEYS.ORGANIZATION_ID);
  };

  // Sync token rotations from API client interceptor
  useEffect(() => {
    const unsubTokenUpdate = onTokenUpdate(({ accessToken: newAccess, refreshToken: newRefresh }) => {
      setAccessToken(newAccess);
      localStorage.setItem(STORAGE_KEYS.ACCESS_TOKEN, newAccess);
      if (newRefresh) {
        setRefreshTokenState(newRefresh);
        localStorage.setItem(STORAGE_KEYS.REFRESH_TOKEN, newRefresh);
      }
    });

    const unsubUnauthorized = onAuthUnauthorized(() => {
      logout();
    });

    return () => {
      unsubTokenUpdate();
      unsubUnauthorized();
    };
  }, []);

  const handleAuthSuccess = useCallback((data, inputEmail = '') => {
    const token = data.accessToken;
    const claims = decodeJwtPayload(token);

    const email =
      data.email ||
      claims.email ||
      claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] ||
      inputEmail ||
      '';

    const userId =
      data.userId ||
      claims.nameid ||
      claims.sub ||
      claims['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'] ||
      '';

    const role =
      data.role ||
      claims.role ||
      claims['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ||
      'Member';

    const organizationId =
      data.organizationId ||
      claims.organization_id ||
      claims.org_id ||
      claims.organizationId ||
      null;

    const firstName = data.firstName || claims.given_name || '';
    const lastName = data.lastName || claims.family_name || '';
    const fullName =
      data.fullName ||
      `${firstName} ${lastName}`.trim() ||
      claims.name ||
      email.split('@')[0] ||
      'User';

    const userData = {
      userId,
      email,
      firstName,
      lastName,
      fullName,
      role,
      organizationId
    };

    setUser(userData);
    setAccessToken(data.accessToken);
    setRefreshTokenState(data.refreshToken);
    setMfaChallenge(null);

    setAuthToken(data.accessToken);
    setRefreshToken(data.refreshToken);

    localStorage.setItem(STORAGE_KEYS.ACCESS_TOKEN, data.accessToken);
    if (data.refreshToken) {
      localStorage.setItem(STORAGE_KEYS.REFRESH_TOKEN, data.refreshToken);
    }
    localStorage.setItem(STORAGE_KEYS.USER, JSON.stringify(userData));

    if (organizationId) {
      setOrganizationId(organizationId);
      localStorage.setItem(STORAGE_KEYS.ORGANIZATION_ID, organizationId);
    }

    return userData;
  }, []);

  /**
   * Authenticates user via email and password.
   */
  const login = useCallback(async (email, password) => {
    const response = await apiClient.post('/auth/login', { email, password });

    if (response.requiresMfa) {
      setMfaChallenge({
        challengeToken: response.mfaChallengeToken,
        email: response.email || email,
        userId: response.userId
      });
      return { requiresMfa: true, mfaChallengeToken: response.mfaChallengeToken };
    }

    const userData = handleAuthSuccess(response, email);
    return { requiresMfa: false, user: userData };
  }, [handleAuthSuccess]);

  /**
   * Verifies 6-digit TOTP / MFA code using active mfaChallenge.
   */
  const verifyMfa = useCallback(async (code) => {
    if (!mfaChallenge?.challengeToken) {
      throw new Error('No active MFA challenge found.');
    }

    const response = await apiClient.post('/auth/mfa/verify', {
      mfaChallengeToken: mfaChallenge.challengeToken,
      code
    });

    const userData = handleAuthSuccess(response, mfaChallenge.email);
    return userData;
  }, [mfaChallenge, handleAuthSuccess]);

  const cancelMfa = useCallback(() => {
    setMfaChallenge(null);
  }, []);

  const logout = useCallback(async () => {
    try {
      const activeRefresh = refreshTokenState || localStorage.getItem(STORAGE_KEYS.REFRESH_TOKEN);
      if (activeRefresh) {
        await apiClient.revokeToken(activeRefresh).catch(() => {});
      }
    } catch {
      // Best effort revocation
    } finally {
      setUser(null);
      setAccessToken(null);
      setRefreshTokenState(null);
      setMfaChallenge(null);
      clearAuthTokens();
      clearStorage();
    }
  }, [refreshTokenState]);

  const updateUserData = useCallback((updatedFields) => {
    setUser((prev) => {
      if (!prev) return prev;
      const updated = { ...prev, ...updatedFields };
      localStorage.setItem(STORAGE_KEYS.USER, JSON.stringify(updated));
      return updated;
    });
  }, []);

  return (
    <AuthContext.Provider
      value={{
        user,
        accessToken,
        refreshToken: refreshTokenState,
        isAuthenticated: !!accessToken && !!user,
        loading,
        mfaChallenge,
        login,
        verifyMfa,
        cancelMfa,
        logout,
        updateUserData,
        handleAuthSuccess
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
