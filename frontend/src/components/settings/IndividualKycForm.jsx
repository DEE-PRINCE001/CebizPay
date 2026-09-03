import React, { useState } from 'react';
import Card from '../common/Card';
import Tabs from '../common/Tabs';
import Input from '../forms/Input';
import Select from '../forms/Select';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import SuccessModal from '../feedback/SuccessModal';
import { ShieldCheck, CreditCard, FileText, CheckCircle2, UserCheck, Calendar } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

const DOCUMENT_TYPES = [
  { value: 1, label: "International Passport" },
  { value: 2, label: "Driver's License" },
  { value: 3, label: "National ID (NIN Slip)" },
  { value: 4, label: "Voter's Card" }
];

/**
 * Individual KYC verification form for BVN, NIN, and government identity documents.
 */
export default function IndividualKycForm({
  user,
  onSuccess,
  className = ''
}) {
  const { showSuccess } = useToast();
  const [subTab, setSubTab] = useState('bvn'); // 'bvn' | 'nin' | 'document'

  // BVN Form State
  const [bvn, setBvn] = useState('');
  const [bvnFirstName, setBvnFirstName] = useState(user?.firstName || '');
  const [bvnLastName, setBvnLastName] = useState(user?.lastName || '');
  const [bvnDob, setBvnDob] = useState('1990-01-01');

  // NIN Form State
  const [nin, setNin] = useState('');
  const [ninFirstName, setNinFirstName] = useState(user?.firstName || '');
  const [ninLastName, setNinLastName] = useState(user?.lastName || '');
  const [ninDob, setNinDob] = useState('1990-01-01');

  // Document Form State
  const [docType, setDocType] = useState(1);
  const [docNumber, setDocNumber] = useState('');
  const [docBase64, setDocBase64] = useState('');

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [successData, setSuccessData] = useState(null);

  const kycTabs = [
    { id: 'bvn', label: 'BVN Verification', icon: CreditCard },
    { id: 'nin', label: 'NIN Verification', icon: UserCheck },
    { id: 'document', label: 'Identity Document', icon: FileText }
  ];

  // Submit BVN
  const handleBvnSubmit = async (e) => {
    e.preventDefault();
    if (bvn.length !== 11) {
      setError('Please enter a valid 11-digit Bank Verification Number.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const response = await apiClient.post('/compliance/kyc/bvn', {
        bvn: bvn.trim(),
        firstName: bvnFirstName.trim(),
        lastName: bvnLastName.trim(),
        dateOfBirth: new Date(bvnDob).toISOString()
      });

      setSuccessData({
        type: 'BVN',
        reference: response?.operationReference || response?.reference || 'VERIFIED',
        tier: 'Tier 2 (Standard Limits)'
      });

      showSuccess('BVN verification passed successfully.');
      setBvn('');
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'BVN verification failed. Please ensure the names match your banking records.');
    } finally {
      setLoading(false);
    }
  };

  // Submit NIN
  const handleNinSubmit = async (e) => {
    e.preventDefault();
    if (nin.length !== 11) {
      setError('Please enter a valid 11-digit National Identification Number.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const response = await apiClient.post('/compliance/kyc/nin', {
        nin: nin.trim(),
        firstName: ninFirstName.trim(),
        lastName: ninLastName.trim(),
        dateOfBirth: new Date(ninDob).toISOString()
      });

      setSuccessData({
        type: 'NIN',
        reference: response?.operationReference || response?.reference || 'VERIFIED',
        tier: 'Tier 2 (Standard Limits)'
      });

      showSuccess('NIN verification passed successfully.');
      setNin('');
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'NIN verification failed. Please verify your details and try again.');
    } finally {
      setLoading(false);
    }
  };

  // Submit Document
  const handleDocSubmit = async (e) => {
    e.preventDefault();
    if (!docNumber.trim()) {
      setError('Please provide the document number.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const response = await apiClient.post('/compliance/kyc/document', {
        documentType: parseInt(docType, 10),
        documentNumber: docNumber.trim(),
        documentImageBase64: docBase64 || 'data:image/jpeg;base64,dGVzdA=='
      });

      setSuccessData({
        type: 'Government ID',
        reference: response?.operationReference || response?.reference || 'SUBMITTED',
        tier: 'Tier 3 (Uncapped Limits)'
      });

      showSuccess('Identity document submitted for verification.');
      setDocNumber('');
      setDocBase64('');
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Document verification failed.');
    } finally {
      setLoading(false);
    }
  };

  const handleFileUpload = (e) => {
    const file = e.target.files[0];
    if (file) {
      const reader = new FileReader();
      reader.onloadend = () => {
        setDocBase64(reader.result);
      };
      reader.readAsDataURL(file);
    }
  };

  return (
    <>
      <Card padding="p-6" className={`bg-white border border-slate-200/80 ${className}`}>
        <div className="flex items-center gap-3 pb-4 mb-4 border-b border-slate-100">
          <div className="w-10 h-10 rounded-2xl bg-brand-50 text-brand-600 flex items-center justify-center">
            <ShieldCheck size={20} />
          </div>
          <div>
            <h3 className="text-base font-bold text-slate-900">Individual KYC Verification</h3>
            <p className="text-xs text-slate-500">Upgrade your transaction limits by verifying your identity</p>
          </div>
        </div>

        <div className="space-y-4">
          <Tabs
            variant="segmented"
            tabs={kycTabs}
            activeTab={subTab}
            onChange={(t) => {
              setSubTab(t);
              setError(null);
            }}
          />

          {error && (
            <Alert variant="danger" onClose={() => setError(null)}>
              {error}
            </Alert>
          )}

          {/* TAB 1: BVN Verification */}
          {subTab === 'bvn' && (
            <form onSubmit={handleBvnSubmit} className="space-y-4 pt-1">
              <Input
                label="Bank Verification Number (BVN)"
                placeholder="Enter 11-digit BVN"
                maxLength={11}
                value={bvn}
                onChange={(e) => {
                  setBvn(e.target.value.replace(/\D/g, ''));
                  if (error) setError(null);
                }}
                icon={CreditCard}
                helperText="Dial *565*0# on your registered mobile number to check your BVN."
                required
              />

              <div className="grid grid-cols-2 gap-3">
                <Input
                  label="First Name on Bank Account"
                  value={bvnFirstName}
                  onChange={(e) => setBvnFirstName(e.target.value)}
                  required
                />
                <Input
                  label="Last Name on Bank Account"
                  value={bvnLastName}
                  onChange={(e) => setBvnLastName(e.target.value)}
                  required
                />
              </div>

              <Input
                label="Date of Birth"
                type="date"
                value={bvnDob}
                onChange={(e) => setBvnDob(e.target.value)}
                icon={Calendar}
                required
              />

              <Button
                type="submit"
                variant="primary"
                size="md"
                loading={loading}
                icon={ShieldCheck}
                className="w-full"
              >
                Verify BVN & Upgrade Account
              </Button>
            </form>
          )}

          {/* TAB 2: NIN Verification */}
          {subTab === 'nin' && (
            <form onSubmit={handleNinSubmit} className="space-y-4 pt-1">
              <Input
                label="National Identification Number (NIN)"
                placeholder="Enter 11-digit NIN"
                maxLength={11}
                value={nin}
                onChange={(e) => {
                  setNin(e.target.value.replace(/\D/g, ''));
                  if (error) setError(null);
                }}
                icon={UserCheck}
                helperText="Dial *346# on your registered mobile line to retrieve your NIN."
                required
              />

              <div className="grid grid-cols-2 gap-3">
                <Input
                  label="First Name on NIMC Record"
                  value={ninFirstName}
                  onChange={(e) => setNinFirstName(e.target.value)}
                  required
                />
                <Input
                  label="Last Name on NIMC Record"
                  value={ninLastName}
                  onChange={(e) => setNinLastName(e.target.value)}
                  required
                />
              </div>

              <Input
                label="Date of Birth"
                type="date"
                value={ninDob}
                onChange={(e) => setNinDob(e.target.value)}
                icon={Calendar}
                required
              />

              <Button
                type="submit"
                variant="primary"
                size="md"
                loading={loading}
                icon={ShieldCheck}
                className="w-full"
              >
                Verify NIN
              </Button>
            </form>
          )}

          {/* TAB 3: Government ID Document Upload */}
          {subTab === 'document' && (
            <form onSubmit={handleDocSubmit} className="space-y-4 pt-1">
              <Select
                label="Select Document Type"
                options={DOCUMENT_TYPES}
                value={docType}
                onChange={(e) => setDocType(e.target.value)}
                required
              />

              <Input
                label="Document / ID Number"
                placeholder="e.g. A01234567"
                value={docNumber}
                onChange={(e) => {
                  setDocNumber(e.target.value);
                  if (error) setError(null);
                }}
                icon={FileText}
                required
              />

              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1.5">
                  Upload Document Photo / Scan
                </label>
                <input
                  type="file"
                  accept="image/*,application/pdf"
                  onChange={handleFileUpload}
                  className="block w-full text-xs text-slate-500 file:mr-4 file:py-2 file:px-4 file:rounded-xl file:border-0 file:text-xs file:font-semibold file:bg-brand-50 file:text-brand-700 hover:file:bg-brand-100"
                />
              </div>

              <Button
                type="submit"
                variant="primary"
                size="md"
                loading={loading}
                icon={ShieldCheck}
                className="w-full"
              >
                Submit Document for Review
              </Button>
            </form>
          )}
        </div>
      </Card>

      {/* Success Modal */}
      {successData && (
        <SuccessModal
          isOpen={true}
          onClose={() => setSuccessData(null)}
          title="Identity Verified"
          message={`Your ${successData.type} verification was approved. Account limits updated to ${successData.tier}. Reference: ${successData.reference}`}
          buttonText="Done"
        />
      )}
    </>
  );
}
