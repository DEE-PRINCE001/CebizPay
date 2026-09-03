import React, { useState, useEffect } from 'react';
import Modal from '../common/Modal';
import Input from '../forms/Input';
import Textarea from '../forms/Textarea';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import { Truck, Mail, Phone, MapPin, Check, Plus } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Modal to create or edit a vendor / supplier profile.
 */
export default function AddSupplierModal({
  isOpen,
  onClose,
  editingSupplier = null,
  onSuccess
}) {
  const { showSuccess } = useToast();

  const [reference, setReference] = useState('');
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [address, setAddress] = useState('');
  const [taxIdentifier, setTaxIdentifier] = useState('');

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (editingSupplier) {
      setReference(editingSupplier.reference || '');
      setName(editingSupplier.name || '');
      setEmail(editingSupplier.email || '');
      setPhone(editingSupplier.phone || '');
      setAddress(editingSupplier.address || '');
      setTaxIdentifier(editingSupplier.taxIdentifier || '');
      setError(null);
    } else {
      setReference(`SUPP-${Math.floor(100000 + Math.random() * 900000)}`);
      setName('');
      setEmail('');
      setPhone('');
      setAddress('');
      setTaxIdentifier('');
      setError(null);
    }
  }, [editingSupplier, isOpen]);

  const handleSubmit = async (e) => {
    e.preventDefault();

    if (!name.trim()) {
      setError('Supplier name is required.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      if (editingSupplier) {
        await apiClient.put(`/org/suppliers/${editingSupplier.id}`, {
          name: name.trim(),
          email: email.trim() || null,
          phone: phone.trim() || null,
          address: address.trim() || null,
          taxIdentifier: taxIdentifier.trim() || null
        });
        showSuccess(`Supplier "${name}" updated.`);
      } else {
        await apiClient.post('/org/suppliers', {
          reference: reference.trim().toUpperCase(),
          name: name.trim(),
          email: email.trim() || null,
          phone: phone.trim() || null,
          address: address.trim() || null,
          taxIdentifier: taxIdentifier.trim() || null
        });
        showSuccess(`Supplier "${name}" registered.`);
      }

      onClose();
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to save supplier.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={editingSupplier ? 'Edit Supplier Profile' : 'New Vendor / Supplier'}
      subtitle="Procurement supplier details, contact info, and tax identity"
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
            label="Supplier Ref #"
            value={reference}
            onChange={(e) => setReference(e.target.value)}
            disabled={!!editingSupplier}
            required
          />
          <Input
            label="Supplier / Vendor Name"
            placeholder="e.g. Apex Industrial Supplies"
            value={name}
            onChange={(e) => {
              setName(e.target.value);
              if (error) setError(null);
            }}
            icon={Truck}
            required
          />
        </div>

        <div className="grid grid-cols-2 gap-3">
          <Input
            label="Email Address"
            type="email"
            placeholder="vendor@company.com"
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

        <Input
          label="Tax Identification Number (TIN)"
          placeholder="e.g. 23819042-0001"
          value={taxIdentifier}
          onChange={(e) => setTaxIdentifier(e.target.value)}
        />

        <Textarea
          label="Vendor Office Address"
          rows={2}
          placeholder="Physical business address..."
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
            icon={editingSupplier ? Check : Plus}
            className="flex-1"
          >
            {editingSupplier ? 'Save Supplier' : 'Register Supplier'}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
