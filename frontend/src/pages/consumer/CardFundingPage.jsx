import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import DataTable from '../../components/common/DataTable';
import Badge from '../../components/common/Badge';
import Modal from '../../components/common/Modal';
import PinModal from '../../components/common/PinModal';
import { useToast } from '../../context/ToastContext';
import { formatCurrency, formatDate } from '../../utils/formatters';
import { CreditCard, Plus, ShieldCheck, Trash2, CheckCircle2, Lock } from 'lucide-react';

export default function CardFundingPage() {
  const [showInitModal, setShowInitModal] = useState(false);
  const [showPinModal, setShowPinModal] = useState(false);
  const [fundAmount, setFundAmount] = useState('20000');
  const [selectedCardId, setSelectedCardId] = useState('card-01');
  const { showSuccess } = useToast();

  const [cards, setCards] = useState([
    {
      id: 'card-01',
      brand: 'Mastercard',
      last4: '4119',
      expMonth: 12,
      expYear: 2028,
      bank: 'GTBank',
      isDefault: true,
      createdAt: '2026-06-15T10:00:00Z'
    },
    {
      id: 'card-02',
      brand: 'Visa',
      last4: '8832',
      expMonth: 8,
      expYear: 2027,
      bank: 'Access Bank',
      isDefault: false,
      createdAt: '2026-07-20T14:30:00Z'
    }
  ]);

  const handleStartCardCharge = (e) => {
    e.preventDefault();
    setShowInitModal(false);
    setShowPinModal(true);
  };

  const handlePinConfirm = (pin) => {
    setShowPinModal(false);
    const card = cards.find((c) => c.id === selectedCardId);
    showSuccess(
      'Card Funded Successfully',
      `${formatCurrency(fundAmount)} deposited into wallet via saved ${card?.brand || 'Card'} (•••• ${card?.last4 || '4119'}).`,
      `TXN-CARD-${Date.now()}`
    );
  };

  const handleDeleteCard = (cardId) => {
    setCards((prev) => prev.filter((c) => c.id !== cardId));
    showSuccess('Card Revoked', 'Tokenized card removed from your wallet.');
  };

  const handleSetDefault = (cardId) => {
    setCards((prev) =>
      prev.map((c) => ({ ...c, isDefault: c.id === cardId }))
    );
    showSuccess('Default Card Set', 'Primary funding card updated.');
  };

  const columns = [
    {
      header: 'Card Brand & Number',
      accessor: 'brand',
      render: (row) => (
        <div className="flex items-center gap-2.5">
          <div className="p-2 rounded-lg bg-slate-100 text-slate-800 font-bold text-xs">
            {row.brand}
          </div>
          <div>
            <span className="font-mono font-bold text-slate-900 block">•••• •••• •••• {row.last4}</span>
            <span className="text-[10px] text-slate-400">{row.bank}</span>
          </div>
        </div>
      )
    },
    {
      header: 'Expiry Date',
      accessor: 'expMonth',
      render: (row) => <span className="font-mono text-slate-700">{row.expMonth.toString().padStart(2, '0')}/{row.expYear}</span>
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
      )
    },
    {
      header: 'Actions',
      align: 'right',
      render: (row) => (
        <div className="flex items-center justify-end gap-1.5">
          {!row.isDefault && (
            <button
              onClick={() => handleSetDefault(row.id)}
              className="px-2.5 py-1 text-xs font-bold text-slate-700 bg-slate-100 hover:bg-slate-200 rounded-lg"
            >
              Set Primary
            </button>
          )}
          <button
            onClick={() => handleDeleteCard(row.id)}
            className="p-1 text-slate-400 hover:text-rose-600 rounded"
            title="Delete Card"
          >
            <Trash2 className="w-4 h-4" />
          </button>
        </div>
      )
    }
  ];

  return (
    <div>
      <PageHeader
        title="Card Funding &amp; Saved Cards"
        subtitle="Fund your personal wallet instantly using 3D-Secure tokenized cards with zero-auth micro-charge verification."
        actions={
          <button
            onClick={() => setShowInitModal(true)}
            className="flex items-center gap-2 px-3.5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl hover:bg-blue-700 shadow-xs"
          >
            <CreditCard className="w-3.5 h-3.5" />
            Deposit with Saved Card
          </button>
        }
      />

      {/* Cards Table */}
      <DataTable
        columns={columns}
        data={cards}
        searchPlaceholder="Search saved cards..."
      />

      {/* Card Deposit Modal */}
      <Modal
        isOpen={showInitModal}
        onClose={() => setShowInitModal(false)}
        title="Deposit Funds via Saved Card"
        subtitle="Choose a tokenized payment card to charge."
        footer={
          <div className="flex items-center justify-end gap-3 w-full">
            <button onClick={() => setShowInitModal(false)} className="px-4 py-2 text-xs font-semibold text-slate-700 bg-white border border-slate-300 rounded-xl">Cancel</button>
            <button onClick={handleStartCardCharge} className="px-5 py-2 text-xs font-bold text-white bg-blue-600 rounded-xl">Proceed to PIN</button>
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
                  {c.brand} (•••• {c.last4}) — {c.bank} {c.isDefault ? '[Primary]' : ''}
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
            Card deposits are protected by 3D-Secure 2.0 tokenization and verified via Flutterwave / Paystack gateway rails.
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
