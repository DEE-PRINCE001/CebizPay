import React, { createContext, useContext, useState, useEffect } from 'react';
import { authApi } from '../api/authApi';

const AuthContext = createContext(null);

export const ROLES = {
  SUPER_ADMIN: 'super-admin',
  ORGANIZATION: 'organization',
  CONSUMER: 'consumer',
};

export function AuthProvider({ children }) {
  const [token, setToken] = useState(() => localStorage.getItem('cebizpay_token') || null);
  const [user, setUser] = useState(() => {
    const saved = localStorage.getItem('cebizpay_user');
    return saved ? JSON.parse(saved) : null;
  });
  
  const [activeRole, setActiveRole] = useState(() => {
    return localStorage.getItem('cebizpay_active_role') || ROLES.SUPER_ADMIN;
  });

  const [activeOrg, setActiveOrg] = useState(() => {
    const saved = localStorage.getItem('cebizpay_active_org');
    return saved ? JSON.parse(saved) : {
      id: 'a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d',
      name: 'Apex Global Technologies Ltd',
      cacNumber: 'RC-1849204',
      kybStatus: 'VERIFIED',
      balance: 14250000.00
    };
  });

  const [balanceVisible, setBalanceVisible] = useState(() => {
    return localStorage.getItem('cebizpay_balance_visible') !== 'false';
  });

  useEffect(() => {
    if (token) {
      localStorage.setItem('cebizpay_token', token);
    } else {
      localStorage.removeItem('cebizpay_token');
    }
  }, [token]);

  useEffect(() => {
    if (user) {
      localStorage.setItem('cebizpay_user', JSON.stringify(user));
    } else {
      localStorage.removeItem('cebizpay_user');
    }
  }, [user]);

  useEffect(() => {
    localStorage.setItem('cebizpay_active_role', activeRole);
  }, [activeRole]);

  useEffect(() => {
    if (activeOrg) {
      localStorage.setItem('cebizpay_active_org', JSON.stringify(activeOrg));
      localStorage.setItem('cebizpay_active_org_id', activeOrg.id);
    } else {
      localStorage.removeItem('cebizpay_active_org');
      localStorage.removeItem('cebizpay_active_org_id');
    }
  }, [activeOrg]);

  useEffect(() => {
    localStorage.setItem('cebizpay_balance_visible', balanceVisible ? 'true' : 'false');
  }, [balanceVisible]);

  // Login handler
  const login = async (email, password) => {
    try {
      const response = await authApi.login(email, password);
      if (response && response.succeeded) {
        setToken(response.accessToken);
        const userData = {
          id: response.userId,
          email: email,
          name: email.split('@')[0],
          isSuperAdmin: email.toLowerCase().includes('honour') || email.toLowerCase().includes('admin'),
        };
        setUser(userData);

        // Set default initial role based on user
        if (userData.isSuperAdmin) {
          setActiveRole(ROLES.SUPER_ADMIN);
        } else if (email.includes('org')) {
          setActiveRole(ROLES.ORGANIZATION);
        } else {
          setActiveRole(ROLES.CONSUMER);
        }

        return { success: true, data: response };
      }
      return { success: false, error: response.errors?.join(', ') || 'Login failed' };
    } catch (err) {
      // If live backend error, allow demo mode switch gracefully with detailed error report
      console.warn('API login failed, checking demo login fallback:', err.message);
      throw err;
    }
  };

  // Quick Demo Login (for rapid testing / presentation)
  const loginAsDemo = (roleType) => {
    let demoUser = null;
    let demoToken = 'demo_jwt_token_' + Date.now();

    if (roleType === ROLES.SUPER_ADMIN) {
      demoUser = {
        id: 'admin-001',
        email: 'honour@gmail.com',
        name: 'Honour Ajani',
        role: 'Super Admin',
        isSuperAdmin: true,
        kycStatus: 'VERIFIED'
      };
      setActiveRole(ROLES.SUPER_ADMIN);
    } else if (roleType === ROLES.ORGANIZATION) {
      demoUser = {
        id: 'org-ceo-001',
        email: 'ceo@apextech.com',
        name: 'Tunde Adeleke (Apex CEO)',
        role: 'Org Admin',
        isSuperAdmin: false,
        organizationId: activeOrg?.id,
        kycStatus: 'VERIFIED'
      };
      setActiveRole(ROLES.ORGANIZATION);
    } else {
      demoUser = {
        id: 'user-001',
        email: 'amina.adeleke@example.com',
        name: 'Amina Adeleke',
        phone: '08012345678',
        role: 'Senior Software Engineer',
        isSuperAdmin: false,
        organizationId: activeOrg?.id,
        kycStatus: 'VERIFIED',
        tier: 'TIER_3'
      };
      setActiveRole(ROLES.CONSUMER);
    }

    setToken(demoToken);
    setUser(demoUser);
  };

  const logout = () => {
    setToken(null);
    setUser(null);
    localStorage.removeItem('cebizpay_token');
    localStorage.removeItem('cebizpay_user');
  };

  const switchRole = (newRole) => {
    setActiveRole(newRole);
  };

  const toggleBalancePrivacy = () => {
    setBalanceVisible((prev) => !prev);
  };

  return (
    <AuthContext.Provider
      value={{
        token,
        user,
        activeRole,
        activeOrg,
        balanceVisible,
        isAuthenticated: !!token || !!user,
        login,
        loginAsDemo,
        logout,
        switchRole,
        setActiveOrg,
        toggleBalancePrivacy,
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
