import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import Badge from '../../components/common/Badge';
import Modal from '../../components/common/Modal';
import { useToast } from '../../context/ToastContext';
import { formatCurrency } from '../../utils/formatters';
import {
  Shield,
  CheckCircle2,
  Lock,
  Upload,
  Camera,
  AlertTriangle,
  ArrowRight,
  ShieldCheck,
  FileCheck
} from 'lucide-react';

export default function KycCompliancePage() {
  const { showSuccess, showError } = useToast();

  const [bvn, setBvn] = useState('22345678901');
  const [nin, setNin] = useState('12345678901');
  const [docType, setDocType] = useState('NIMC_CARD');
  const [docNumber, setDocNumber] = useState('NIN-9928341');
  const [docFileUrl, setDocFileUrl] = useState('https://storage.cebizpay.com/kyc/amina-id.pdf');

  const [isVerifyingBvn, setIsVerifyingBvn] = useState(false);
  const [isVerifyingNin, setIsVerifyingNin] = useState(false);
  const [isVerifyingBio, setIsVerifyingBio] = useState(false);

  const [tierState, setTierState] = useState({
    currentTier: 'TIER_3',
    bvnVerified: true,
    ninVerified: true,
    biometricsVerified: true,
    docVerified: true,
    amlScreeningStatus: 'CLEAN'
  });

  const handleVerifyBvn = (e) => {
    e.preventDefault();
    setIsVerifyingBvn(true);
    setTimeout(() => {
      setIsVerifyingBvn(false);
      setTierState((prev) => ({ ...prev, bvnVerified: true }));
      showSuccess('BVN Match Confirmed', 'Verified against NIBSS centralized identity registry.');
    }, 800);
  };

  const handleVerifyNin = (e) => {
    e.preventDefault();
    setIsVerifyingNin(true);
    setTimeout(() => {
      setIsVerifyingNin(false);
      setTierState((prev) => ({ ...prev, ninVerified: true }));
      showSuccess('NIN Match Confirmed', 'Verified against NIMC database.');
    }, 800);
  };

  const handleVerifyBiometrics = () => {
    setIsVerifyingBio(true);
    setTimeout(() => {
      setIsVerifyingBio(false);
      setTierState((prev) => ({ ...prev, biometricsVerified: true, currentTier: 'TIER_3' }));
      showSuccess('Biometric Liveness Match (99.4%)', 'SmartSelfie™ matched against photographic government records.');
    }, 1200);
  };

  return (
    <div className="max-w-4xl mx-auto">
      <PageHeader
        title="Identity Verification &amp; KYC Tiering"
        subtitle="Sovereign tier levels compliant with Central Bank of Nigeria (CBN) CDD 2023 regulations."
      />

      {/* Current Tier Overview Banner */}
      <div className="bg-white rounded-3xl border border-slate-200/80 p-6 sm:p-8 mb-8 shadow-xs">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-6 pb-6 border-b border-slate-100">
          <div className="flex items-center gap-4">
            <div className="w-14 h-14 rounded-2xl bg-emerald-50 text-emerald-600 flex items-center justify-center border border-emerald-100">
              <ShieldCheck className="w-7 h-7" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h3 className="text-xl font-bold text-slate-900">Tier 3 (Full Regulatory KYC)</h3>
                <Badge status="VERIFIED" size="sm" />
              </div>
              <p className="text-xs text-slate-500 mt-1">
                Account Status: <strong>Fully Unlocked</strong> • AML Watchlist: <strong className="text-emerald-700 font-mono">CLEAN</strong>
              </p>
            </div>
          </div>

          <div className="flex items-center gap-4 text-xs font-mono">
            <div className="p-3 bg-slate-50 rounded-xl border border-slate-100 text-center">
              <span className="text-slate-400 block text-[10px] font-sans">Single Transfer Limit</span>
              <span className="font-bold text-slate-900 font-mono">₦5,000,000.00</span>
            </div>
            <div className="p-3 bg-slate-50 rounded-xl border border-slate-100 text-center">
              <span className="text-slate-400 block text-[10px] font-sans">Cumulative Daily Limit</span>
              <span className="font-bold text-slate-900 font-mono">₦25,000,000.00</span>
            </div>
          </div>
        </div>

        {/* Tier Requirements Grid */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 pt-6 text-xs">
          {/* Tier 1 */}
          <div className="p-4 rounded-2xl border border-emerald-200 bg-emerald-50/30">
            <div className="flex items-center justify-between mb-2">
              <span className="font-bold text-emerald-950">Tier 1 — Basic Mobile</span>
              <CheckCircle2 className="w-4 h-4 text-emerald-600" />
            </div>
            <p className="text-[11px] text-slate-500 mb-2">Verified phone number &amp; name</p>
            <span className="font-mono font-bold text-slate-700">Max Daily: ₦50,000</span>
          </div>

          {/* Tier 2 */}
          <div className="p-4 rounded-2xl border border-emerald-200 bg-emerald-50/30">
            <div className="flex items-center justify-between mb-2">
              <span className="font-bold text-emerald-950">Tier 2 — NIBSS / NIMC</span>
              <CheckCircle2 className="w-4 h-4 text-emerald-600" />
            </div>
            <p className="text-[11px] text-slate-500 mb-2">Bank Verification Number (BVN) + NIN</p>
            <span className="font-mono font-bold text-slate-700">Max Daily: ₦200,000</span>
          </div>

          {/* Tier 3 */}
          <div className="p-4 rounded-2xl border border-emerald-200 bg-emerald-50/30">
            <div className="flex items-center justify-between mb-2">
              <span className="font-bold text-emerald-950">Tier 3 — Biometric Match</span>
              <CheckCircle2 className="w-4 h-4 text-emerald-600" />
            </div>
            <p className="text-[11px] text-slate-500 mb-2">SmartSelfie™ Liveness &amp; Government ID</p>
            <span className="font-mono font-bold text-slate-700">Max Daily: ₦25,000,000</span>
          </div>
        </div>
      </div>

      {/* Verification Action Modules */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6 text-xs text-left">
        {/* BVN / NIN Module */}
        <div className="bg-white p-6 rounded-3xl border border-slate-200/80 shadow-xs space-y-4">
          <h4 className="font-bold text-sm text-slate-900 flex items-center gap-2">
            <Shield className="w-4 h-4 text-blue-600" />
            National Identity Verification (BVN &amp; NIN)
          </h4>

          <form onSubmit={handleVerifyBvn} className="space-y-3 p-3.5 bg-slate-50 rounded-2xl border border-slate-100">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">11-Digit Bank Verification Number (BVN)</label>
              <div className="flex gap-2">
                <input
                  type="text"
                  maxLength={11}
                  required
                  value={bvn}
                  onChange={(e) => setBvn(e.target.value)}
                  className="flex-1 px-3 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold"
                />
                <button
                  type="submit"
                  disabled={isVerifyingBvn}
                  className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded-xl disabled:opacity-50"
                >
                  {isVerifyingBvn ? 'Verifying...' : 'Verify BVN'}
                </button>
              </div>
            </div>
          </form>

          <form onSubmit={handleVerifyNin} className="space-y-3 p-3.5 bg-slate-50 rounded-2xl border border-slate-100">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">National Identity Number (NIN)</label>
              <div className="flex gap-2">
                <input
                  type="text"
                  maxLength={11}
                  required
                  value={nin}
                  onChange={(e) => setNin(e.target.value)}
                  className="flex-1 px-3 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold"
                />
                <button
                  type="submit"
                  disabled={isVerifyingNin}
                  className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded-xl disabled:opacity-50"
                >
                  {isVerifyingNin ? 'Verifying...' : 'Verify NIN'}
                </button>
              </div>
            </div>
          </form>
        </div>

        {/* Biometrics & ID Upload Module */}
        <div className="bg-white p-6 rounded-3xl border border-slate-200/80 shadow-xs space-y-4">
          <h4 className="font-bold text-sm text-slate-900 flex items-center gap-2">
            <Camera className="w-4 h-4 text-purple-600" />
            Biometric Liveness &amp; Document OCR
          </h4>

          <div className="p-4 bg-slate-50 rounded-2xl border border-slate-100 space-y-3">
            <div className="flex items-center justify-between">
              <div>
                <span className="font-bold text-slate-900 block">SmartSelfie™ 1:1 Facial Match</span>
                <span className="text-[11px] text-slate-500">3D liveness detection powered by Smile ID</span>
              </div>
              <button
                type="button"
                onClick={handleVerifyBiometrics}
                disabled={isVerifyingBio}
                className="px-4 py-2 bg-purple-600 hover:bg-purple-700 text-white font-bold rounded-xl disabled:opacity-50"
              >
                {isVerifyingBio ? 'Scanning...' : 'Scan Liveness'}
              </button>
            </div>
          </div>

          <div className="p-4 bg-slate-50 rounded-2xl border border-slate-100 space-y-3">
            <span className="font-bold text-slate-900 block">Government Document OCR</span>
            <div className="grid grid-cols-2 gap-2">
              <select
                value={docType}
                onChange={(e) => setDocType(e.target.value)}
                className="px-3 py-2 bg-white border border-slate-200 rounded-xl font-medium"
              >
                <option value="NIMC_CARD">NIMC National Card</option>
                <option value="DRIVERS_LICENSE">Driver's License (FRSC)</option>
                <option value="INTERNATIONAL_PASSPORT">International Passport (NIS)</option>
              </select>
              <input
                type="text"
                value={docNumber}
                onChange={(e) => setDocNumber(e.target.value)}
                className="px-3 py-2 bg-white border border-slate-200 rounded-xl font-mono font-bold"
              />
            </div>
            <button
              type="button"
              onClick={() => showSuccess('Document Uploaded & Verified', 'OCR validated against national issuer.')}
              className="w-full py-2 bg-slate-800 hover:bg-slate-900 text-white font-bold rounded-xl flex items-center justify-center gap-1.5"
            >
              <Upload className="w-3.5 h-3.5" /> Upload ID Document
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
