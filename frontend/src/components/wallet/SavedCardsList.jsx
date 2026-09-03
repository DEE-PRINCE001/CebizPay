import React, { useState } from 'react';
import Card from '../common/Card';
import Button from '../common/Button';
import Badge from '../common/Badge';
import Skeleton from '../common/Skeleton';
import EmptyState from '../feedback/EmptyState';
import ConfirmModal from '../feedback/ConfirmModal';
import Modal from '../common/Modal';
import Input from '../forms/Input';
import Alert from '../feedback/Alert';
import { CreditCard, Plus, Star, Trash2, Zap, RefreshCw } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Tokenized saved cards list and recurring debit management.
 */
export default function SavedCardsList({
  cards = [],
  loading = false,
  onRefresh,
  onAddNewCard,
  className = ''
}) {
  const { showSuccess, showError } = useToast();
  const [actionLoadingId, setActionLoadingId] = useState(null);
  const [cardToDelete, setCardToDelete] = useState(null);

  // Quick Charge Modal State
  const [chargeCard, setChargeCard] = useState(null);
  const [chargeAmount, setChargeAmount] = useState('');
  const [chargeLoading, setChargeLoading] = useState(false);
  const [chargeError, setChargeError] = useState(null);

  const handleSetDefault = async (cardId) => {
    setActionLoadingId(cardId);
    try {
      await apiClient.post(`/saved-cards/${cardId}/default`);
      showSuccess('Default payment card updated.');
      if (onRefresh) onRefresh();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      showError(parsed.message || 'Failed to update default card.');
    } finally {
      setActionLoadingId(null);
    }
  };

  const handleRevoke = async () => {
    if (!cardToDelete) return;
    setActionLoadingId(cardToDelete.id);
    try {
      await apiClient.delete(`/saved-cards/${cardToDelete.id}`);
      showSuccess('Card revoked successfully.');
      setCardToDelete(null);
      if (onRefresh) onRefresh();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      showError(parsed.message || 'Failed to revoke card.');
    } finally {
      setActionLoadingId(null);
    }
  };

  const handleChargeSubmit = async (e) => {
    e.preventDefault();
    const amount = parseFloat(chargeAmount);
    if (!amount || amount < 100) {
      setChargeError('Minimum funding amount is ₦100.00');
      return;
    }

    setChargeLoading(true);
    setChargeError(null);

    try {
      await apiClient.postFinancial('/funding/card/charge-saved', {
        savedCardId: chargeCard.id,
        amount,
        currency: 'NGN'
      });
      showSuccess(`Successfully charged ₦${amount.toLocaleString()} to wallet.`);
      setChargeCard(null);
      setChargeAmount('');
      if (onRefresh) onRefresh();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setChargeError(parsed.message || 'Card charge failed.');
    } finally {
      setChargeLoading(false);
    }
  };

  return (
    <div className={`space-y-4 ${className}`}>
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
        <div>
          <h3 className="text-sm font-bold text-slate-900">Saved Cards & Payment Methods</h3>
          <p className="text-xs text-slate-500 mt-0.5">
            Tokenized debit cards for fast one-click wallet top-ups.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            icon={RefreshCw}
            onClick={onRefresh}
          >
            Refresh
          </Button>
          <Button
            variant="primary"
            size="sm"
            icon={Plus}
            onClick={onAddNewCard}
          >
            Add New Card
          </Button>
        </div>
      </div>

      {loading && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <Skeleton variant="card" count={2} />
        </div>
      )}

      {!loading && cards.length === 0 && (
        <Card padding="p-8">
          <EmptyState
            icon={CreditCard}
            title="No saved cards"
            description="Add and save a debit card to easily top up your operating wallet anytime."
            actionLabel="Add Debit Card"
            onAction={onAddNewCard}
          />
        </Card>
      )}

      {!loading && cards.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {cards.map((card) => {
            const isDefault = card.isDefault || card.default;
            const isLoading = actionLoadingId === card.id;

            return (
              <Card key={card.id} padding="p-5" className="relative flex flex-col justify-between space-y-4">
                <div className="flex items-start justify-between gap-2">
                  <div className="flex items-center gap-3">
                    <div className="w-11 h-11 rounded-xl bg-slate-900 text-white flex items-center justify-center font-bold text-xs shrink-0">
                      {card.cardBrand || card.brand || 'CARD'}
                    </div>
                    <div>
                      <div className="flex items-center gap-2">
                        <h4 className="text-sm font-bold text-slate-900 font-mono">
                          •••• •••• •••• {card.last4Digits || card.last4 || '••••'}
                        </h4>
                        {isDefault && (
                          <Badge variant="brand" size="sm">
                            Default
                          </Badge>
                        )}
                      </div>
                      <p className="text-xs text-slate-500 mt-0.5">
                        Expires {card.expiryMonth || 'MM'}/{card.expiryYear || 'YY'}
                      </p>
                    </div>
                  </div>
                </div>

                <div className="pt-3 border-t border-slate-100 flex items-center justify-between text-xs">
                  <Button
                    variant="outline"
                    size="sm"
                    icon={Zap}
                    onClick={() => {
                      setChargeCard(card);
                      setChargeAmount('');
                      setChargeError(null);
                    }}
                    className="text-xs py-1 px-2.5 h-auto"
                  >
                    Quick Top-up
                  </Button>

                  <div className="flex items-center gap-2">
                    {!isDefault && (
                      <button
                        type="button"
                        disabled={isLoading}
                        onClick={() => handleSetDefault(card.id)}
                        className="inline-flex items-center gap-1 text-xs font-semibold text-brand-600 hover:text-brand-700 hover:underline disabled:opacity-50"
                      >
                        <Star size={13} />
                        <span>Set Default</span>
                      </button>
                    )}

                    <button
                      type="button"
                      disabled={isLoading}
                      onClick={() => setCardToDelete(card)}
                      className="p-1.5 rounded-lg text-slate-400 hover:text-red-600 hover:bg-red-50 transition-colors disabled:opacity-50"
                      aria-label="Revoke Card"
                    >
                      <Trash2 size={14} />
                    </button>
                  </div>
                </div>
              </Card>
            );
          })}
        </div>
      )}

      {/* Revoke Confirmation Modal */}
      {cardToDelete && (
        <ConfirmModal
          isOpen={true}
          onClose={() => setCardToDelete(null)}
          onConfirm={handleRevoke}
          title="Revoke Saved Card"
          message={`Are you sure you want to remove card ending in •••• ${cardToDelete.last4Digits || cardToDelete.last4}?`}
          confirmText="Revoke Card"
          confirmVariant="danger"
          loading={actionLoadingId === cardToDelete.id}
        />
      )}

      {/* Quick Top-up Modal */}
      {chargeCard && (
        <Modal
          isOpen={true}
          onClose={() => setChargeCard(null)}
          title="Quick Wallet Top-up"
          subtitle={`Charge card ending in •••• ${chargeCard.last4Digits || chargeCard.last4}`}
          maxWidth="max-w-sm"
        >
          <form onSubmit={handleChargeSubmit} className="space-y-4 pt-1">
            {chargeError && (
              <Alert variant="danger" onClose={() => setChargeError(null)}>
                {chargeError}
              </Alert>
            )}

            <Input
              label="Amount to Deposit (₦)"
              type="number"
              min="100"
              step="100"
              placeholder="5000.00"
              value={chargeAmount}
              onChange={(e) => {
                setChargeAmount(e.target.value);
                if (chargeError) setChargeError(null);
              }}
              required
            />

            <div className="flex items-center gap-3 pt-2">
              <Button
                variant="outline"
                size="md"
                onClick={() => setChargeCard(null)}
                disabled={chargeLoading}
                className="flex-1"
              >
                Cancel
              </Button>
              <Button
                type="submit"
                variant="primary"
                size="md"
                loading={chargeLoading}
                icon={Zap}
                className="flex-1"
              >
                Deposit Now
              </Button>
            </div>
          </form>
        </Modal>
      )}
    </div>
  );
}
