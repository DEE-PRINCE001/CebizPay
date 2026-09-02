import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import { formatDate } from '../../utils/formatters';
import { FileSpreadsheet, Filter, Search, Terminal, Eye } from 'lucide-react';
import Modal from '../../components/common/Modal';

export default function AdminAuditLogs() {
  const [selectedLog, setSelectedLog] = useState(null);

  const [logs] = useState([
    {
      id: 'aud-001',
      action: 'ORGANIZATION_STATUS_UPDATED',
      actorId: 'honour@gmail.com (SuperAdmin)',
      resourceType: 'ORGANIZATION',
      resourceId: 'a1b2c3d4-e5f6-7a8b-9c0d-1e2f3a4b5c6d',
      details: 'Changed KYB status from PENDING to VERIFIED. Unlocked outbound corporate payroll rails.',
      ipAddress: '197.210.55.12',
      correlationId: 'corr_88392014829',
      timestamp: '2026-09-01T22:30:00Z'
    },
    {
      id: 'aud-002',
      action: 'FEE_POLICY_ACTIVATED',
      actorId: 'honour@gmail.com (SuperAdmin)',
      resourceType: 'FEE_POLICY',
      resourceId: 'pol-peer-01',
      details: 'Created and activated Version 2 Peer Transfer Fee Policy (Rate: 0.5%, Floor: ₦20, Cap: ₦500).',
      ipAddress: '197.210.55.12',
      correlationId: 'corr_77492018471',
      timestamp: '2026-09-01T21:15:00Z'
    },
    {
      id: 'aud-003',
      action: 'PAYROLL_BATCH_EXECUTED',
      actorId: 'ceo@apextech.com (Apex CEO)',
      resourceType: 'PAYROLL_BATCH',
      resourceId: 'batch-99281',
      details: 'Executed Mode: Pay All across 28 staff members. Total Net: ₦14,250,000.00.',
      ipAddress: '102.89.44.18',
      correlationId: 'corr_11029384729',
      timestamp: '2026-09-01T20:00:00Z'
    },
    {
      id: 'aud-004',
      action: 'PAYMENT_VOUCHER_METADATA_UPDATED',
      actorId: 'finance@apextech.com (Finance Manager)',
      resourceType: 'PAYMENT_VOUCHER',
      resourceId: 'vouch-44921',
      details: 'Updated paying bank description to "Standard Chartered Corporate Clearing".',
      ipAddress: '102.89.44.18',
      correlationId: 'corr_44920184912',
      timestamp: '2026-09-01T18:30:00Z'
    },
    {
      id: 'aud-005',
      action: 'STAFF_MEMBERSHIP_TERMINATED',
      actorId: 'hr@apextech.com (HR Manager)',
      resourceType: 'STAFF_MEMBERSHIP',
      resourceId: 'staff-88192',
      details: 'Terminated staff membership for John Doe. Converted corporate loan #LN-4921 to standard individual loan contract.',
      ipAddress: '102.89.44.18',
      correlationId: 'corr_99381029481',
      timestamp: '2026-09-01T16:00:00Z'
    }
  ]);

  const columns = [
    {
      header: 'Timestamp',
      accessor: 'timestamp',
      render: (row) => <span className="font-mono text-slate-500 text-[11px]">{formatDate(row.timestamp, true)}</span>
    },
    {
      header: 'Action',
      accessor: 'action',
      render: (row) => <Badge status="ACTIVE" label={row.action} size="sm" />
    },
    {
      header: 'Actor',
      accessor: 'actorId',
      render: (row) => <span className="font-bold text-slate-800 text-xs">{row.actorId}</span>
    },
    {
      header: 'Resource',
      accessor: 'resourceType',
      render: (row) => (
        <div>
          <span className="font-bold text-slate-700 text-xs block">{row.resourceType}</span>
          <span className="text-[10px] text-slate-400 font-mono">{row.resourceId}</span>
        </div>
      )
    },
    {
      header: 'Details & Impact',
      accessor: 'details',
      render: (row) => <span className="text-slate-600 text-xs truncate max-w-sm block">{row.details}</span>
    },
    {
      header: 'Inspector',
      align: 'right',
      render: (row) => (
        <button
          onClick={() => setSelectedLog(row)}
          className="p-1.5 text-slate-500 hover:text-blue-600 hover:bg-slate-100 rounded-lg transition-colors"
          title="Inspect Record"
        >
          <Eye className="w-4 h-4" />
        </button>
      )
    }
  ];

  return (
    <div>
      <PageHeader
        title="Platform Audit Trail &amp; Ledger Logs"
        subtitle="Immutable append-only audit trail logging every administrative state transition, fee alteration, voucher edit, and permission change."
      />

      <DataTable
        columns={columns}
        data={logs}
        searchPlaceholder="Search audit events by action, actor, resource ID, or correlation ID..."
      />

      {/* Log Inspector Modal */}
      {selectedLog && (
        <Modal
          isOpen={!!selectedLog}
          onClose={() => setSelectedLog(null)}
          title={`Audit Event: ${selectedLog.action}`}
          subtitle={`Logged with Correlation ID: ${selectedLog.correlationId}`}
          footer={
            <button
              onClick={() => setSelectedLog(null)}
              className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl"
            >
              Close
            </button>
          }
        >
          <div className="space-y-3 text-xs text-left">
            <div className="p-3 bg-slate-50 rounded-xl border border-slate-200 space-y-2">
              <div className="flex justify-between">
                <span className="text-slate-500">Timestamp:</span>
                <span className="font-mono font-bold text-slate-800">{formatDate(selectedLog.timestamp, true)}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Actor Identity:</span>
                <span className="font-bold text-slate-800">{selectedLog.actorId}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Origin IP:</span>
                <span className="font-mono text-slate-800">{selectedLog.ipAddress}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Resource:</span>
                <span className="font-mono text-slate-800">{selectedLog.resourceType} ({selectedLog.resourceId})</span>
              </div>
            </div>

            <div>
              <span className="font-semibold text-slate-700 block mb-1">Detailed Event Description:</span>
              <p className="p-3 bg-slate-50 rounded-xl border border-slate-200 text-slate-800 font-mono leading-relaxed">
                {selectedLog.details}
              </p>
            </div>
          </div>
        </Modal>
      )}
    </div>
  );
}
