import React, { useState } from 'react';
import CustomerLayout from '../../layouts/CustomerLayout';
import Tabs from '../../components/common/Tabs';
import ProfileSettings from '../../components/settings/ProfileSettings';
import SecuritySettings from '../../components/settings/SecuritySettings';
import IndividualKycForm from '../../components/settings/IndividualKycForm';
import OrganizationKybForm from '../../components/settings/OrganizationKybForm';
import ComplianceStatusBadge from '../../components/settings/ComplianceStatusBadge';

import { User, Shield, ShieldCheck, Building2, RefreshCw } from 'lucide-react';
import { useAuth } from '../../context/AuthContext';
import { useOrg } from '../../context/OrgContext';
import { useApiQuery } from '../../hooks/useApiQuery';
import apiClient from '../../services/api/client';
import Button from '../../components/common/Button';

/**
 * Settings, compliance verification, and security preferences workspace.
 */
export default function SettingsPage() {
  const { user } = useAuth();
  const { currentOrg, currentOrgId } = useOrg();
  const [activeTab, setActiveTab] = useState('profile'); // 'profile' | 'security' | 'compliance'

  const settingsTabs = [
    { id: 'profile', label: 'Profile & Account', icon: User },
    { id: 'security', label: 'Security & Password', icon: Shield },
    { id: 'compliance', label: 'Compliance & Verification', icon: ShieldCheck }
  ];

  // Fetch Compliance Profile & Tier
  const {
    data: complianceData,
    loading: complianceLoading,
    refetch: refetchCompliance
  } = useApiQuery(
    () => apiClient.get('/compliance/profile').catch(() => null),
    { deps: [user?.userId, currentOrgId] }
  );

  const currentTier = complianceData?.currentTier || complianceData?.tier || 'Tier1';
  const complianceStatus = complianceData?.status || 'Verified';

  return (
    <CustomerLayout
      title="Settings & Verification"
      subtitle="Manage your profile information, credentials, and KYC/KYB compliance tier"
      headerAction={
        <div className="flex items-center gap-3">
          <ComplianceStatusBadge
            tier={currentTier}
            status={complianceStatus}
          />
          <Button
            variant="outline"
            size="sm"
            icon={RefreshCw}
            onClick={refetchCompliance}
            className="hidden sm:inline-flex"
          >
            Refresh
          </Button>
        </div>
      }
    >
      <div className="space-y-6 max-w-4xl">
        {/* Navigation Tabs */}
        <div className="border-b border-slate-200/80">
          <Tabs
            variant="underlined"
            tabs={settingsTabs}
            activeTab={activeTab}
            onChange={(t) => setActiveTab(t)}
          />
        </div>

        {/* Viewport 1: Profile & Account */}
        {activeTab === 'profile' && (
          <ProfileSettings />
        )}

        {/* Viewport 2: Security & Password */}
        {activeTab === 'security' && (
          <SecuritySettings />
        )}

        {/* Viewport 3: Compliance & KYC/KYB */}
        {activeTab === 'compliance' && (
          <div className="space-y-6">
            <IndividualKycForm
              user={user}
              onSuccess={refetchCompliance}
            />

            <OrganizationKybForm
              currentOrg={currentOrg}
              currentOrgId={currentOrgId}
              onSuccess={refetchCompliance}
            />
          </div>
        )}
      </div>
    </CustomerLayout>
  );
}
