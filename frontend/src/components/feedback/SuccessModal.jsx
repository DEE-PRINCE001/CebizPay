import React from 'react';
import { CheckCircle2 } from 'lucide-react';
import Modal from '../common/Modal';
import Button from '../common/Button';

/**
 * Action completion modal.
 */
export default function SuccessModal({
  isOpen,
  onClose,
  title = 'Successful',
  message = 'Your operation has been processed successfully.',
  buttonText = 'Done',
  onAction
}) {
  const handleAction = () => {
    if (onAction) {
      onAction();
    } else {
      onClose();
    }
  };

  return (
    <Modal isOpen={isOpen} onClose={onClose} maxWidth="max-w-sm" showClose={true}>
      <div className="flex flex-col items-center text-center p-2">
        <div className="w-16 h-16 rounded-full bg-status-success-bg text-status-success flex items-center justify-center mb-4 ring-8 ring-status-success-bg/50">
          <CheckCircle2 size={32} />
        </div>
        <h3 className="text-lg font-bold text-slate-900 mb-1">{title}</h3>
        <p className="text-xs text-slate-500 leading-relaxed mb-6">{message}</p>
        <Button
          variant="primary"
          size="md"
          onClick={handleAction}
          className="w-full"
        >
          {buttonText}
        </Button>
      </div>
    </Modal>
  );
}
