import React, { useState, useEffect } from 'react';
import PageHeader from '../../components/common/PageHeader';
import StatCard from '../../components/common/StatCard';
import Badge from '../../components/common/Badge';
import { formatCurrency, formatPercent, formatDate } from '../../utils/formatters';
import { adminApi } from '../../api/adminApi';
import {
  Landmark,
  ShieldCheck,
  Activity,
  AlertTriangle,
  Building,
  Users,
  Wallet,
  ArrowUpRight,
  TrendingUp,
  CreditCard,
  Send,
} from 'lucide-react';
import { Link } from 'react-router-dom';

export default function AdminDashboard() {
  const [stats, setStats] = useState({
    totalPlatformLiquidity: 482500000.0,
    activeOrganizationsCount: 42,
    activeConsumersCount: 1845,
    pendingKycKybReviewCount: 14,
    monthlyVolume: 1248000000.0,
    reconciliationDiscrepanciesCount: 2,
    reserveLedgerHealth: '100% BALANCED',
  });

  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    const fetchDashboardStats = async () => {
      try {
        const res = await adminApi.getReconciliationRecords();
        if (Array.isArray(res)) {
          setStats((prev) => ({
            ...prev,
            reconciliationDiscrepanciesCount: res.length,
          }));
        }
      } catch (err) {
        console.warn('Backend dashboard stats fetch fallback:', err);
      }
    };
    fetchDashboardStats();
  }, []);

  return (
    <div>
      <PageHeader
        title="Super Admin: System Overview"
        subtitle="Platform-wide liquidity monitor, Central Double-Entry Ledger health, and CBN CDD/EDD regulatory queues."
        actions={
          <div className="flex items-center gap-2">
            <Link
              to="/admin/reconciliation"
              className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-slate-800 bg-white border border-slate-200 rounded-xl hover:bg-slate-50 shadow-xs"
            >
              <Activity className="w-3.5 h-3.5 text-blue-600" />
              Reconciliation Plane
            </Link>
            <Link
              to="/admin/compliance"
              className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs"
            >
              <ShieldCheck className="w-3.5 h-3.5" />
              Compliance CDD Queue
            </Link>
          </div>
        }
      />

      {/* KPI Stats Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
        <StatCard
          title="Aggregated Liquidity Vaults"
          value={formatCurrency(stats.totalPlatformLiquidity)}
          subtext="Central Bank of Nigeria settlement backing"
          icon={Landmark}
          trend={{ direction: 'up', text: '+18.4% vs last mo' }}
        />
        <StatCard
          title="Monthly Transaction Volume"
          value={formatCurrency(stats.monthlyVolume)}
          subtext="NIP, Card Checkout &amp; Internal transfers"
          icon={TrendingUp}
          trend={{ direction: 'up', text: '+32.1% YoY' }}
        />
        <StatCard
          title="Active Corporate Tenants"
          value={stats.activeOrganizationsCount.toString()}
          subtext="Verified KYB organizations on payroll"
          icon={Building}
        />
        <StatCard
          title="CDD / KYB Reviews Required"
          value={stats.pendingKycKybReviewCount.toString()}
          subtext="Pending human officer review"
          icon={AlertTriangle}
          trend={{ direction: 'down', text: 'Action required' }}
        />
      </div>

      {/* Quick Navigation Cards */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
        <Link
          to="/admin/fees"
          className="p-6 bg-white rounded-3xl border border-slate-200/80 hover:border-blue-300 transition-all shadow-xs flex flex-col justify-between group"
        >
          <div>
            <div className="w-10 h-10 rounded-2xl bg-blue-50 text-blue-600 flex items-center justify-center font-bold mb-3 group-hover:scale-105 transition-transform">
              <CreditCard className="w-5 h-5" />
            </div>
            <h3 className="font-bold text-slate-900 text-sm mb-1">Fee Policy Engine</h3>
            <p className="text-xs text-slate-500 leading-relaxed">
              Configure peer transfer, interbank payout, and platform bearer fee economics.
            </p>
          </div>
          <span className="text-xs font-bold text-blue-600 mt-4 flex items-center gap-1 group-hover:translate-x-1 transition-transform">
            Configure Policies →
          </span>
        </Link>

        <Link
          to="/admin/savings-policies"
          className="p-6 bg-white rounded-3xl border border-slate-200/80 hover:border-blue-300 transition-all shadow-xs flex flex-col justify-between group"
        >
          <div>
            <div className="w-10 h-10 rounded-2xl bg-emerald-50 text-emerald-600 flex items-center justify-center font-bold mb-3 group-hover:scale-105 transition-transform">
              <TrendingUp className="w-5 h-5" />
            </div>
            <h3 className="font-bold text-slate-900 text-sm mb-1">Savings Interest Yields</h3>
            <p className="text-xs text-slate-500 leading-relaxed">
              Manage annual yields (8.5%–14.5% p.a.) and early exit liquidation penalties.
            </p>
          </div>
          <span className="text-xs font-bold text-emerald-600 mt-4 flex items-center gap-1 group-hover:translate-x-1 transition-transform">
            Configure Savings Tiers →
          </span>
        </Link>

        <Link
          to="/admin/audit-logs"
          className="p-6 bg-white rounded-3xl border border-slate-200/80 hover:border-blue-300 transition-all shadow-xs flex flex-col justify-between group"
        >
          <div>
            <div className="w-10 h-10 rounded-2xl bg-purple-50 text-purple-600 flex items-center justify-center font-bold mb-3 group-hover:scale-105 transition-transform">
              <ShieldCheck className="w-5 h-5" />
            </div>
            <h3 className="font-bold text-slate-900 text-sm mb-1">Audit Trail Telemetry</h3>
            <p className="text-xs text-slate-500 leading-relaxed">
              Inspect immutable ledger entries, correlation IDs, and actor footprints.
            </p>
          </div>
          <span className="text-xs font-bold text-purple-600 mt-4 flex items-center gap-1 group-hover:translate-x-1 transition-transform">
            View Audit Trail →
          </span>
        </Link>
      </div>
    </div>
  );
}
