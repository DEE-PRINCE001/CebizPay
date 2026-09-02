import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Tabs from '../../components/common/Tabs';
import Modal from '../../components/common/Modal';
import { useToast } from '../../context/ToastContext';
import { formatCurrency, formatDate } from '../../utils/formatters';
import {
  RefreshCw,
  AlertCircle,
  CheckCircle,
  HelpCircle,
  RotateCcw,
  Search,
  Sliders,
  DollarSign
} from 'lucide-react';

export default function AdminReconciliation() {
  const [activeTab, setActiveTab] = useState('unmatched'); // 'unmatched' | 'recoveries' | 'requery'
  const [requeryRef, setRequeryRef] = useState('');
  const [requeryResult, setRequeryResult] = useState(null);
  const [isRequerying, setIsRequerying] = useState(false);
  const [selectedRecord, setSelectedRecord] = useState(null);
  const [showReviewModal, setShowReviewModal] = useState(false);
  const [reviewDecision, setReviewDecision] = useState('ConfirmSuccess');
  const [reviewerNotes, setReviewerNotes] = useState('');

  const { showSuccess, showError } = useToast();

  const [records, setRecords] = useState([
    {
      id: 'rec-001',
      reference: 'TXN-MNFY-88492019',
      type: 'BankTransfer',
      provider: 'Monnify',
      amount: 250000.0,
      currency: 'NGN',
      status: 'MATCHED',
      createdAt: '2026-09-01T21:10:00Z',
      notes: 'Settled automatically via primary rail'
    },
    {
      id: 'rec-002',
      reference: 'TXN-FLW-99201483',
      type: 'CardFunding',
      provider: 'Flutterwave',
      amount: 50000.0,
      currency: 'NGN',
      status: 'AMBIGUOUS',
      createdAt: '2026-09-01T22:45:00Z',
      notes: 'Webhook delayed; gateway returned HTTP 504 on initial poll'
    },
    {
      id: 'rec-003',
      reference: 'TXN-PSTK-11928471',
      type: 'BankTransfer',
      provider: 'Paystack',
      amount: 120000.0,
      currency: 'NGN',
      status: 'MANUAL_REVIEW_REQUIRED',
      createdAt: '2026-09-01T23:00:00Z',
      notes: 'Provider reports reversed after double-entry posting'
    }
  ]);

  const [recoveries, setRecoveries] = useState([
    {
      id: 'recov-01',
      accountHolder: 'Kazeem Oladipo',
      walletId: 'wlt-998231',
      shortfallAmount: 14500.0,
      currency: 'NGN',
      status: 'PENDING',
      reason: 'Chargeback occurred with insufficient available wallet balance',
      createdAt: '2026-08-25T11:00:00Z'
    }
  ]);

  const handleRequery = (e) => {
    e.preventDefault();
    if (!requeryRef) return;
    setIsRequerying(true);
    setTimeout(() => {
      setIsRequerying(false);
      setRequeryResult({
        reference: requeryRef,
        provider: 'Monnify',
        providerStatus: 'PAID',
        settlementAmount: 250000.0,
        providerFee: 350.0,
        sessionReference: 'MNFY_SESS_883920148201',
        synchronizedAt: new Date().toISOString(),
        ledgerStatus: 'POSTED_AND_BALANCED'
      });
      showSuccess('Requery Successful', `Reference ${requeryRef} resolved to PAID on provider gateway.`);
    }, 900);
  };

  const handleOpenReview = (row) => {
    setSelectedRecord(row);
    setReviewDecision('ConfirmSuccess');
    setReviewerNotes('');
    setShowReviewModal(true);
  };

  const handleExecuteReview = () => {
    if (!reviewerNotes) {
      showError('Notes Required', 'Reviewer audit notes are mandatory for financial reconciliation.');
      return;
    }
    setRecords((prev) =>
      prev.map((r) =>
        r.id === selectedRecord.id
          ? { ...r, status: reviewDecision === 'ConfirmSuccess' ? 'MATCHED' : 'RESOLVED', notes: reviewerNotes }
          : r
      )
    );
    showSuccess('Reconciliation Executed', `Record ${selectedRecord.reference} resolved.`);
    setShowReviewModal(false);
  };

  const columns = [
    {
      header: 'Reference Code',
      accessor: 'reference',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 font-mono block">{row.reference}</span>
          <span className="text-[11px] text-slate-400">{row.type}</span>
        </div>
      )
    },
    {
      header: 'Rail Provider',
      accessor: 'provider',
      render: (row) => <span className="font-semibold text-slate-700">{row.provider}</span>
    },
    {
      header: 'Amount',
      accessor: 'amount',
      render: (row) => <span className="font-bold text-slate-900 font-mono">{formatCurrency(row.amount)}</span>
    },
    {
      header: 'Reconciliation Status',
      accessor: 'status',
      render: (row) => (
        <Badge
          status={
            row.status === 'MATCHED'
              ? 'VERIFIED'
              : row.status === 'AMBIGUOUS'
              ? 'UNDER_REVIEW'
              : 'EDD_REQUIRED'
          }
          label={row.status}
          size="sm"
        />
      )
    },
    {
      header: 'Audit Notes',
      accessor: 'notes',
      render: (row) => <span className="text-slate-500 text-[11px] truncate max-w-xs block">{row.notes}</span>
    },
    {
      header: 'Actions',
      align: 'right',
      render: (row) => (
        <div className="flex items-center justify-end gap-2">
          {row.status !== 'MATCHED' && (
            <button
              onClick={() => handleOpenReview(row)}
              className="px-3 py-1.5 bg-blue-50 text-blue-700 hover:bg-blue-100 rounded-lg text-xs font-bold transition-colors"
            >
              Manual Review
            </button>
          )}
        </div>
      )
    }
  ];

  const recoveryColumns = [
    {
      header: 'Account Holder',
      accessor: 'accountHolder',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-900 block">{row.accountHolder}</span>
          <span className="text-[11px] text-slate-400 font-mono">{row.walletId}</span>
        </div>
      )
    },
    {
      header: 'Outstanding Shortfall',
      accessor: 'shortfallAmount',
      render: (row) => (
        <span className="font-bold text-rose-600 font-mono">{formatCurrency(row.shortfallAmount)}</span>
      )
    },
    {
      header: 'Recovery Reason',
      accessor: 'reason',
      render: (row) => <span className="text-slate-600 text-xs">{row.reason}</span>
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
          onClick={() => showSuccess('Recovery Claim Dispatched', 'Automated offset triggered against next incoming deposit.')}
          className="px-3 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-800 rounded-lg text-xs font-bold"
        >
          Claim Offset
        </button>
      )
    }
  ];

  return (
    <div>
      <PageHeader
        title="Financial &amp; Webhook Reconciliation Control Plane"
        subtitle="Authoritative reconciliation engine ensuring zero double credits, failover safety, and transparent recovery tracking."
      />

      <Tabs
        tabs={[
          { id: 'unmatched', label: 'Payment & Payout Rails', count: records.length, icon: RefreshCw },
          { id: 'recoveries', label: 'Outstanding Recoveries', count: recoveries.length, icon: DollarSign },
          { id: 'requery', label: 'On-Demand Status Requery', icon: Search }
        ]}
        activeTab={activeTab}
        onChange={setActiveTab}
        className="mb-6"
      />

      {activeTab === 'unmatched' && (
        <DataTable
          columns={columns}
          data={records}
          searchPlaceholder="Search by transaction reference or provider..."
        />
      )}

      {activeTab === 'recoveries' && (
        <DataTable
          columns={recoveryColumns}
          data={recoveries}
          searchPlaceholder="Search recoveries..."
        />
      )}

      {activeTab === 'requery' && (
        <div className="bg-white rounded-2xl border border-slate-200/80 p-6 shadow-xs max-w-2xl">
          <h3 className="text-sm font-bold text-slate-900 mb-1">On-Demand Provider Status Requery</h3>
          <p className="text-xs text-slate-500 mb-6">
            Directly poll Monnify, Flutterwave, or Paystack APIs to synchronize ambiguous transaction states without modifying double-entry invariants prematurely.
          </p>

          <form onSubmit={handleRequery} className="flex gap-2.5 mb-6">
            <input
              type="text"
              required
              value={requeryRef}
              onChange={(e) => setRequeryRef(e.target.value)}
              placeholder="e.g. TXN-MNFY-88492019 or FLW_REF_99210"
              className="flex-1 px-3.5 py-2.5 text-xs bg-white border border-slate-200 rounded-xl focus:border-blue-600 focus:ring-2 focus:ring-blue-500/20 font-mono outline-hidden"
            />
            <button
              type="submit"
              disabled={isRequerying}
              className="px-5 py-2.5 bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold rounded-xl shadow-xs flex items-center gap-1.5 disabled:opacity-50"
            >
              {isRequerying ? <span className="w-3.5 h-3.5 border-2 border-white/30 border-t-white rounded-full animate-spin" /> : <RefreshCw className="w-3.5 h-3.5" />}
              Requery Status
            </button>
          </form>

          {requeryResult && (
            <div className="p-4 bg-slate-50 rounded-2xl border border-slate-200 space-y-3 text-xs animate-in fade-in">
              <div className="flex items-center justify-between pb-2 border-b border-slate-200">
                <span className="text-slate-500">Provider Status:</span>
                <Badge status="VERIFIED" label="PAID &amp; SETTLED" size="sm" />
              </div>
              <div className="flex items-center justify-between">
                <span className="text-slate-500">Gateway Provider:</span>
                <span className="font-bold text-slate-900">{requeryResult.provider}</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-slate-500">Gross Settled Amount:</span>
                <span className="font-bold text-slate-900 font-mono">{formatCurrency(requeryResult.settlementAmount)}</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-slate-500">Central Ledger Status:</span>
                <span className="font-bold text-emerald-700 font-mono">{requeryResult.ledgerStatus}</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-slate-500">Provider Session Ref:</span>
                <span className="font-mono text-slate-600">{requeryResult.sessionReference}</span>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Manual Review Modal */}
      {selectedRecord && (
        <Modal
          isOpen={showReviewModal}
          onClose={() => setShowReviewModal(false)}
          title={`Manual Review: ${selectedRecord.reference}`}
          footer={
            <div className="flex items-center justify-end gap-3 w-full">
              <button
                onClick={() => setShowReviewModal(false)}
                className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl"
              >
                Cancel
              </button>
              <button
                onClick={handleExecuteReview}
                className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl"
              >
                Submit Reconciliation Disposition
              </button>
            </div>
          }
        >
          <div className="space-y-4 text-xs text-left">
            <div>
              <label className="block font-semibold text-slate-700 mb-1.5">Review Decision</label>
              <select
                value={reviewDecision}
                onChange={(e) => setReviewDecision(e.target.value)}
                className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl font-bold"
              >
                <option value="ConfirmSuccess">ConfirmSuccess (Post Double-Entry Credit)</option>
                <option value="ConfirmFailure">ConfirmFailure (Unlock Reserved Funds)</option>
                <option value="ConfirmReversal">ConfirmReversal (Record Offset Ledger Reversal)</option>
                <option value="Dismiss">Dismiss (Ignore Malformed Webhook)</option>
              </select>
            </div>

            <div>
              <label className="block font-semibold text-slate-700 mb-1.5">Reviewer Audit Notes</label>
              <textarea
                rows={3}
                required
                value={reviewerNotes}
                onChange={(e) => setReviewerNotes(e.target.value)}
                placeholder="Detail the verified evidence from upstream provider portal..."
                className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl outline-hidden"
              />
            </div>
          </div>
        </Modal>
      )}
    </div>
  );
}
