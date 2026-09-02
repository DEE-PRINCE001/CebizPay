import React from 'react';
import { Loader2 } from 'lucide-react';

/**
 * Reusable Button component matching CebizPay pill-based visual language.
 * Uses design system color tokens from index.css.
 */
export default function Button({
  children,
  variant = 'primary',
  size = 'md',
  loading = false,
  disabled = false,
  icon: Icon = null,
  iconPosition = 'left',
  className = '',
  type = 'button',
  onClick,
  ...props
}) {
  const baseStyles = 'inline-flex items-center justify-center font-medium transition-all duration-150 focus:outline-none focus:ring-2 focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed select-none';

  const variants = {
    primary: 'bg-brand-600 hover:bg-brand-700 active:bg-brand-800 text-white rounded-full shadow-xs shadow-brand-500/20 focus:ring-brand-600',
    secondary: 'bg-brand-50 hover:bg-brand-100 active:bg-brand-200 text-brand-600 rounded-full focus:ring-brand-500',
    outline: 'border border-slate-200 hover:border-slate-300 bg-white hover:bg-slate-50 text-slate-700 rounded-full focus:ring-slate-400',
    danger: 'bg-red-600 hover:bg-red-700 active:bg-red-800 text-white rounded-full shadow-xs shadow-red-500/20 focus:ring-red-500',
    ghost: 'text-slate-600 hover:bg-slate-100 active:bg-slate-200 rounded-lg focus:ring-slate-300'
  };

  const sizes = {
    sm: 'text-xs px-3.5 py-1.5 gap-1.5',
    md: 'text-sm px-5 py-2 gap-2',
    lg: 'text-base px-6 py-2.5 gap-2.5'
  };

  const iconSizes = {
    sm: 14,
    md: 16,
    lg: 18
  };

  return (
    <button
      type={type}
      disabled={disabled || loading}
      onClick={onClick}
      className={`${baseStyles} ${variants[variant] || variants.primary} ${sizes[size] || sizes.md} ${className}`}
      {...props}
    >
      {loading ? (
        <Loader2 size={iconSizes[size] || 16} className="animate-spin" />
      ) : (
        Icon && iconPosition === 'left' && <Icon size={iconSizes[size] || 16} />
      )}
      <span>{children}</span>
      {!loading && Icon && iconPosition === 'right' && <Icon size={iconSizes[size] || 16} />}
    </button>
  );
}
