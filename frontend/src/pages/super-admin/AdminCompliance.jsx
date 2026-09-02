import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Tabs from '../../components/common/Tabs';
import Modal from '../../components/common/Modal';
import { useToast } from '../../context/ToastContext';
import { formatDate } from '../../utils/formatters';
import {
  ShieldCheck,
  Building,
  User,
  CheckCircle,
  XCircle,
  AlertTriangle,
  FileText,
  Search,
  Eye,
  Sliders,
  ExternalLink,
  Lock,
  Unlock,
  ShieldAlert
} from 'lucide-react';

export default function AdminCompliance() {
  const [activeTab, setActiveTab] = useState('kyb'); // 'kyb' | 'kyc' | 'edd' | 'restrictions'
  const [selectedRecord, setSelectedRecord] = useState(null);
  const [showReviewModal, setShowReviewModal] = useState(false);
  const [reviewDecision, setReviewDecision] = useState('APPROVED');
  const [reviewReason, setReviewReason] = useState('');
  const [showRestrictionModal, setShowRestrictionModal] = useState(false);
  const [restrictionLimit, setRestrictionLimit] = useState('500000');
  const { showSuccess, showError } = useToast();

  // Mock data representing KYB & KYC compliance profiles
  const [kybSubmissions, setKybSubmissions] = useState([
    {
      id: 'org-101',
      companyName: 'Apex Global Technologies Ltd',
      cacNumber: 'RC-1849204',
      email: 'contact@apextech.com',
      tin: '22839401-0001',
      directorsCount: 3,
      uboPercentage: '65% (Tunde Adeleke)',
      cacDocUrl: 'https://docs.cebizpay.com/cac/apex-rc1849204.pdf',
      status: 'VERIFIED',
      riskRating: 'LOW',
      cddStatus: 'STANDARD',
      submittedAt: '2026-08-14T10:30:00Z'
    },
    {
      id: 'org-102',
      companyName: 'Zenith Matrix Logistics Ltd',
      cacNumber: 'RC-9042183',
      email: 'ops@zenithmatrix.ng',
      tin: '88492019-0001',
      directorsCount: 2,
      uboPercentage: '51% (Emeka Obi)',
      cacDocUrl: 'https://docs.cebizpay.com/cac/zenith-rc9042183.pdf',
      status: 'PENDING',
      riskRating: 'LOW',
      cddStatus: 'STANDARD',
      submittedAt: '2026-09-01T08:15:00Z'
    },
    {
      id: 'org-103',
      companyName: 'Quantum Health International',
      cacNumber: 'RC-7721849',
      email: 'compliance@quantumhealth.org',
      tin: '19482013-0001',
      directorsCount: 4,
      uboPercentage: '40% (Cross-border Entity)',
      cacDocUrl: 'https://docs.cebizpay.com/cac/quantum-rc7721849.pdf',
      status: 'EDD_REQUIRED',
      riskRating: 'HIGH',
      cddStatus: 'ENHANCED',
      submittedAt: '2026-08-28T14:45:00Z'
    }
  ]);

  const [kycSubmissions, setKycSubmissions] = useState([
    {
      id: 'usr-201',
      fullName: 'Amina Adeleke',
      email: 'amina.adeleke@example.com',
      phone: '08012345678',
      bvn: '22345678901',
      nin: '12345678901',
      docType: 'NIMC_CARD',
      docNumber: 'NIN-9928341',
      tierLevel: 'TIER_3',
      status: 'VERIFIED',
      riskRating: 'LOW',
      livenessMatch: '99.4% (Smile ID)',
      amlCheck: 'CLEAN (Dojah Watchlist)'
    },
    {
      id: 'usr-202',
      fullName: 'Ibrahim Danjuma',
      email: 'ibrahim.d@example.com',
      phone: '08098765432',
      bvn: '22883399112',
      nin: '98765432109',
      docType: 'DRIVERS_LICENSE',
      docNumber: 'DL-8839201A',
      tierLevel: 'TIER_2',
      status: 'PENDING',
      riskRating: 'LOW',
      livenessMatch: '98.1% (Smile ID)',
      amlCheck: 'CLEAN (Dojah Watchlist)'
    },
    {
      id: 'usr-203',
      fullName: 'Chief Marcus Okoro',
      email: 'm.okoro@crossriver.gov.ng',
      phone: '08033221144',
      bvn: '22001144778',
      nin: '44556677889',
      docType: 'INTERNATIONAL_PASSPORT',
      docNumber: 'A09823145',
      tierLevel: 'TIER_3',
      status: 'EDD_REQUIRED',
      riskRating: 'HIGH',
      livenessMatch: '97.6% (Smile ID)',
      amlCheck: 'PEP MATCH (Public Official Category B)'
    }
  ]);

  const handleOpenReview = (record) => {
    setSelectedRecord(record);
    setReviewDecision(record.status === 'PENDING' ? 'VERIFIED' : record.status);
    setReviewReason('');
    setShowReviewModal(true);
  };

  const handleSaveDecision = () => {
    if (!selectedRecord) return;
    if (activeTab === 'kyb') {
      setKybSubmissions((prev) =>
        prev.map((item) =>
          item.id === selectedRecord.id ? { ...item, status: reviewDecision } : item
        )
      );
    } else {
      setKycSubmissions((prev) =>
        prev.map((item) =>
          item.id === selectedRecord.id ? { ...item, status: reviewDecision } : item
        )
      );
    }
    showSuccess(
      'Compliance Decision Executed',
      `${selectedRecord.companyName || selectedRecord.fullName} marked as ${reviewDecision}.`
    );
    setShowReviewModal(false);
  };

  const kybColumns = [
    {
      header: 'Organization Name',
      accessor: 'companyName',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 block">{row.companyName}</span>
          <span className="text-[11px] text-slate-400 font-mono">{row.cacNumber}</span>
        </div>
      )
    },
    {
      header: 'Tax ID (TIN)',
      accessor: 'tin',
      render: (row) => <span className="font-mono text-slate-600">{row.tin}</span>
    },
    {
      header: 'Beneficial Ownership',
      accessor: 'uboPercentage',
      render: (row) => <span className="text-slate-700 font-medium">{row.uboPercentage}</span>
    },
    {
      header: 'Risk Level',
      accessor: 'riskRating',
      render: (row) => <Badge status={row.riskRating} size="sm" />
    },
    {
      header: 'KYB Status',
      accessor: 'status',
      render: (row) => <Badge status={row.status} size="sm" />
    },
    {
      header: 'Actions',
      align: 'right',
      render: (row) => (
        <button
          onClick={() => handleOpenReview(row)}
          className="px-3 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-800 rounded-lg text-xs font-bold transition-colors"
        >
          Review Evidence
        </button>
      )
    }
  ];

  const kycColumns = [
    {
      header: 'Individual Name',
      accessor: 'fullName',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 block">{row.fullName}</span>
          <span className="text-[11px] text-slate-400">{row.email}</span>
        </div>
      )
    },
    {
      header: 'Tier Level',
      accessor: 'tierLevel',
      render: (row) => <Badge status={row.tierLevel} size="sm" />
    },
    {
      header: 'Verified Document',
      accessor: 'docType',
      render: (row) => (
        <div>
          <span className="text-slate-700 font-medium block">{row.docType.replace('_', ' ')}</span>
          <span className="text-[11px] text-slate-400 font-mono">{row.docNumber}</span>
        </div>
      )
    },
    {
      header: 'Liveness Match',
      accessor: 'livenessMatch',
      render: (row) => <span className="text-slate-600 font-mono">{row.livenessMatch}</span>
    },
    {
      header: 'Status',
      accessor: 'status',
      render: (row) => <Badge status={row.status} size="sm" />
    },
    {
      header: 'Actions',
      align: 'right',
      render: (row) => (
        <button
          onClick={() => handleOpenReview(row)}
          className="px-3 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-800 rounded-lg text-xs font-bold transition-colors"
        >
          Inspect &amp; Decide
        </button>
      )
    }
  ];

  return (
    <div>
      <PageHeader
        title="Compliance, KYC &amp; KYB Decisions"
        subtitle="Sovereign compliance control plane aligned with CBN Customer Due Diligence 2023 regulations and multi-provider verification evidence."
      />

      {/* Tabs */}
      <Tabs
        tabs={[
          { id: 'kyb', label: 'Corporate KYB (Legal Persons)', count: kybSubmissions.length, icon: Building },
          { id: 'kyc', label: 'Individual Tiered KYC', count: kycSubmissions.length, icon: User },
          { id: 'edd', label: 'Enhanced Due Diligence (EDD)', count: 2, icon: AlertTriangle },
          { id: 'restrictions', label: 'Account Limits & Restrictions', count: 1, icon: Sliders }
        ]}
        activeTab={activeTab}
        onChange={setActiveTab}
        className="mb-6"
      />

      {/* Table Content */}
      {activeTab === 'kyb' && (
        <DataTable
          columns={kybColumns}
          data={kybSubmissions}
          searchPlaceholder="Search by company name, CAC number, or TIN..."
        />
      )}

      {activeTab === 'kyc' && (
        <DataTable
          columns={kycColumns}
          data={kycSubmissions}
          searchPlaceholder="Search by individual name, phone, or BVN..."
        />
      )}

      {activeTab === 'edd' && (
        <div className="bg-white rounded-2xl border border-slate-200/80 p-6 shadow-xs">
          <div className="flex items-center gap-3 p-4 bg-amber-50 rounded-xl border border-amber-200 text-xs text-amber-900 mb-6">
            <AlertTriangle className="w-5 h-5 text-amber-600 shrink-0" />
            <div>
              <strong>Enhanced Due Diligence (EDD) Cases</strong> are mandatorily triggered for Politically Exposed Persons (PEPs), complex ownership (&gt;5% foreign entities), and high velocity accounts.
            </div>
          </div>

          <div className="space-y-4">
            <div className="p-4 border border-slate-200 rounded-xl hover:border-slate-300 transition-all flex flex-col md:flex-row md:items-center justify-between gap-4">
              <div>
                <div className="flex items-center gap-2 mb-1">
                  <h4 className="font-bold text-slate-900 text-sm">Chief Marcus Okoro</h4>
                  <Badge status="HIGH" size="sm" />
                  <Badge status="EDD_REQUIRED" size="sm" />
                </div>
                <p className="text-xs text-slate-500">
                  Trigger: <strong>PEP Watchlist Match</strong> • Public Official Category B • Cross River State
                </p>
                <p className="text-xs text-slate-600 mt-2 bg-slate-50 p-2 rounded-lg font-mono">
                  Source of Funds: Government Contractor Remittances • Source of Wealth: Agricultural Real Estate
                </p>
              </div>
              <div className="flex items-center gap-2 shrink-0">
                <button
                  onClick={() => showSuccess('EDD Case Approved', 'Chief Marcus Okoro verified under PEP continuous monitoring.')}
                  className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-xs font-bold transition-colors"
                >
                  Approve with Monitoring
                </button>
                <button
                  onClick={() => showError('EDD Case Rejected', 'Account flagged for restricted transaction caps.')}
                  className="px-4 py-2 bg-rose-50 hover:bg-rose-100 text-rose-700 border border-rose-200 rounded-xl text-xs font-bold transition-colors"
                >
                  Restrict Account
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {activeTab === 'restrictions' && (
        <div className="bg-white rounded-2xl border border-slate-200/80 p-6 shadow-xs">
          <div className="flex items-center justify-between mb-6">
            <div>
              <h3 className="text-sm font-bold text-slate-900">Custom Transaction Limits &amp; Compliance Restrictions</h3>
              <p className="text-xs text-slate-500 mt-0.5">Account-level caps placed by compliance officers on specific entities</p>
            </div>
            <button
              onClick={() => setShowRestrictionModal(true)}
              className="px-3.5 py-2 bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold rounded-xl shadow-xs"
            >
              + Add Account Restriction
            </button>
          </div>

          <div className="border border-slate-200 rounded-xl overflow-hidden text-xs">
            <table className="w-full text-left">
              <thead className="bg-slate-50 text-slate-500 font-semibold border-b border-slate-200">
                <tr>
                  <th className="p-3">Entity Name</th>
                  <th className="p-3">Restriction Type</th>
                  <th className="p-3">Effective Cap</th>
                  <th className="p-3">Reason</th>
                  <th className="p-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                <tr>
                  <td className="p-3 font-bold text-slate-900">Quantum Health International</td>
                  <td className="p-3">MAX_DAILY_PAYOUT</td>
                  <td className="p-3 font-mono font-bold">₦1,000,000.00</td>
                  <td className="p-3 text-slate-600">Cross-border foreign director EDD review pending</td>
                  <td className="p-3 text-right">
                    <button
                      onClick={() => showSuccess('Restriction Lifted', 'Effective limit restored to default.')}
                      className="text-rose-600 hover:text-rose-800 font-bold"
                    >
                      Remove
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Review Modal */}
      {selectedRecord && (
        <Modal
          isOpen={showReviewModal}
          onClose={() => setShowReviewModal(false)}
          title={`Review ${selectedRecord.companyName || selectedRecord.fullName}`}
          subtitle="Inspect evidence verified via Dojah, Smile ID, and Monnify integrations before executing a regulatory decision."
          footer={
            <div className="flex items-center justify-end gap-3 w-full">
              <button
                onClick={() => setShowReviewModal(false)}
                className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl hover:bg-slate-50"
              >
                Cancel
              </button>
              <button
                onClick={handleSaveDecision}
                className="px-5 py-2 text-xs font-bold text-white bg-blue-600 hover:bg-blue-700 rounded-xl shadow-xs"
              >
                Execute Compliance Decision
              </button>
            </div>
          }
        >
          <div className="space-y-4 text-xs text-left">
            <div className="p-3 bg-slate-50 rounded-xl border border-slate-200 grid grid-cols-2 gap-3">
              <div>
                <span className="text-slate-400 block text-[11px]">Entity Identity</span>
                <span className="font-bold text-slate-900">{selectedRecord.companyName || selectedRecord.fullName}</span>
              </div>
              <div>
                <span className="text-slate-400 block text-[11px]">Regulatory Risk</span>
                <span className="font-bold text-slate-900">{selectedRecord.riskRating}</span>
              </div>
            </div>

            {selectedRecord.cacDocUrl && (
              <div className="p-3 bg-blue-50/50 rounded-xl border border-blue-100 flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <FileText className="w-4 h-4 text-blue-600" />
                  <span className="font-semibold text-slate-800">CAC Incorporation Document</span>
                </div>
                <a
                  href={selectedRecord.cacDocUrl}
                  target="_blank"
                  rel="noreferrer"
                  className="text-blue-600 hover:underline font-bold flex items-center gap-1"
                >
                  View Artifact <ExternalLink className="w-3 h-3" />
                </a>
              </div>
            )}

            <div>
              <label className="block font-semibold text-slate-700 mb-1.5">Compliance Disposition</label>
              <select
                value={reviewDecision}
                onChange={(e) => setReviewDecision(e.target.value)}
                className="w-full px-3.5 py-2.5 text-xs bg-white border border-slate-200 rounded-xl focus:border-blue-600 focus:ring-2 focus:ring-blue-500/20 outline-hidden font-bold text-slate-800"
              >
                <option value="VERIFIED">VERIFY / APPROVE (Unlock Platform Limits)</option>
                <option value="REJECTED">REJECT (Retain Tier 1 Cap / Block Corporate Payroll)</option>
                <option value="EDD_REQUIRED">REQUIRE ENHANCED DUE DILIGENCE (Escalate Case)</option>
                <option value="SUSPENDED">SUSPEND ENTITY (Freeze Outbound Disbursements)</option>
              </select>
            </div>

            <div>
              <label className="block font-semibold text-slate-700 mb-1.5">Decision Notes &amp; Rationale</label>
              <textarea
                rows={3}
                value={reviewReason}
                onChange={(e) => setReviewReason(e.target.value)}
                placeholder="State audit reasons for regulatory decision..."
                className="w-full px-3.5 py-2 text-xs bg-white border border-slate-200 rounded-xl focus:border-blue-600 focus:ring-2 focus:ring-blue-500/20 outline-hidden"
              />
            </div>
          </div>
        </Modal>
      )}

      {/* Add Restriction Modal */}
      <Modal
        isOpen={showRestrictionModal}
        onClose={() => setShowRestrictionModal(false)}
        title="Add Account Restriction"
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button
              onClick={() => setShowRestrictionModal(false)}
              className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl"
            >
              Cancel
            </button>
            <button
              onClick={() => {
                showSuccess('Restriction Added', 'Custom account-level transaction cap enforced.');
                setShowRestrictionModal(false);
              }}
              className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl"
            >
              Enforce Cap
            </button>
          </div>
        }
      >
        <div className="space-y-4 text-xs text-left">
          <div>
            <label className="block font-semibold text-slate-700 mb-1.5">Target Account / Organization</label>
            <input
              type="text"
              placeholder="e.g. Zenith Matrix Logistics Ltd"
              className="w-full px-3.5 py-2 text-xs bg-white border border-slate-200 rounded-xl outline-hidden"
            />
          </div>
          <div>
            <label className="block font-semibold text-slate-700 mb-1.5">Maximum Daily Transaction Cap (₦)</label>
            <input
              type="number"
              value={restrictionLimit}
              onChange={(e) => setRestrictionLimit(e.target.value)}
              className="w-full px-3.5 py-2 text-xs bg-white border border-slate-200 rounded-xl outline-hidden font-bold"
            />
          </div>
        </div>
      </Modal>
    </div>
  );
}
