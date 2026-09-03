import React, { useState } from 'react';
import CustomerLayout from '../../layouts/CustomerLayout';
import SavedCardsList from '../../components/wallet/SavedCardsList';
import FundWalletModal from '../../components/dashboard/FundWalletModal';
import Button from '../../components/common/Button';
import { useApiQuery } from '../../hooks/useApiQuery';
import apiClient from '../../services/api/client';
import { CreditCard, Plus, RefreshCw } from 'lucide-react';

/**
 * Saved cards and payment methods management view.
 */
export default function CardsPage() {
  const [isFundOpen, setIsFundOpen] = useState(false);

  const {
    data: savedCards,
    loading: cardsLoading,
    refetch: refetchCards
  } = useApiQuery(
    () => apiClient.get('/saved-cards').catch(() => []),
    { deps: [] }
  );

  return (
    <CustomerLayout
      title="Saved Cards"
      subtitle="Tokenized debit cards and automated recurring payment methods"
      headerAction={
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            icon={RefreshCw}
            onClick={refetchCards}
            className="hidden sm:inline-flex"
          >
            Refresh
          </Button>
          <Button
            variant="primary"
            size="sm"
            icon={Plus}
            onClick={() => setIsFundOpen(true)}
          >
            Add New Card
          </Button>
        </div>
      }
    >
      <div className="space-y-6">
        <SavedCardsList
          cards={savedCards}
          loading={cardsLoading}
          onRefresh={refetchCards}
          onAddNewCard={() => setIsFundOpen(true)}
        />
      </div>

      <FundWalletModal
        isOpen={isFundOpen}
        onClose={() => {
          setIsFundOpen(false);
          refetchCards();
        }}
      />
    </CustomerLayout>
  );
}
