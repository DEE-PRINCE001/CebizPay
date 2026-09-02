import React from 'react';

/**
 * Standard Data Table Wrapper.
 */
export default function Table({
  children,
  className = '',
  responsive = true
}) {
  const content = (
    <table className={`w-full text-left border-collapse ${className}`}>
      {children}
    </table>
  );

  if (responsive) {
    return (
      <div className="w-full overflow-x-auto">
        {content}
      </div>
    );
  }

  return content;
}
