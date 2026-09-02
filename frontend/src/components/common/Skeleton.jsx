import React from 'react';

/**
 * Shimmer Loading Placeholder for cards, text, and tables.
 */
export default function Skeleton({
  variant = 'text',
  width,
  height,
  className = '',
  count = 1
}) {
  const getStyles = () => {
    switch (variant) {
      case 'circle':
        return 'rounded-full w-10 h-10';
      case 'card':
        return 'rounded-2xl h-32 w-full';
      case 'table-row':
        return 'rounded-lg h-12 w-full';
      case 'button':
        return 'rounded-full h-10 w-28';
      case 'text':
      default:
        return 'rounded-md h-4 w-full';
    }
  };

  const inlineStyles = {
    ...(width && { width }),
    ...(height && { height })
  };

  if (count > 1) {
    return (
      <div className="space-y-2.5 w-full">
        {Array.from({ length: count }).map((_, i) => (
          <div
            key={i}
            style={inlineStyles}
            className={`animate-pulse bg-slate-200/80 ${getStyles()} ${className}`}
          />
        ))}
      </div>
    );
  }

  return (
    <div
      style={inlineStyles}
      className={`animate-pulse bg-slate-200/80 ${getStyles()} ${className}`}
    />
  );
}
