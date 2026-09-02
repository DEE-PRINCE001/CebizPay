import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import StatCard from '../../components/common/StatCard';
import Badge from '../../components/common/Badge';
import { formatCurrency, formatDate } from '../../utils/formatters';
import { useToast } from '../../context/ToastContext';
import {
  Wallet,
  Building2,
  Users,
  ShieldCheck,
  AlertTriangle,
  ArrowUpRight,
  RefreshCw,
  FileSpreadsheet,
  CheckCircle2,
  Clock,
  Send,
  Plus
} from 'lucide-react';
import Modal from '../../components/common/Modal';

export default function AdminDashboard() {
  const { showSuccess } = useToast();
  const [showAnnouncementModal, setShowAnnouncementModal] = useState(false);
  const [announcementTitle, setAnnouncementTitle] = useState('');
  const [announcementContent, setAnnouncementContent] = useState('');

  // Platform Metrics summary
  const metrics = {
    totalLiquidity: 184500000.00,
    totalOrgs: 142,
    totalIndividuals: 18940,
    pendingCompliance: 14,
    activeLoansVolume: 32400000.00,
    activeSavingsVolume: 67100000.00,
    monthlyVolume: 412800000.00,
  };

  // Recent compliance submissions queue
  const recentSubmissions = [
    {
      id: 'sub-01',
      name: 'Zenith Matrix Logistics Ltd',
      type: 'Organization (KYB)',
      docType: 'CAC Certificate & MemArt',
      submittedAt: new Date(Date.now() - 1000 * 60 * 18).toISOString(),
      status: 'PENDING',
      riskScore: 'Low (0.12)'
    },
    {
      id: 'sub-02',
      name: 'Olufunke Adeyemi',
      type: 'Individual (KYC)',
      docType: 'NIMC Card & SmartSelfie™',
      submittedAt: new Date(Date.now() - 1000 * 60 * 45).toISOString(),
      status: 'UNDER_REVIEW',
      riskScore: 'Medium (0.45)'
    },
    {
      id: 'sub-03',
      name: 'Quantum Health Technologies',
      type: 'Organization (KYB)',
      docType: 'CAC + Tax Clearance',
      submittedAt: new Date(Date.now() - 1000 * 60 * 120).toISOString(),
      status: 'EDD_REQUIRED',
      riskScore: 'High (0.78)'
    },
    {
      id: 'sub-04',
      name: 'Chukwudi Eze',
      type: 'Individual (KYC)',
      docType: "Driver's License",
      submittedAt: new Date(Date.now() - 1000 * 60 * 240).toISOString(),
      status: 'VERIFIED',
      riskScore: 'Low (0.08)'
    }
  ];

  // 12-Month Platform Volume breakdown
  const monthlyTrends = [
    { month: 'Jan', volume: 280, count: 1200 },
    { month: 'Feb', volume: 310, count: 1450 },
    { month: 'Mar', volume: 295, count: 1390 },
    { month: 'Apr', volume: 340, count: 1620 },
    { month: 'May', volume: 380, count: 1840 },
    { month: 'Jun', volume: 395, count: 1910 },
    { month: 'Jul', volume: 420, count: 2100 },
    { month: 'Aug', volume: 460, count: 2350 },
    { month: 'Sep', volume: 490, count: 2480 },
    { month: 'Oct', volume: 520, count: 2600 },
    { month: 'Nov', volume: 550, count: 2850 },
    { month: 'Dec', volume: 610, count: 3200 }
  ];

  const handlePublishAnnouncement = (e) => {
    e.preventDefault();
    if (!announcementTitle) return;
    showSuccess('Announcement Published', `Platform notice "${announcementTitle}" is now live across all web and mobile apps.`);
    setShowAnnouncementModal(false);
    setAnnouncementTitle('');
    setAnnouncementContent('');
  };

  return (
    <div>
      <PageHeader
        title="Super Admin Control Plane"
        subtitle="Platform-wide liquidity oversight, compliance decisions, multi-tenant governance, and double-entry ledger analytics."
        actions={
          <div className="flex items-center gap-2.5">
            <button
              onClick={() => setShowAnnouncementModal(true)}
              className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 transition-all shadow-xs"
            >
              <Send className="w-3.5 h-3.5" />
              Publish Announcement
            </button>
          </div>
        }
      />

      {/* Top Level Metric KPIs */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        <StatCard
          title="Platform Wallet Liquidity"
          value={formatCurrency(metrics.totalLiquidity)}
          icon={Wallet}
          iconBg="bg-blue-50 text-blue-600"
          trend={{ value: '+8.4%', isPositive: true, label: 'vs last 30d' }}
        />
        <StatCard
          title="Corporate Tenants (B2B)"
          value={metrics.totalOrgs}
          icon={Building2}
          iconBg="bg-purple-50 text-purple-600"
          subtitle="138 Active • 4 Pending KYB"
        />
        <StatCard
          title="Individual / Staff Accounts"
          value={metrics.totalIndividuals.toLocaleString()}
          icon={Users}
          iconBg="bg-emerald-50 text-emerald-600"
          trend={{ value: '+14.2%', isPositive: true, label: 'month-over-month' }}
        />
        <StatCard
          title="Compliance Review Queue"
          value={metrics.pendingCompliance}
          icon={ShieldCheck}
          iconBg="bg-amber-50 text-amber-600"
          subtitle="Requires compliance sign-off"
        />
      </div>

      {/* Secondary Financial Indicators */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-8">
        <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-xs">
          <p className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-1">Monthly Ledger Volume</p>
          <h4 className="text-xl font-bold text-slate-900">{formatCurrency(metrics.monthlyVolume)}</h4>
          <div className="mt-3 flex items-center justify-between text-xs text-slate-500">
            <span>Settled Payouts &amp; Transfers</span>
            <span className="font-semibold text-emerald-600">99.98% SLA</span>
          </div>
        </div>

        <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-xs">
          <p className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-1">Corporate Loan Principal</p>
          <h4 className="text-xl font-bold text-slate-900">{formatCurrency(metrics.activeLoansVolume)}</h4>
          <div className="mt-3 flex items-center justify-between text-xs text-slate-500">
            <span>Active Salary Deduction</span>
            <span className="font-semibold text-blue-600">33% DTI Cap</span>
          </div>
        </div>

        <div className="bg-white p-5 rounded-2xl border border-slate-200/80 shadow-xs">
          <p className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-1">Total Locked Savings &amp; Thrift</p>
          <h4 className="text-xl font-bold text-slate-900">{formatCurrency(metrics.activeSavingsVolume)}</h4>
          <div className="mt-3 flex items-center justify-between text-xs text-slate-500">
            <span>Fixed-Lock &amp; Rotational Ajo</span>
            <span className="font-semibold text-purple-600">Daily Accrual</span>
          </div>
        </div>
      </div>

      {/* Chart & Live Submissions Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* 12-Month Platform Volume Visual Graph */}
        <div className="lg:col-span-2 bg-white p-6 rounded-2xl border border-slate-200/80 shadow-xs">
          <div className="flex items-center justify-between mb-6">
            <div>
              <h3 className="text-sm font-bold text-slate-900">12-Month Platform Volume &amp; Settlement Flow</h3>
              <p className="text-xs text-slate-500 mt-0.5">Aggregated transactional volume across Bank, Peer, and Card rails</p>
            </div>
            <span className="text-xs font-bold text-slate-700 bg-slate-100 px-2.5 py-1 rounded-lg">
              2026 YTD
            </span>
          </div>

          {/* Bar Chart Visualization */}
          <div className="h-48 flex items-end justify-between gap-2 pt-6 pb-2 border-b border-slate-100">
            {monthlyTrends.map((item, idx) => {
              const heightPct = Math.round((item.volume / 650) * 100);
              return (
                <div key={idx} className="flex-1 flex flex-col items-center gap-1.5 group">
                  <div className="w-full bg-slate-100 rounded-t-md h-36 flex items-end p-0.5 relative">
                    <div
                      style={{ height: `${heightPct}%` }}
                      className="w-full bg-blue-600 rounded-t-sm group-hover:bg-blue-700 transition-all"
                    />
                    <div className="opacity-0 group-hover:opacity-100 absolute -top-8 left-1/2 -translate-x-1/2 bg-slate-900 text-white text-[10px] font-mono py-1 px-1.5 rounded shadow-md pointer-events-none transition-opacity whitespace-nowrap z-10">
                      ₦{item.volume}M
                    </div>
                  </div>
                  <span className="text-[10px] font-semibold text-slate-400 group-hover:text-slate-800 transition-colors">
                    {item.month}
                  </span>
                </div>
              );
            })}
          </div>

          <div className="mt-4 flex items-center justify-between text-xs text-slate-500">
            <div className="flex items-center gap-4">
              <span className="flex items-center gap-1.5">
                <span className="w-2.5 h-2.5 rounded-sm bg-blue-600" />
                Gross Volume (₦ Millions)
              </span>
            </div>
            <span className="font-semibold text-slate-700">Total YTD: ₦4.78 Billion</span>
          </div>
        </div>

        {/* Pending Verification & CDD Queue */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200/80 shadow-xs flex flex-col justify-between">
          <div>
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-sm font-bold text-slate-900">Compliance &amp; CDD Queue</h3>
              <span className="text-xs bg-amber-100 text-amber-800 font-bold px-2 py-0.5 rounded-full">
                {recentSubmissions.length} Pending
              </span>
            </div>

            <div className="space-y-3">
              {recentSubmissions.map((item) => (
                <div key={item.id} className="p-3 bg-slate-50 rounded-xl border border-slate-100 hover:border-slate-200 transition-all text-xs">
                  <div className="flex items-start justify-between gap-2 mb-1">
                    <h5 className="font-bold text-slate-900 truncate">{item.name}</h5>
                    <Badge status={item.status} size="sm" />
                  </div>
                  <div className="flex items-center justify-between text-slate-500 text-[11px]">
                    <span>{item.docType}</span>
                    <span className="font-mono text-slate-400">{item.riskScore}</span>
                  </div>
                </div>
              ))}
            </div>
          </div>

          <a
            href="/admin/compliance"
            className="mt-4 w-full py-2.5 bg-slate-100 hover:bg-slate-200 text-slate-800 text-xs font-bold rounded-xl transition-colors text-center block"
          >
            Review All Compliance Documents →
          </a>
        </div>
      </div>

      {/* Publish Announcement Modal */}
      <Modal
        isOpen={showAnnouncementModal}
        onClose={() => setShowAnnouncementModal(false)}
        title="Publish Platform Announcement"
        subtitle="This notice will be broadcasted to all mobile and web users across the CebizPay ecosystem."
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button
              onClick={() => setShowAnnouncementModal(false)}
              className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl hover:bg-slate-50"
            >
              Cancel
            </button>
            <button
              onClick={handlePublishAnnouncement}
              className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs flex items-center gap-1.5"
            >
              <Send className="w-3.5 h-3.5" />
              Publish Notice
            </button>
          </div>
        }
      >
        <form onSubmit={handlePublishAnnouncement} className="space-y-4 text-left">
          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1.5">Announcement Title</label>
            <input
              type="text"
              required
              value={announcementTitle}
              onChange={(e) => setAnnouncementTitle(e.target.value)}
              placeholder="e.g. Scheduled Network Maintenance / CBN CDD Guidelines Update"
              className="w-full px-3.5 py-2.5 text-xs bg-white border border-slate-200 rounded-xl focus:border-blue-600 focus:ring-2 focus:ring-blue-500/20 outline-hidden transition-all"
            />
          </div>

          <div>
            <label className="block text-xs font-semibold text-slate-700 mb-1.5">Notice Content</label>
            <textarea
              rows={4}
              required
              value={announcementContent}
              onChange={(e) => setAnnouncementContent(e.target.value)}
              placeholder="Provide clear, concise details regarding the platform update or regulation..."
              className="w-full px-3.5 py-2 text-xs bg-white border border-slate-200 rounded-xl focus:border-blue-600 focus:ring-2 focus:ring-blue-500/20 outline-hidden transition-all"
            />
          </div>

          <div className="p-3 bg-blue-50 rounded-xl border border-blue-100 text-xs text-blue-900">
            Announcements are signed with your Super Admin cryptographic audit identity.
          </div>
        </form>
      </Modal>
    </div>
  );
}
