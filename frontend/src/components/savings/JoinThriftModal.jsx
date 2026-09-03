import React, { useState } from 'react';
import Modal from '../common/Modal';
import Input from '../forms/Input';
import Button from '../common/Button';
import Alert from '../feedback/Alert';
import { KeyRound, Users } from 'lucide-react';
import apiClient from '../../services/api/client';
import { useToast } from '../../hooks/useToast';
import { parseProblemDetails } from '../../utils/problemDetails';

/**
 * Modal to join an existing Thrift (Ajo) circle via invitation code.
 */
export default function JoinThriftModal({
  isOpen,
  onClose,
  onSuccess
}) {
  const { showSuccess } = useToast();
  const [invitationCode, setInvitationCode] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!invitationCode.trim()) {
      setError('Please enter a valid invitation code.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      await apiClient.post('/work/thrift/join', {
        invitationCode: invitationCode.trim()
      });

      showSuccess('Joined thrift circle successfully.');
      setInvitationCode('');
      onClose();
      if (onSuccess) onSuccess();
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(parsed.message || 'Invalid or expired invitation code.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Join Thrift Circle"
      subtitle="Enter your circle invitation code to participate"
      maxWidth="max-w-md"
    >
      <form onSubmit={handleSubmit} className="space-y-4 pt-1">
        {error && (
          <Alert variant="danger" onClose={() => setError(null)}>
            {error}
          </Alert>
        )}

        <Input
          label="Thrift Invitation Code"
          placeholder="e.g. AJO-7X9K2P"
          value={invitationCode}
          onChange={(e) => {
            setInvitationCode(e.target.value.toUpperCase());
            if (error) setError(null);
          }}
          icon={KeyRound}
          required
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
            icon={Users}
            className="flex-1"
          >
            Join Circle
          </Button>
        </div>
      </form>
    </Modal>
  );
}
