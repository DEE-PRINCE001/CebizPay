import React, { useState } from 'react';
import CustomerLayout from '../../layouts/CustomerLayout';
import Tabs from '../../components/common/Tabs';
import StatCard from '../../components/common/StatCard';
import Button from '../../components/common/Button';

import SavingsPlanList from '../../components/savings/SavingsPlanList';
import CreateSavingsPlanModal from '../../components/savings/CreateSavingsPlanModal';
import SavingsPlanDetailModal from '../../components/savings/SavingsPlanDetailModal';
import DepositSavingsModal from '../../components/savings/DepositSavingsModal';
import WithdrawSavingsModal from '../../components/savings/WithdrawSavingsModal';

import ThriftCycleList from '../../components/savings/ThriftCycleList';
import CreateThriftGroupModal from '../../components/savings/CreateThriftGroupModal';
import JoinThriftModal from '../../components/savings/JoinThriftModal';
import ThriftGroupDetailModal from '../../components/savings/ThriftGroupDetailModal';

import { PiggyBank, Coins, Plus, KeyRound, TrendingUp, ShieldCheck, RefreshCw } from 'lucide-react';
import { useApiQuery } from '../../hooks/useApiQuery';
import { useOrg } from '../../context/OrgContext';
import apiClient from '../../services/api/client';

/**
 * Savings and rotational thrift (Ajo/Esusu) workspace.
 */
export default function SavingsPage() {
  const { currentOrgId } = useOrg();
  const [activeTab, setActiveTab] = useState('savings'); // 'savings' | 'thrift'

  // Savings Modal State
  const [isCreateSavingsOpen, setIsCreateSavingsOpen] = useState(false);
  const [selectedSavingsPlan, setSelectedSavingsPlan] = useState(null);
  const [depositPlan, setDepositPlan] = useState(null);
  const [withdrawPlan, setWithdrawPlan] = useState(null);

  // Thrift Modal State
  const [isCreateThriftOpen, setIsCreateThriftOpen] = useState(false);
  const [isJoinThriftOpen, setIsJoinThriftOpen] = useState(false);
  const [selectedThriftGroup, setSelectedThriftGroup] = useState(null);

  const mainTabs = [
    { id: 'savings', label: 'Target & Locked Savings', icon: PiggyBank },
    { id: 'thrift', label: 'Ajo / Esusu Thrift Circles', icon: Coins }
  ];

  // 1. Fetch User Savings Accounts
  const {
    data: savingsData,
    loading: savingsLoading,
    error: savingsError,
    refetch: refetchSavings
  } = useApiQuery(
    () => apiClient.get('/work/savings').catch(() => []),
    { deps: [currentOrgId] }
  );

  // 2. Fetch User Thrift Groups
  const {
    data: thriftData,
    loading: thriftLoading,
    error: thriftError,
    refetch: refetchThrift
  } = useApiQuery(
    () => apiClient.get('/work/thrift').catch(() => []),
    { deps: [currentOrgId] }
  );

  const savingsPlans = Array.isArray(savingsData) ? savingsData : [];
  const thriftGroups = Array.isArray(thriftData) ? thriftData : [];

  const totalSaved = savingsPlans.reduce((acc, p) => acc + (p.principalBalance || 0), 0);
  const totalAccruedInterest = savingsPlans.reduce((acc, p) => acc + (p.accruedInterest || 0), 0);

  const handleRefreshAll = () => {
    refetchSavings();
    refetchThrift();
  };

  const formatAmount = (amt) => {
    return new Intl.NumberFormat('en-NG', {
      style: 'currency',
      currency: 'NGN',
      minimumFractionDigits: 2
    }).format(amt || 0);
  };

  return (
    <CustomerLayout
      title="Savings & Rotational Thrift"
      subtitle="Goal-oriented target savings, high-yield fixed locks, and peer Ajo/Esusu circles"
      headerAction={
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            icon={RefreshCw}
            onClick={handleRefreshAll}
            className="hidden sm:inline-flex"
          >
            Refresh
          </Button>

          {activeTab === 'savings' && (
            <Button
              variant="primary"
              size="sm"
              icon={Plus}
              onClick={() => setIsCreateSavingsOpen(true)}
            >
              Create Savings Plan
            </Button>
          )}

          {activeTab === 'thrift' && (
            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                size="sm"
                icon={KeyRound}
                onClick={() => setIsJoinThriftOpen(true)}
              >
                Join Circle
              </Button>
              <Button
                variant="primary"
                size="sm"
                icon={Plus}
                onClick={() => setIsCreateThriftOpen(true)}
              >
                Create Circle
              </Button>
            </div>
          )}
        </div>
      }
    >
      <div className="space-y-6">
        {/* Metric Cards */}
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
          <StatCard
            icon={PiggyBank}
            label="Total Saved Balance"
            value={formatAmount(totalSaved)}
            loading={savingsLoading}
          />
          <StatCard
            icon={TrendingUp}
            label="Accrued Interest"
            value={formatAmount(totalAccruedInterest)}
            loading={savingsLoading}
          />
          <StatCard
            icon={Coins}
            label="Active Thrift Circles"
            value={thriftGroups.length.toString()}
            loading={thriftLoading}
          />
          <StatCard
            icon={ShieldCheck}
            label="Interest Protection"
            value="12.0% p.a."
          />
        </div>

        {/* Sub-Navigation Tabs */}
        <div className="border-b border-slate-200/80">
          <Tabs
            variant="underlined"
            tabs={mainTabs}
            activeTab={activeTab}
            onChange={(t) => setActiveTab(t)}
          />
        </div>

        {/* Viewport 1: Savings Plans */}
        {activeTab === 'savings' && (
          <SavingsPlanList
            plans={savingsPlans}
            loading={savingsLoading}
            error={savingsError}
            onRetry={refetchSavings}
            onViewPlan={(plan) => setSelectedSavingsPlan(plan)}
            onCreatePlan={() => setIsCreateSavingsOpen(true)}
          />
        )}

        {/* Viewport 2: Thrift Circles */}
        {activeTab === 'thrift' && (
          <ThriftCycleList
            groups={thriftGroups}
            loading={thriftLoading}
            error={thriftError}
            onRetry={refetchThrift}
            onViewGroup={(grp) => setSelectedThriftGroup(grp)}
            onCreateGroup={() => setIsCreateThriftOpen(true)}
            onJoinGroup={() => setIsJoinThriftOpen(true)}
          />
        )}
      </div>

      {/* Savings Modals */}
      <CreateSavingsPlanModal
        isOpen={isCreateSavingsOpen}
        onClose={() => setIsCreateSavingsOpen(false)}
        organizationId={currentOrgId}
        onSuccess={handleRefreshAll}
      />

      <SavingsPlanDetailModal
        isOpen={!!selectedSavingsPlan}
        onClose={() => setSelectedSavingsPlan(null)}
        plan={selectedSavingsPlan}
        onDeposit={(plan) => setDepositPlan(plan)}
        onWithdraw={(plan) => setWithdrawPlan(plan)}
      />

      <DepositSavingsModal
        isOpen={!!depositPlan}
        onClose={() => setDepositPlan(null)}
        plan={depositPlan}
        onSuccess={handleRefreshAll}
      />

      <WithdrawSavingsModal
        isOpen={!!withdrawPlan}
        onClose={() => setWithdrawPlan(null)}
        plan={withdrawPlan}
        onSuccess={handleRefreshAll}
      />

      {/* Thrift Modals */}
      <CreateThriftGroupModal
        isOpen={isCreateThriftOpen}
        onClose={() => setIsCreateThriftOpen(false)}
        organizationId={currentOrgId}
        onSuccess={handleRefreshAll}
      />

      <JoinThriftModal
        isOpen={isJoinThriftOpen}
        onClose={() => setIsJoinThriftOpen(false)}
        onSuccess={handleRefreshAll}
      />

      <ThriftGroupDetailModal
        isOpen={!!selectedThriftGroup}
        onClose={() => setSelectedThriftGroup(null)}
        group={selectedThriftGroup}
        onRefresh={handleRefreshAll}
      />
    </CustomerLayout>
  );
}
