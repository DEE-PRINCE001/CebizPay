import { useState, useEffect, useCallback, useRef } from 'react';
import { parseProblemDetails } from '../utils/problemDetails';

/**
 * Declarative Hook for API Data Fetching.
 * 
 * @param {Function} queryFn - Async function returning data (e.g. () => apiClient.get('/wallet/external-accounts'))
 * @param {Object} [options={}] - Query configuration options
 * @param {boolean} [options.enabled=true] - Whether the query should execute automatically
 * @param {any} [options.initialData=null] - Initial fallback data before resolution
 * @param {Array} [options.deps=[]] - Dependency array to trigger automatic refetches
 * @param {Function} [options.onSuccess] - Callback on successful resolution
 * @param {Function} [options.onError] - Callback on resolution error
 * @returns {{ data: any, loading: boolean, error: any, problemDetails: import('../utils/problemDetails').NormalizedError|null, refetch: Function, setData: Function }}
 */
export function useApiQuery(queryFn, options = {}) {
  const {
    enabled = true,
    initialData = null,
    deps = [],
    onSuccess,
    onError
  } = options;

  const [data, setData] = useState(initialData);
  const [loading, setLoading] = useState(enabled);
  const [error, setError] = useState(null);
  const [problemDetails, setProblemDetails] = useState(null);

  const isMountedRef = useRef(true);
  const queryFnRef = useRef(queryFn);
  queryFnRef.current = queryFn;

  const onSuccessRef = useRef(onSuccess);
  onSuccessRef.current = onSuccess;

  const onErrorRef = useRef(onError);
  onErrorRef.current = onError;

  useEffect(() => {
    isMountedRef.current = true;
    return () => {
      isMountedRef.current = false;
    };
  }, []);

  const execute = useCallback(async () => {
    if (!isMountedRef.current) return;

    setLoading(true);
    setError(null);
    setProblemDetails(null);

    try {
      const result = await queryFnRef.current();
      if (isMountedRef.current) {
        setData(result);
        setLoading(false);
        if (onSuccessRef.current) {
          onSuccessRef.current(result);
        }
      }
      return result;
    } catch (err) {
      if (isMountedRef.current) {
        const parsed = err.problemDetails || parseProblemDetails(err);
        setError(err);
        setProblemDetails(parsed);
        setLoading(false);
        if (onErrorRef.current) {
          onErrorRef.current(parsed, err);
        }
      }
      throw err;
    }
  }, []);

  useEffect(() => {
    if (enabled) {
      execute().catch(() => {
        // Handled in catch block inside execute
      });
    } else {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled, ...deps]);

  return {
    data,
    loading,
    error,
    problemDetails,
    refetch: execute,
    setData
  };
}

export default useApiQuery;
