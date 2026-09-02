// Currency formatting with proper symbols and precision
export function formatCurrency(amount, currency = 'NGN', hideFraction = false) {
  if (amount === undefined || amount === null || isNaN(Number(amount))) {
    amount = 0;
  }
  const numeric = Number(amount);
  
  let prefix = '₦';
  if (currency === 'USD') prefix = '$';
  else if (currency === 'EUR') prefix = '€';
  else if (currency === 'GBP') prefix = '£';
  else if (currency === 'GHS') prefix = 'GH₵';
  else if (currency === 'USDT') prefix = 'USDT ';
  else if (currency === 'INTERNATIONAL_NGN') prefix = 'Int. ₦';

  const formatted = new Intl.NumberFormat('en-NG', {
    minimumFractionDigits: hideFraction ? 0 : 2,
    maximumFractionDigits: 2,
  }).format(numeric);

  return `${prefix}${formatted}`;
}

// Format date to human readable string
export function formatDate(dateString, includeTime = false) {
  if (!dateString) return '—';
  try {
    const d = new Date(dateString);
    if (isNaN(d.getTime())) return dateString;
    
    const options = {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      ...(includeTime ? { hour: '2-digit', minute: '2-digit' } : {})
    };
    return new Intl.DateTimeFormat('en-GB', options).format(d);
  } catch {
    return dateString;
  }
}

// Relative time formatting
export function formatRelativeTime(dateString) {
  if (!dateString) return '—';
  try {
    const d = new Date(dateString);
    const now = new Date();
    const diffInSeconds = Math.floor((now - d) / 1000);

    if (diffInSeconds < 60) return 'Just now';
    if (diffInSeconds < 3600) return `${Math.floor(diffInSeconds / 60)}m ago`;
    if (diffInSeconds < 86400) return `${Math.floor(diffInSeconds / 3600)}h ago`;
    if (diffInSeconds < 2592000) return `${Math.floor(diffInSeconds / 86400)}d ago`;
    return formatDate(dateString);
  } catch {
    return dateString;
  }
}

// Format percentage rate
export function formatPercent(rate) {
  if (rate === undefined || rate === null) return '0%';
  const val = Number(rate);
  return `${(val * 100).toFixed(2).replace(/\.00$/, '')}%`;
}

// Mask sensitive identifiers (account number, card, phone)
export function maskIdentifier(str, visibleEnd = 4) {
  if (!str) return '••••';
  const clean = String(str);
  if (clean.length <= visibleEnd) return clean;
  return '•••• ' + clean.slice(-visibleEnd);
}

// Truncate long UUIDs or text
export function truncate(str, maxLen = 16) {
  if (!str) return '';
  if (str.length <= maxLen) return str;
  return `${str.slice(0, 8)}...${str.slice(-6)}`;
}
