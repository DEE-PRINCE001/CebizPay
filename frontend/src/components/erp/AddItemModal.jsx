import React, { useState, useEffect } from 'react';
import Modal from '../common/Modal';
import Input from '../forms/Input';
import Select from '../forms/Select';
import Textarea from '../forms/Textarea';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import { PackagePlus, Barcode, DollarSign, Check, Plus } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

const CATEGORIES = [
  { value: 'Electronics', label: 'Electronics & Gadgets' },
  { value: 'FMCG', label: 'Fast-Moving Consumer Goods (FMCG)' },
  { value: 'OfficeSupplies', label: 'Office Supplies & Stationery' },
  { value: 'RawMaterials', label: 'Raw Materials & Components' },
  { value: 'Apparel', label: 'Apparel & Uniforms' },
  { value: 'GeneralGoods', label: 'General Merchandise' }
];

const UNITS = [
  { value: 'Units', label: 'Units (pcs)' },
  { value: 'Boxes', label: 'Boxes / Cartons' },
  { value: 'Kg', label: 'Kilograms (kg)' },
  { value: 'Litres', label: 'Litres (L)' },
  { value: 'Packs', label: 'Packs' },
  { value: 'Meters', label: 'Meters (m)' }
];

/**
 * Modal to create or edit an ERP inventory stock item.
 */
export default function AddItemModal({
  isOpen,
  onClose,
  editingItem = null,
  onSuccess
}) {
  const { showSuccess } = useToast();

  const [formData, setFormData] = useState({
    sku: '',
    name: '',
    category: 'GeneralGoods',
    unitOfMeasure: 'Units',
    sellingPrice: '',
    initialUnitCost: '',
    initialQuantity: '0',
    reorderLevel: '5',
    description: ''
  });

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (editingItem) {
      setFormData({
        sku: editingItem.sku || '',
        name: editingItem.name || '',
        category: editingItem.category || 'GeneralGoods',
        unitOfMeasure: editingItem.unitOfMeasure || 'Units',
        sellingPrice: editingItem.sellingPrice?.toString() || '',
        initialUnitCost: editingItem.averageCost?.toString() || '',
        initialQuantity: editingItem.quantityOnHand?.toString() || '0',
        reorderLevel: editingItem.reorderLevel?.toString() || '5',
        description: editingItem.description || ''
      });
      setError(null);
    } else {
      setFormData({
        sku: '',
        name: '',
        category: 'GeneralGoods',
        unitOfMeasure: 'Units',
        sellingPrice: '',
        initialUnitCost: '',
        initialQuantity: '0',
        reorderLevel: '5',
        description: ''
      });
      setError(null);
    }
  }, [editingItem, isOpen]);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
    if (error) setError(null);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();

    const price = parseFloat(formData.sellingPrice);
    const cost = parseFloat(formData.initialUnitCost) || 0;
    const qty = parseFloat(formData.initialQuantity) || 0;
    const reorder = parseInt(formData.reorderLevel, 10) || 0;

    if (!formData.name.trim()) {
      setError('Item name is required.');
      return;
    }

    if (!editingItem && !formData.sku.trim()) {
      setError('SKU / Item code is required.');
      return;
    }

    if (isNaN(price) || price <= 0) {
      setError('Please provide a valid positive selling price.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      if (editingItem) {
        await apiClient.put(`/org/inventory/items/${editingItem.id}`, {
          name: formData.name.trim(),
          unitOfMeasure: formData.unitOfMeasure,
          sellingPrice: price,
          description: formData.description.trim() || null,
          category: formData.category,
          reorderLevel: reorder
        });
        showSuccess(`Item "${formData.name}" updated successfully.`);
      } else {
        await apiClient.post('/org/inventory/items', {
          sku: formData.sku.trim().toUpperCase(),
          name: formData.name.trim(),
          unitOfMeasure: formData.unitOfMeasure,
          sellingPrice: price,
          description: formData.description.trim() || null,
          category: formData.category,
          reorderLevel: reorder,
          currency: 'NGN',
          initialQuantity: qty,
          initialUnitCost: cost
        });
        showSuccess(`Inventory item "${formData.name}" created.`);
      }

      onClose();
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Failed to save inventory item.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={editingItem ? 'Edit Inventory Item' : 'New Inventory Stock Item'}
      subtitle="Stock catalog tracking, valuation costs, and automated reorder alerts"
      maxWidth="max-w-lg"
    >
      <form onSubmit={handleSubmit} className="space-y-4 pt-1">
        {error && (
          <Alert variant="danger" onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        <div className="grid grid-cols-2 gap-3">
          <Input
            label="SKU / Barcode"
            name="sku"
            placeholder="e.g. SKU-84920"
            value={formData.sku}
            onChange={handleChange}
            icon={Barcode}
            disabled={!!editingItem}
            required={!editingItem}
          />
          <Input
            label="Item Name"
            name="name"
            placeholder="e.g. Wireless Barcode Scanner"
            value={formData.name}
            onChange={handleChange}
            required
          />
        </div>

        <div className="grid grid-cols-2 gap-3">
          <Select
            label="Category"
            name="category"
            options={CATEGORIES}
            value={formData.category}
            onChange={handleChange}
          />
          <Select
            label="Unit of Measure"
            name="unitOfMeasure"
            options={UNITS}
            value={formData.unitOfMeasure}
            onChange={handleChange}
          />
        </div>

        <div className="grid grid-cols-2 gap-3">
          <Input
            label="Selling Price (₦)"
            type="number"
            min="0"
            step="50"
            name="sellingPrice"
            placeholder="e.g. 15000"
            value={formData.sellingPrice}
            onChange={handleChange}
            required
          />
          <Input
            label="Reorder Alert Level"
            type="number"
            min="0"
            name="reorderLevel"
            placeholder="e.g. 10"
            value={formData.reorderLevel}
            onChange={handleChange}
            helperText="Low stock threshold alert."
          />
        </div>

        {!editingItem && (
          <div className="grid grid-cols-2 gap-3 p-3 bg-slate-50 border border-slate-200/80 rounded-2xl">
            <Input
              label="Opening Quantity"
              type="number"
              min="0"
              name="initialQuantity"
              value={formData.initialQuantity}
              onChange={handleChange}
            />
            <Input
              label="Initial Unit Cost (₦)"
              type="number"
              min="0"
              step="50"
              name="initialUnitCost"
              placeholder="e.g. 9500"
              value={formData.initialUnitCost}
              onChange={handleChange}
            />
          </div>
        )}

        <Textarea
          label="Item Description"
          name="description"
          rows={2}
          placeholder="Product specifications or supplier notes (optional)..."
          value={formData.description}
          onChange={handleChange}
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
            icon={editingItem ? Check : PackagePlus}
            className="flex-1"
          >
            {editingItem ? 'Save Changes' : 'Create Item'}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
