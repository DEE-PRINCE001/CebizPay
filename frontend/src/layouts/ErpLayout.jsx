import React from 'react';
import CustomerLayout from './CustomerLayout';
import ErpNav from '../components/navigation/ErpNav';

/**
 * ERP module shell layout.
 */
export default function ErpLayout({
  children,
  title,
  subtitle,
  headerAction,
  className = ''
}) {
  return (
    <CustomerLayout
      title={title}
      subtitle={subtitle}
      headerAction={headerAction}
    >
      <ErpNav />
      <div className={className}>
        {children}
      </div>
    </CustomerLayout>
  );
}
