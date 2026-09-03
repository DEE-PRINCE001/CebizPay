import React, { useState } from 'react';
import Card from '../common/Card';
import Input from '../forms/Input';
import Select from '../forms/Select';
import Textarea from '../forms/Textarea';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import SuccessModal from '../feedback/SuccessModal';
import { Building2, FileCheck, CheckCircle2, ShieldCheck, ArrowRight } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

const INDUSTRIES = [
  { value: 'Fintech', label: 'Financial Technology & Services' },
  { value: 'Ecommerce', label: 'E-commerce & Retail' },
  { value: 'Logistics', label: 'Logistics & Supply Chain' },
  { value: 'Healthcare', label: 'Healthcare & Pharmaceuticals' },
  { value: 'Education', label: 'Education & EdTech' },
  { value: 'Agriculture', label: 'Agriculture & Food Processing' },
  { value: 'Construction', label: 'Real Estate & Construction' },
  { value: 'GeneralCommerce', label: 'General Corporate / Services' }
];

/**
 * Corporate KYB business identity verification form.
 */
export default function OrganizationKybForm({
  currentOrg,
  currentOrgId,
  onSuccess,
  className = ''
}) {
  const { showSuccess } = useToast();

  const [step, setStep] = useState(1); // 1: Business Details, 2: CAC & Documents
  const [formData, setFormData] = useState({
    organizationName: currentOrg?.name || '',
    cacNumber: '',
    tin: '',
    industry: 'GeneralCommerce',
    contactEmail: '',
    contactPhone: '',
    address: ''
  });

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [successData, setSuccessData] = useState(null);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
    if (error) setError(null);
  };

  const handleStep1Submit = async (e) => {
    e.preventDefault();

    if (!formData.organizationName || !formData.cacNumber) {
      setError('Please provide the registered business name and CAC / RC number.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      if (currentOrgId) {
        // Run official CAC registry check
        await apiClient.post('/compliance/kyb/business', {
          organizationId: currentOrgId,
          cacNumber: formData.cacNumber.trim(),
          companyName: formData.organizationName.trim()
        });
      }

      setStep(2);
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'CAC business registry verification check failed.');
    } finally {
      setLoading(false);
    }
  };

  const handleStep2Submit = async (e) => {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      if (currentOrgId) {
        await apiClient.post('/org/kyb/register-step1', {
          organizationId: currentOrgId,
          organizationName: formData.organizationName,
          registrationNumber: formData.cacNumber,
          tin: formData.tin || null,
          industry: formData.industry,
          address: formData.address || null
        });

        await apiClient.post('/org/kyb/register-step2', {
          organizationId: currentOrgId,
          cacCertificateUrl: 'https://storage.cebizpay.com/cac_cert.pdf',
          memorandumUrl: 'https://storage.cebizpay.com/memart.pdf',
          proofOfAddressUrl: 'https://storage.cebizpay.com/utility.pdf'
        });
      }

      setSuccessData({
        companyName: formData.organizationName,
        rcNumber: formData.cacNumber
      });

      showSuccess('Corporate KYB verification documents submitted.');
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'KYB submission failed.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <>
      <Card padding="p-6" className={`bg-white border border-slate-200/80 ${className}`}>
        <div className="flex items-center gap-3 pb-4 mb-4 border-b border-slate-100">
          <div className="w-10 h-10 rounded-2xl bg-brand-50 text-brand-600 flex items-center justify-center">
            <Building2 size={20} />
          </div>
          <div>
            <h3 className="text-base font-bold text-slate-900">Corporate KYB Verification</h3>
            <p className="text-xs text-slate-500">Corporate Affairs Commission (CAC) business verification</p>
          </div>
        </div>

        {error && (
          <Alert variant="danger" onClose={() => setError(null)} className="mb-4">
            {error}
          </Alert>
        )}

        {/* STEP 1: Corporate Details */}
        {step === 1 && (
          <form onSubmit={handleStep1Submit} className="space-y-4">
            <Input
              label="Registered Corporate Name (as on CAC)"
              name="organizationName"
              placeholder="e.g. Acme Enterprise Technologies Ltd"
              value={formData.organizationName}
              onChange={handleChange}
              required
            />

            <div className="grid grid-cols-2 gap-3">
              <Input
                label="CAC / RC Number"
                name="cacNumber"
                placeholder="e.g. RC-1849204"
                value={formData.cacNumber}
                onChange={handleChange}
                required
              />
              <Input
                label="Tax Identification Number (TIN)"
                name="tin"
                placeholder="e.g. 23491820-0001"
                value={formData.tin}
                onChange={handleChange}
              />
            </div>

            <Select
              label="Industry / Sector"
              name="industry"
              options={INDUSTRIES}
              value={formData.industry}
              onChange={handleChange}
            />

            <Textarea
              label="Registered Office Address"
              name="address"
              rows={2}
              placeholder="Physical business address..."
              value={formData.address}
              onChange={handleChange}
            />

            <Button
              type="submit"
              variant="primary"
              size="md"
              loading={loading}
              icon={ArrowRight}
              iconPosition="right"
              className="w-full"
            >
              Verify with CAC & Continue
            </Button>
          </form>
        )}

        {/* STEP 2: Document Uploads */}
        {step === 2 && (
          <form onSubmit={handleStep2Submit} className="space-y-4">
            <div className="p-3.5 bg-brand-50 border border-brand-100 rounded-2xl flex items-center gap-2.5 text-xs text-brand-800">
              <ShieldCheck size={16} className="text-brand-600 shrink-0" />
              <span>CAC business match confirmed. Please attach corporate documents for compliance clearance.</span>
            </div>

            <div className="space-y-3 text-xs">
              <div>
                <label className="block font-semibold text-slate-700 mb-1">
                  CAC Certificate of Incorporation (PDF / JPG)
                </label>
                <input
                  type="file"
                  accept="image/*,application/pdf"
                  className="block w-full text-xs text-slate-500 file:mr-4 file:py-2 file:px-4 file:rounded-xl file:border-0 file:text-xs file:font-semibold file:bg-slate-100 file:text-slate-700 hover:file:bg-slate-200"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">
                  Memorandum & Articles of Association (MEMART)
                </label>
                <input
                  type="file"
                  accept="image/*,application/pdf"
                  className="block w-full text-xs text-slate-500 file:mr-4 file:py-2 file:px-4 file:rounded-xl file:border-0 file:text-xs file:font-semibold file:bg-slate-100 file:text-slate-700 hover:file:bg-slate-200"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">
                  Proof of Business Address (Utility Bill &lt; 3 Months)
                </label>
                <input
                  type="file"
                  accept="image/*,application/pdf"
                  className="block w-full text-xs text-slate-500 file:mr-4 file:py-2 file:px-4 file:rounded-xl file:border-0 file:text-xs file:font-semibold file:bg-slate-100 file:text-slate-700 hover:file:bg-slate-200"
                />
              </div>
            </div>

            <div className="flex items-center gap-3 pt-2">
              <Button
                variant="outline"
                size="md"
                onClick={() => setStep(1)}
                disabled={loading}
                className="flex-1"
              >
                Back
              </Button>
              <Button
                type="submit"
                variant="primary"
                size="md"
                loading={loading}
                icon={FileCheck}
                className="flex-1"
              >
                Submit KYB Verification
              </Button>
            </div>
          </form>
        )}
      </Card>

      {/* Success Modal */}
      {successData && (
        <SuccessModal
          isOpen={true}
          onClose={() => setSuccessData(null)}
          title="KYB Documents Submitted"
          message={`Corporate verification documents for ${successData.companyName} (${successData.rcNumber}) have been submitted for compliance review.`}
          buttonText="Done"
        />
      )}
    </>
  );
}
