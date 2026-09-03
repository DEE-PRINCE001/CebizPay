import React, { useState, useEffect } from 'react';
import Modal from '../common/Modal';
import Input from '../forms/Input';
import Textarea from '../forms/Textarea';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import { Briefcase, DollarSign, Check, Plus } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Modal to create or edit an ERP billable service offering.
 */
export default function AddServiceModal({
  isOpen,
  onClose,
  editingService = null,
  onSuccess
}) {
  const { showSuccess } = useToast();

  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [unitPrice, setUnitPrice] = useState('');
  const [description, setDescription] = useState('');

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (editingService) {
      setCode(editingService.code || '');
      setName(editingService.name || '');
      setUnitPrice(editingService.unitPrice?.toString() || '');
      setDescription(editingService.description || '');
      setError(null);
    } else {
      setCode('');
      setName('');
      setUnitPrice('');
      setDescription('');
      setError(null);
    }
  }, [editingService, isOpen]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    const price = parseFloat(unitPrice);

    if (!name.trim()) {
      setError('Service name is required.');
      return;
    }

    if (!editingService && !code.trim()) {
      setError('Service code is required.');
      return;
    }

    if (isNaN(price) || price < 0) {
      setError('Please provide a valid unit rate / price.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      if (editingService) {
        await apiClient.put(`/org/services/${editingService.id}`, {
          name: name.trim(),
          unitPrice: price,
          description: description.trim() || null
        });
        showSuccess(`Service "${name}" updated.`);
      } else {
        await apiClient.post('/org/services', {
          code: code.trim().toUpperCase(),
          name: name.trim(),
          unitPrice: price,
          description: description.trim() || null,
          currency: 'NGN'
        });
        showSuccess(`Service "${name}" added to catalog.`);
      }

      onClose();
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to save service.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={editingService ? 'Edit Billable Service' : 'Add Billable Service'}
      subtitle="Catalog of professional services, consulting fees, and hourly billing rates"
      maxWidth="max-w-md"
    >
      <form onSubmit={handleSubmit} className="space-y-4 pt-1">
        {error && (
          <Alert variant="danger" onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        <div className="grid grid-cols-2 gap-3">
          <Input
            label="Service Code"
            placeholder="e.g. SVC-CONSULT"
            value={code}
            onChange={(e) => {
              setCode(e.target.value);
              if (error) setError(null);
            }}
            disabled={!!editingService}
            required={!editingService}
          />
          <Input
            label="Service Name"
            placeholder="e.g. Legal Advisory"
            value={name}
            onChange={(e) => {
              setName(e.target.value);
              if (error) setError(null);
            }}
            required
          />
        </div>

        <Input
          label="Billing Rate / Unit Price (₦)"
          type="number"
          min="0"
          step="500"
          placeholder="e.g. 50000"
          value={unitPrice}
          onChange={(e) => {
            setUnitPrice(e.target.value);
            if (error) setError(null);
          }}
          required
        />

        <Textarea
          label="Service Scope / Description"
          rows={3}
          placeholder="Deliverables, hourly rate terms, or SLA notes..."
          value={description}
          onChange={(e) => setDescription(e.target.value)}
        />

        <div className="flex items-center gap-3 pt-3 border-t border-slate-100">
          <Button
            variant="outline"
            size="md"
            onClick={onClose}
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
            icon={editingService ? Check : Briefcase}
            className="flex-1"
          >
            {editingService ? 'Save Changes' : 'Add Service'}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
