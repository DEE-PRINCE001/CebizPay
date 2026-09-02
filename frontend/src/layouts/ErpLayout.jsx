import React from 'react';
import CustomerLayout from './CustomerLayout';
import ErpNav from '../components/navigation/ErpNav';

/**
 * Specialized ERP / Catalog Operations Shell Layout.
 * Matches Invoice generator.png (D251) and catalog views.
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
