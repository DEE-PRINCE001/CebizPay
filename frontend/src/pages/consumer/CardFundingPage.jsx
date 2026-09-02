import React, { useState, useEffect } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Modal from '../../components/common/Modal';
import PinModal from '../../components/common/PinModal';
import { useToast } from '../../context/ToastContext';
import { formatCurrency, formatDate } from '../../utils/formatters';
import { cardsApi } from '../../api/cardsApi';
import { CreditCard, Plus, ShieldCheck, Trash2, CheckCircle2, Lock, RefreshCw, AlertCircle } from 'lucide-react';

export default function CardFundingPage() {
  const [showInitModal, setShowInitModal] = useState(false);
  const [showPinModal, setShowPinModal] = useState(false);
  const [fundAmount, setFundAmount] = useState('20000');
  const [selectedCardId, setSelectedCardId] = useState('');
  const [cards, setCards] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState(null);
  const { showSuccess, showError } = useToast();

  const fetchCards = async () => {
    setIsLoading(true);
    setErrorMessage(null);
    try {
      const res = await cardsApi.getSavedCards();
      if (Array.isArray(res)) {
        setCards(res);
        if (res.length > 0) {
          setSelectedCardId(res[0].id);
        }
      } else {
        setCards([]);
      }
    } catch (err) {
      setCards([]);
      console.warn('Backend saved cards fetch:', err);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchCards();
  }, []);

  const handleStartCardCharge = (e) => {
    e.preventDefault();
    if (!selectedCardId) {
      showError('Select Card', 'Please select a tokenized card to charge.');
      return;
    }
    setShowInitModal(false);
    setShowPinModal(true);
  };

  const handlePinConfirm = async (pin) => {
    setShowPinModal(false);
    setIsLoading(true);

    const card = cards.find((c) => c.id === selectedCardId);
    const amt = parseFloat(fundAmount);
    const idempotencyKey = 'fund_' + Date.now().toString(36) + Math.random().toString(36).substring(2, 6);

    try {
      await cardsApi.chargeSavedCard(selectedCardId, amt, 'NGN', idempotencyKey);
      showSuccess(
        'Card Funded Successfully',
        `${formatCurrency(amt)} deposited into wallet via tokenized card (•••• ${card?.last4 || '4119'}).`,
        `TXN-CARD-${Date.now()}`
      );
      await fetchCards();
    } catch (err) {
      const msg = err.message || 'Failed to charge tokenized card.';
      showError('Card Funding Error', msg);
    } finally {
      setIsLoading(false);
    }
  };

  const handleDeleteCard = async (cardId) => {
    try {
      await cardsApi.revokeSavedCard(cardId);
      setCards((prev) => prev.filter((c) => c.id !== cardId));
      showSuccess('Card Revoked', 'Tokenized card removed from your wallet.');
    } catch (err) {
      showError('Failed to Revoke Card', err.message || 'Could not delete tokenized card.');
    }
  };

  const handleSetDefault = async (cardId) => {
    try {
      await cardsApi.setDefaultCard(cardId);
      setCards((prev) =>
        prev.map((c) => ({ ...c, isDefault: c.id === cardId }))
      );
      showSuccess('Default Card Set', 'Primary funding card updated.');
    } catch (err) {
      showError('Failed to Set Default', err.message || 'Could not set default card.');
    }
  };

  const columns = [
    {
      header: 'Card Brand & Number',
      accessor: 'brand',
      render: (row) => (
        <div className="flex items-center gap-2.5">
          <div className="p-2 rounded-lg bg-slate-100 text-slate-800 font-bold text-xs">
            {row.brand || 'Card'}
          </div>
          <div>
            <span className="font-mono font-bold text-slate-900 block">•••• •••• •••• {row.last4}</span>
            <span className="text-[10px] text-slate-400">{row.bank || 'Commercial Bank'}</span>
          </div>
        </div>
      ),
    },
    {
      header: 'Expiry Date',
      accessor: 'expMonth',
      render: (row) => <span className="font-mono text-slate-700">{row.expMonth?.toString().padStart(2, '0')}/{row.expYear}</span>,
    },
    {
      header: 'Default Status',
      accessor: 'isDefault',
      render: (row) => (
        row.isDefault ? (
          <Badge status="ACTIVE" label="Primary Card" size="sm" />
        ) : (
          <span className="text-slate-400 text-xs">—</span>
        )
      ),
    },
    {
      header: 'Actions',
      align: 'right',
      render: (row) => (
        <div className="flex items-center justify-end gap-1.5">
          {!row.isDefault && (
            <button
              onClick={() => handleSetDefault(row.id)}
              className="px-2.5 py-1 text-xs font-bold text-slate-700 bg-slate-100 hover:bg-slate-200 rounded-lg cursor-pointer"
            >
              Set Primary
            </button>
          )}
          <button
            onClick={() => handleDeleteCard(row.id)}
            className="p-1 text-slate-400 hover:text-rose-600 rounded cursor-pointer"
            title="Delete Card"
          >
            <Trash2 className="w-4 h-4" />
          </button>
        </div>
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title="Card Funding &amp; Saved Cards"
        subtitle="Fund your personal wallet instantly using 3D-Secure tokenized cards with zero-auth micro-charge verification."
        actions={
          <button
            onClick={() => setShowInitModal(true)}
            disabled={cards.length === 0}
            className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs cursor-pointer disabled:opacity-50"
          >
            <CreditCard className="w-3.5 h-3.5" />
            Deposit with Saved Card
          </button>
        }
      />

      {isLoading ? (
        <div className="p-12 text-center text-xs text-slate-400 bg-white rounded-3xl border border-slate-200">
          <RefreshCw className="w-6 h-6 animate-spin mx-auto mb-2 text-blue-600" />
          Loading tokenized payment cards...
        </div>
      ) : cards.length === 0 ? (
        <div className="p-12 text-center text-xs text-slate-500 bg-white rounded-3xl border border-dashed border-slate-200">
          <CreditCard className="w-10 h-10 mx-auto mb-3 text-slate-300" />
          <h4 className="font-bold text-slate-900 text-sm">No Tokenized Cards Found</h4>
          <p className="mt-1 text-slate-400 max-w-sm mx-auto">
            Add a debit card during checkout or wallet funding to enable 1-click tokenized recurring deposits.
          </p>
        </div>
      ) : (
        <DataTable
          columns={columns}
          data={cards}
          searchPlaceholder="Search saved cards..."
        />
      )}

      {/* Card Deposit Modal */}
      <Modal
        isOpen={showInitModal}
        onClose={() => setShowInitModal(false)}
        title="Deposit Funds via Saved Card"
        subtitle="Choose a tokenized payment card to charge."
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button onClick={() => setShowInitModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl cursor-pointer">Cancel</button>
            <button onClick={handleStartCardCharge} className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl shadow-xs cursor-pointer">Proceed to PIN</button>
          </div>
        }
      >
        <form onSubmit={handleStartCardCharge} className="space-y-4 text-xs text-left">
          <div>
            <label className="block font-semibold text-slate-700 mb-1.5">Select Saved Card</label>
            <select
              value={selectedCardId}
              onChange={(e) => setSelectedCardId(e.target.value)}
              className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl font-bold"
            >
              {cards.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.brand || 'Card'} (•••• {c.last4}) — {c.bank || 'Bank'} {c.isDefault ? '[Primary]' : ''}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="block font-semibold text-slate-700 mb-1.5">Deposit Amount (₦)</label>
            <input
              type="number"
              required
              value={fundAmount}
              onChange={(e) => setFundAmount(e.target.value)}
              className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl font-mono text-base font-bold"
            />
          </div>
          <div className="p-3 bg-slate-50 rounded-xl border border-slate-200 text-slate-500">
            Card deposits are protected by 3D-Secure 2.0 tokenization and verified via gateway settlement rails.
          </div>
        </form>
      </Modal>

      {/* PIN Modal */}
      <PinModal
        isOpen={showPinModal}
        onClose={() => setShowPinModal(false)}
        onConfirm={handlePinConfirm}
        title="Authorize Card Deposit"
        amount={formatCurrency(fundAmount)}
        recipient="Personal CebizPay Wallet"
      />
    </div>
  );
}
