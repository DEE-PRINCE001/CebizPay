import React, { useState, useEffect } from 'react';
import Modal from '../common/Modal';
import Input from '../forms/Input';
import Textarea from '../forms/Textarea';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import { Users, Mail, Phone, MapPin, Check, Plus } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Modal to create or edit a customer account.
 */
export default function AddCustomerModal({
  isOpen,
  onClose,
  editingCustomer = null,
  onSuccess
}) {
  const { showSuccess } = useToast();

  const [reference, setReference] = useState('');
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [address, setAddress] = useState('');

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (editingCustomer) {
      setReference(editingCustomer.reference || '');
      setName(editingCustomer.name || '');
      setEmail(editingCustomer.email || '');
      setPhone(editingCustomer.phone || '');
      setAddress(editingCustomer.address || '');
      setError(null);
    } else {
      setReference(`CUST-${Math.floor(100000 + Math.random() * 900000)}`);
      setName('');
      setEmail('');
      setPhone('');
      setAddress('');
      setError(null);
    }
  }, [editingCustomer, isOpen]);

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!name.trim()) {
      setError('Customer name is required.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      if (editingCustomer) {
        await apiClient.put(`/org/customers/${editingCustomer.id}`, {
          name: name.trim(),
          email: email.trim() || null,
          phone: phone.trim() || null,
          address: address.trim() || null
        });
        showSuccess(`Customer "${name}" updated.`);
      } else {
        await apiClient.post('/org/customers', {
          reference: reference.trim().toUpperCase(),
          name: name.trim(),
          email: email.trim() || null,
          phone: phone.trim() || null,
          address: address.trim() || null
        });
        showSuccess(`Customer "${name}" registered.`);
      }

      onClose();
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to save customer.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={editingCustomer ? 'Edit Customer Profile' : 'New Customer Account'}
      subtitle="Client contact details, billing address, and CRM ledger association"
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
            label="Customer Ref #"
            value={reference}
            onChange={(e) => setReference(e.target.value)}
            disabled={!!editingCustomer}
            required
          />
          <Input
            label="Customer Name"
            placeholder="e.g. John Doe / Global Tech"
            value={name}
            onChange={(e) => {
              setName(e.target.value);
              if (error) setError(null);
            }}
            icon={Users}
            required
          />
        </div>

        <div className="grid grid-cols-2 gap-3">
          <Input
            label="Email Address"
            type="email"
            placeholder="client@example.com"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            icon={Mail}
          />
          <Input
            label="Phone Number"
            type="tel"
            placeholder="0801 234 5678"
            value={phone}
            onChange={(e) => setPhone(e.target.value)}
            icon={Phone}
          />
        </div>

        <Textarea
          label="Billing / Shipping Address"
          rows={2}
          placeholder="Physical or business address..."
          value={address}
          onChange={(e) => setAddress(e.target.value)}
          icon={MapPin}
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
            icon={editingCustomer ? Check : Plus}
            className="flex-1"
          >
            {editingCustomer ? 'Save Customer' : 'Create Customer'}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
