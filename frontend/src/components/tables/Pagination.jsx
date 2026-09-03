import React from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import Button from '../common/Button';

/**
 * Data table pagination controls.
 */
export default function Pagination({
  currentPage = 1,
  totalPages = 1,
  onPageChange,
  hasNextPage = false,
  hasPrevPage = false,
  className = ''
}) {
  const isFirst = currentPage <= 1;
  const isLast = currentPage >= totalPages;

  return (
    <div className={`flex items-center justify-between pt-4 select-none ${className}`}>
      {/* Left: Next button */}
      <div>
        <Button
          variant="outline"
          size="sm"
          disabled={isLast && !hasNextPage}
          onClick={() => onPageChange && onPageChange(currentPage + 1)}
        >
          Next
        </Button>
      </div>

      {/* Right: Page controls */}
      <div className="flex items-center gap-2 text-xs text-slate-500">
        <button
          type="button"
          disabled={isFirst && !hasPrevPage}
          onClick={() => onPageChange && onPageChange(currentPage - 1)}
          className="p-1.5 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
          aria-label="Previous Page"
        >
          <ChevronLeft size={14} />
        </button>

        <div className="min-w-8 h-8 flex items-center justify-center rounded-lg border border-slate-200 bg-white font-semibold text-slate-900 px-2">
          {currentPage}
        </div>

        <button
          type="button"
          disabled={isLast && !hasNextPage}
          onClick={() => onPageChange && onPageChange(currentPage + 1)}
          className="p-1.5 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
          aria-label="Next Page"
        >
          <ChevronRight size={14} />
        </button>

        {totalPages > 1 && (
          <span className="ml-1 text-slate-400">
            of <span className="font-medium text-slate-700">{totalPages}</span>
          </span>
        )}
      </div>
    </div>
  );
}
