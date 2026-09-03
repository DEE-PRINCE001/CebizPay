import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { useAuth } from './AuthContext';
import { setOrganizationId as setClientOrgId, getOrganizationId } from '../services/api/client';

const STORAGE_ORG_KEY = 'cebizpay_active_org_id';

export const OrgContext = createContext(null);

export function OrgProvider({ children }) {
  const { user, isAuthenticated } = useAuth();
  const [currentOrgId, setCurrentOrgId] = useState(null);
  const [currentOrg, setCurrentOrg] = useState(null);
  const [organizations, setOrganizations] = useState([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!isAuthenticated || !user) {
      setCurrentOrgId(null);
      setCurrentOrg(null);
      setOrganizations([]);
      setClientOrgId(null);
      localStorage.removeItem(STORAGE_ORG_KEY);
      return;
    }

    const storedOrgId = localStorage.getItem(STORAGE_ORG_KEY) || user.organizationId;

    if (storedOrgId) {
      setCurrentOrgId(storedOrgId);
      setClientOrgId(storedOrgId);
      setCurrentOrg({
        id: storedOrgId,
        name: user.organizationName || 'My Organization',
        role: user.role || 'Member'
      });
    } else {
      // Default workspace for single users
      setCurrentOrg({
        id: null,
        name: user.organizationName || 'Personal Workspace',
        role: user.role || 'Member'
      });
    }
  }, [isAuthenticated, user]);

  const switchOrganization = useCallback((orgId, orgData = null) => {
    setCurrentOrgId(orgId);
    setClientOrgId(orgId);
    if (orgId) {
      localStorage.setItem(STORAGE_ORG_KEY, orgId);
    } else {
      localStorage.removeItem(STORAGE_ORG_KEY);
    }

    if (orgData) {
      setCurrentOrg(orgData);
    } else {
      const found = organizations.find((o) => o.id === orgId);
      if (found) {
        setCurrentOrg(found);
      } else {
        setCurrentOrg({ id: orgId, name: 'Active Organization' });
      }
    }
  }, [organizations]);

  const setAvailableOrganizations = useCallback((orgList) => {
    setOrganizations(orgList);
  }, []);

  return (
    <OrgContext.Provider
      value={{
        currentOrgId,
        currentOrg,
        organizations,
        loading,
        switchOrganization,
        setAvailableOrganizations
      }}
    >
      {children}
    </OrgContext.Provider>
  );
}

export function useOrg() {
  const context = useContext(OrgContext);
  if (!context) {
    throw new Error('useOrg must be used within an OrgProvider');
  }
  return context;
}
