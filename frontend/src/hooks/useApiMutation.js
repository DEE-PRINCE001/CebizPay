import { useState, useCallback, useRef } from 'react';
import { parseProblemDetails } from '../utils/problemDetails';

/**
 * Declarative Hook for API Mutations (POST, PUT, PATCH, DELETE).
 * 
 * @param {Function} mutationFn - Async mutation function (e.g. (data) => apiClient.post('/wallet/transfer/bank', data))
 * @param {Object} [options={}] - Mutation options
 * @param {Function} [options.onSuccess] - Callback upon successful mutation
 * @param {Function} [options.onError] - Callback upon mutation failure
 * @param {string} [options.successMessage] - Optional toast message to trigger on success
 * @returns {{ mutate: Function, mutateAsync: Function, loading: boolean, error: any, problemDetails: import('../utils/problemDetails').NormalizedError|null, reset: Function }}
 */
export function useApiMutation(mutationFn, options = {}) {
  const {
    onSuccess,
    onError
  } = options;

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [problemDetails, setProblemDetails] = useState(null);
  const [data, setData] = useState(null);

  const mutationFnRef = useRef(mutationFn);
  mutationFnRef.current = mutationFn;

  const onSuccessRef = useRef(onSuccess);
  onSuccessRef.current = onSuccess;

  const onErrorRef = useRef(onError);
  onErrorRef.current = onError;

  const reset = useCallback(() => {
    setLoading(false);
    setError(null);
    setProblemDetails(null);
    setData(null);
  }, []);

  const mutateAsync = useCallback(async (variables) => {
    setLoading(true);
    setError(null);
    setProblemDetails(null);

    try {
      const result = await mutationFnRef.current(variables);
      setData(result);
      setLoading(false);

      if (onSuccessRef.current) {
        onSuccessRef.current(result, variables);
      }

      return result;
    } catch (err) {
      const parsed = err.problemDetails || parseProblemDetails(err);
      setError(err);
      setProblemDetails(parsed);
      setLoading(false);

      if (onErrorRef.current) {
        onErrorRef.current(parsed, err, variables);
      }

      throw err;
    }
  }, []);

  const mutate = useCallback((variables) => {
    mutateAsync(variables).catch(() => {
      // Handled in mutateAsync; avoids unhandled promise rejection in fire-and-forget calls
    });
  }, [mutateAsync]);

  return {
    mutate,
    mutateAsync,
    loading,
    error,
    problemDetails,
    data,
    reset
  };
}

export default useApiMutation;
