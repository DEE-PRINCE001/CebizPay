import React, { useState } from 'react';
import PageHeader from '../../components/common/PageHeader';
import Tabs from '../../components/common/Tabs';
import PinModal from '../../components/common/PinModal';
import Badge from '../../components/common/Badge';
import { useToast } from '../../context/ToastContext';
import { formatCurrency } from '../../utils/formatters';
import { VAS_NETWORKS } from '../../utils/constants';
import { Smartphone, Wifi, Zap, CheckCircle2, AlertCircle } from 'lucide-react';

export default function VasPage() {
  const [activeTab, setActiveTab] = useState('airtime'); // 'airtime' | 'data'
  const [phoneNumber, setPhoneNumber] = useState('08031234567');
  const [selectedNetwork, setSelectedNetwork] = useState('MTN');
  const [airtimeAmount, setAirtimeAmount] = useState('2000');
  const [selectedBundle, setSelectedBundle] = useState('bundle-10gb');

  const [showPinModal, setShowPinModal] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const { showSuccess, showError } = useToast();

  // Data bundles
  const dataBundles = [
    { id: 'bundle-1.5gb', name: '1.5 GB Monthly Plan', validity: '30 Days', price: 1200.0, network: 'MTN' },
    { id: 'bundle-4.5gb', name: '4.5 GB Monthly Plan', validity: '30 Days', price: 2500.0, network: 'MTN' },
    { id: 'bundle-10gb', name: '10 GB Monthly Plan', validity: '30 Days', price: 3500.0, network: 'MTN' },
    { id: 'bundle-25gb', name: '25 GB Monthly Plan', validity: '30 Days', price: 6500.0, network: 'MTN' },
    { id: 'bundle-50gb', name: '50 GB Mega Plan', validity: '30 Days', price: 11000.0, network: 'MTN' },
    { id: 'bundle-100gb', name: '100 GB Ultra Plan', validity: '60 Days', price: 20000.0, network: 'MTN' }
  ];

  // Auto-detect Telco operator based on prefix
  const handlePhoneChange = (val) => {
    setPhoneNumber(val);
    const clean = val.replace(/\D/g, '');
    if (clean.length >= 4) {
      const prefix = clean.slice(0, 4);
      if (['0803', '0806', '0703', '0706', '0813', '0816', '0810', '0814', '0903', '0906'].includes(prefix)) {
        setSelectedNetwork('MTN');
      } else if (['0802', '0808', '0708', '0812', '0701', '0902', '0901', '0904'].includes(prefix)) {
        setSelectedNetwork('AIRTEL');
      } else if (['0805', '0807', '0705', '0815', '0811', '0905', '0915'].includes(prefix)) {
        setSelectedNetwork('GLO');
      } else if (['0809', '0817', '0818', '0909', '0908'].includes(prefix)) {
        setSelectedNetwork('9MOBILE');
      }
    }
  };

  const handleStartPurchase = (e) => {
    e.preventDefault();
    if (!phoneNumber || phoneNumber.length < 11) {
      showError('Invalid Phone', 'Please provide a valid 11-digit Nigerian mobile phone number.');
      return;
    }
    setShowPinModal(true);
  };

  const handleConfirmPin = (pin) => {
    setShowPinModal(false);
    setIsSubmitting(true);

    setTimeout(() => {
      setIsSubmitting(false);
      const bundle = dataBundles.find((b) => b.id === selectedBundle);
      const cost = activeTab === 'airtime' ? parseFloat(airtimeAmount) : bundle?.price || 3500;

      showSuccess(
        `${activeTab === 'airtime' ? 'Airtime' : 'Data Bundle'} Top-Up Complete`,
        `Successfully delivered ${activeTab === 'airtime' ? formatCurrency(cost) : bundle?.name} to ${phoneNumber} on ${selectedNetwork}.`,
        `VAS-${selectedNetwork}-${Date.now()}`
      );
    }, 1200);
  };

  return (
    <div className="max-w-3xl mx-auto">
      <PageHeader
        title="Value-Added Services (VAS)"
        subtitle="Instant mobile airtime recharge and broadband data bundles with automated network carrier detection and duplicate purchase guards."
      />

      <Tabs
        tabs={[
          { id: 'airtime', label: 'Airtime Top-Up', icon: Smartphone },
          { id: 'data', label: 'Data Bundles', icon: Wifi }
        ]}
        activeTab={activeTab}
        onChange={setActiveTab}
        className="mb-6"
      />

      <div className="bg-white rounded-3xl border border-slate-200/80 p-6 sm:p-8 shadow-xs text-xs text-left">
        <form onSubmit={handleStartPurchase} className="space-y-6">
          {/* Operator Selector with Network Brand Badges */}
          <div>
            <label className="block font-semibold text-slate-700 mb-2">Telecommunication Network</label>
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-2.5">
              {VAS_NETWORKS.map((net) => {
                const isSelected = selectedNetwork === net.id;
                return (
                  <button
                    key={net.id}
                    type="button"
                    onClick={() => setSelectedNetwork(net.id)}
                    className={`p-3 rounded-2xl border text-center transition-all flex flex-col items-center gap-1.5 ${
                      isSelected
                        ? `${net.border} bg-slate-900 text-white font-bold shadow-xs scale-102`
                        : 'border-slate-200 bg-white hover:bg-slate-50 text-slate-700'
                    }`}
                  >
                    <span className={`w-3 h-3 rounded-full ${net.color}`} />
                    <span className="text-xs font-bold">{net.name}</span>
                  </button>
                );
              })}
            </div>
          </div>

          {/* Mobile Phone Input */}
          <div>
            <label className="block font-semibold text-slate-700 mb-1.5">Recipient Mobile Number</label>
            <div className="relative">
              <input
                type="tel"
                required
                maxLength={11}
                value={phoneNumber}
                onChange={(e) => handlePhoneChange(e.target.value)}
                placeholder="08012345678"
                className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl font-mono text-base font-bold focus:border-blue-600 focus:ring-2 focus:ring-blue-500/20 outline-hidden"
              />
              <div className="absolute inset-y-0 right-0 pr-3.5 flex items-center pointer-events-none">
                <Badge status="ACTIVE" label={selectedNetwork} size="sm" />
              </div>
            </div>
          </div>

          {/* Airtime Chips vs Data Bundle Grid */}
          {activeTab === 'airtime' ? (
            <div>
              <label className="block font-semibold text-slate-700 mb-2">Recharge Amount (₦)</label>
              <div className="grid grid-cols-4 gap-2 mb-3">
                {['500', '1000', '2000', '5000'].map((amt) => (
                  <button
                    key={amt}
                    type="button"
                    onClick={() => setAirtimeAmount(amt)}
                    className={`py-2 rounded-xl border text-xs font-bold font-mono transition-all ${
                      airtimeAmount === amt
                        ? 'bg-blue-600 text-white border-blue-600 shadow-xs'
                        : 'bg-slate-50 text-slate-700 border-slate-200 hover:bg-slate-100'
                    }`}
                  >
                    ₦{parseInt(amt).toLocaleString()}
                  </button>
                ))}
              </div>
              <input
                type="number"
                required
                value={airtimeAmount}
                onChange={(e) => setAirtimeAmount(e.target.value)}
                className="w-full px-3.5 py-2.5 bg-white border border-slate-200 rounded-xl font-mono text-base font-bold"
              />
            </div>
          ) : (
            <div>
              <label className="block font-semibold text-slate-700 mb-2">Choose Data Bundle</label>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                {dataBundles.map((b) => {
                  const isSelected = selectedBundle === b.id;
                  return (
                    <div
                      key={b.id}
                      onClick={() => setSelectedBundle(b.id)}
                      className={`p-3.5 rounded-2xl border cursor-pointer transition-all ${
                        isSelected
                          ? 'border-blue-600 bg-blue-50/50 shadow-xs ring-1 ring-blue-500'
                          : 'border-slate-200 hover:border-slate-300 bg-white'
                      }`}
                    >
                      <div className="flex items-center justify-between mb-1">
                        <span className="font-bold text-slate-900 text-xs">{b.name}</span>
                        <span className="font-mono font-bold text-blue-700">{formatCurrency(b.price)}</span>
                      </div>
                      <span className="text-[11px] text-slate-400 font-medium">Validity: {b.validity}</span>
                    </div>
                  );
                })}
              </div>
            </div>
          )}

          {/* 120s Duplicate Guard Notice */}
          <div className="p-3 bg-slate-50 rounded-xl border border-slate-200 text-slate-500 flex items-center gap-2">
            <Zap className="w-4 h-4 text-amber-500 shrink-0" />
            <span>Protected by automated 120-second duplicate purchase guard to prevent duplicate debit.</span>
          </div>

          <button
            type="submit"
            disabled={isSubmitting}
            className="w-full py-3 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded-xl shadow-xs flex items-center justify-center gap-2 disabled:opacity-50"
          >
            <Zap className="w-4 h-4" />
            <span>
              Authorize {activeTab === 'airtime' ? formatCurrency(airtimeAmount) : 'Data Bundle'} Purchase
            </span>
          </button>
        </form>
      </div>

      {/* PIN Modal */}
      <PinModal
        isOpen={showPinModal}
        onClose={() => setShowPinModal(false)}
        onConfirm={handleConfirmPin}
        title={`Authorize ${selectedNetwork} ${activeTab === 'airtime' ? 'Airtime' : 'Data'} Purchase`}
        amount={
          activeTab === 'airtime'
            ? formatCurrency(airtimeAmount)
            : formatCurrency(dataBundles.find((b) => b.id === selectedBundle)?.price || 3500)
        }
        recipient={`${phoneNumber} (${selectedNetwork})`}
      />
    </div>
  );
}
