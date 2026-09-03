import React, { useState } from 'react';
import Modal from '../common/Modal';
import Tabs from '../common/Tabs';
import Input from '../forms/Input';
import Select from '../forms/Select';
import PinInput from '../forms/PinInput';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import SuccessModal from '../feedback/SuccessModal';
import { Send, Building2, User, CheckCircle2 } from 'lucide-react';
import apiClient from '../../services/api/client';
import { parseProblemDetails } from '../../utils/problemDetails';
import { useToast } from '../../hooks/useToast';

const MAJOR_NIGERIAN_BANKS = [
  { value: '044', label: 'Access Bank' },
  { value: '058', label: 'Guaranty Trust Bank (GTBank)' },
  { value: '011', label: 'First Bank of Nigeria' },
  { value: '033', label: 'United Bank for Africa (UBA)' },
  { value: '057', label: 'Zenith Bank' },
  { value: '035', label: 'Wema Bank' },
  { value: '101', label: 'Providus Bank' },
  { value: '232', label: 'Sterling Bank' },
  { value: '070', label: 'Fidelity Bank' },
  { value: '214', label: 'First City Monument Bank (FCMB)' },
  { value: '082', label: 'Keystone Bank' },
  { value: '076', label: 'Polaris Bank' },
  { value: '221', label: 'Stanbic IBTC Bank' },
  { value: '032', label: 'Union Bank of Nigeria' },
  { value: '090110', label: 'VFD Microfinance Bank' },
  { value: '090267', label: 'Kuda Bank' },
  { value: '090405', label: 'Moniepoint MFB' },
  { value: '100004', label: 'Opay (Paycom)' },
  { value: '100033', label: 'Palmpay' }
];

/**
 * Transfer modal for peer and commercial bank payouts.
 */
export default function QuickTransferModal({
  isOpen,
  onClose,
  onSuccess
}) {
  const { showSuccess } = useToast();
  const [transferType, setTransferType] = useState('peer');

  // Form States
  const [recipient, setRecipient] = useState('');
  const [bankCode, setBankCode] = useState('044');
  const [accountNumber, setAccountNumber] = useState('');
  const [resolvedAccountName, setResolvedAccountName] = useState('');
  const [resolvingAccount, setResolvingAccount] = useState(false);
  const [amount, setAmount] = useState('');
  const [pin, setPin] = useState('');

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [successData, setSuccessData] = useState(null);

  const transferTabs = [
    { id: 'peer', label: 'CebizPay Peer Transfer' },
    { id: 'bank', label: 'Commercial Bank Payout' }
  ];

  const resetForm = () => {
    setRecipient('');
    setAccountNumber('');
    setResolvedAccountName('');
    setAmount('');
    setPin('');
    setError(null);
  };

  const handleClose = () => {
    resetForm();
    onClose();
  };

  // Resolve account name
  const handleResolveAccount = async (acct, bank) => {
    if (acct.length === 10 && bank) {
      setResolvingAccount(true);
      try {
        const res = await apiClient.get('/wallet/transfer/resolve-account', {
          params: { bankCode: bank, accountNumber: acct }
        });
        if (res.accountName) {
          setResolvedAccountName(res.accountName);
        }
      } catch {
        setResolvedAccountName('');
      } finally {
        setResolvingAccount(false);
      }
    } else {
      setResolvedAccountName('');
    }
  };

  const handleAccountChange = (e) => {
    const val = e.target.value.replace(/\D/g, '').slice(0, 10);
    setAccountNumber(val);
    if (val.length === 10) {
      handleResolveAccount(val, bankCode);
    } else {
      setResolvedAccountName('');
    }
  };

  const handleBankChange = (e) => {
    const bCode = e.target.value;
    setBankCode(bCode);
    if (accountNumber.length === 10) {
      handleResolveAccount(accountNumber, bCode);
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    const numAmount = parseFloat(amount);

    if (!numAmount || numAmount <= 0) {
      setError('Please enter a valid transfer amount greater than zero.');
      return;
    }

    if (pin.length < 4) {
      setError('Please enter your 4-digit transaction PIN.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      let result;
      if (transferType === 'peer') {
        if (!recipient.trim()) {
          setError('Please enter the recipient email or phone number.');
          setLoading(false);
          return;
        }

        result = await apiClient.postFinancial('/wallet/transfer/peer', {
          recipientIdentifier: recipient.trim(),
          amount: numAmount,
          currency: 'NGN',
          transactionPin: pin
        });
      } else {
        if (accountNumber.length !== 10) {
          setError('Destination account number must be exactly 10 digits.');
          setLoading(false);
          return;
        }

        result = await apiClient.postFinancial('/wallet/transfer/bank', {
          destinationBankCode: bankCode,
          destinationAccountNumber: accountNumber,
          amount: numAmount,
          currency: 'NGN',
          transactionPin: pin
        });
      }

      setSuccessData({
        amount: numAmount,
        recipient: transferType === 'peer' ? recipient : (resolvedAccountName || accountNumber),
        reference: result?.reference || result?.transactionId || 'Completed'
      });

      showSuccess('Transfer processed successfully.');
      resetForm();
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Transfer failed. Please check your balance and transaction PIN.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <>
      <Modal
        isOpen={isOpen && !successData}
        onClose={handleClose}
        title="Send Money & Transfers"
        subtitle="Instant ledger settlement and inter-bank payments"
        maxWidth="max-w-md"
      >
        <div className="space-y-4 pt-1">
          <Tabs
            variant="segmented"
            tabs={transferTabs}
            activeTab={transferType}
            onChange={(tab) => {
              setTransferType(tab);
              setError(null);
            }}
            className="w-full"
          />

          {error && (
            <Alert variant="danger" onClose={() => setError(null)}>
              {error}
            </Alert>
          )}

          <form onSubmit={handleSubmit} className="space-y-4">
            {transferType === 'peer' ? (
              <Input
                label="Recipient Email or Phone"
                placeholder="user@cebizpay.com or 080..."
                value={recipient}
                onChange={(e) => {
                  setRecipient(e.target.value);
                  if (error) setError(null);
                }}
                icon={User}
                required
              />
            ) : (
              <>
                <Select
                  label="Destination Commercial Bank"
                  options={MAJOR_NIGERIAN_BANKS}
                  value={bankCode}
                  onChange={handleBankChange}
                  required
                />

                <Input
                  label="Account Number (10 Digits)"
                  type="text"
                  inputMode="numeric"
                  maxLength={10}
                  placeholder="0123456789"
                  value={accountNumber}
                  onChange={handleAccountChange}
                  icon={Building2}
                  required
                />

                {resolvingAccount && (
                  <p className="text-xs text-slate-500 animate-pulse">Resolving beneficiary name...</p>
                )}

                {resolvedAccountName && (
                  <div className="p-2.5 bg-brand-50 border border-brand-100 rounded-xl flex items-center gap-2 text-xs text-brand-700 font-semibold">
                    <CheckCircle2 size={15} className="text-brand-600 shrink-0" />
                    <span>Beneficiary: {resolvedAccountName}</span>
                  </div>
                )}
              </>
            )}

            <Input
              label="Transfer Amount (₦)"
              type="number"
              step="0.01"
              placeholder="0.00"
              value={amount}
              onChange={(e) => {
                setAmount(e.target.value);
                if (error) setError(null);
              }}
              required
            />

            <div className="pt-2">
              <PinInput
                label="Authorize with 4-Digit PIN"
                value={pin}
                onChange={(val) => {
                  setPin(val);
                  if (error) setError(null);
                }}
              />
            </div>

            <div className="flex items-center gap-3 pt-3 border-t border-slate-100">
              <Button
                variant="outline"
                size="md"
                onClick={handleClose}
                disabled={loading}
                className="flex-1"
              >
                Cancel
              </Button>
              <Button
                type="submit"
                variant="primary"
                size="md"
                loading={loading}
                icon={Send}
                className="flex-1"
              >
                Send Transfer
              </Button>
            </div>
          </form>
        </div>
      </Modal>

      {/* Success Modal */}
      {successData && (
        <SuccessModal
          isOpen={true}
          onClose={() => {
            setSuccessData(null);
            handleClose();
          }}
          title="Transfer Successful"
          message={`Successfully transferred ₦${successData.amount.toLocaleString()} to ${successData.recipient}. Reference: ${successData.reference}`}
          buttonText="Done"
        />
      )}
    </>
  );
}
