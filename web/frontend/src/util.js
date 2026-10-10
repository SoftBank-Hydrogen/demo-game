import { useCallback, useEffect, useState } from 'react';

// Load data when the page opens (and when `deps` change). Returns { data, error, loading, reload, setData }.
export function useLoad(load, deps) {
  const [state, setState] = useState({ data: null, error: null, loading: true });
  const run = useCallback(load, deps);   // a new `load` only when deps change

  const reload = useCallback(() => {
    let active = true;
    setState(s => ({ ...s, loading: true }));
    run().then(data => active && setState({ data, error: null, loading: false }))
      .catch(error => active && setState({ data: null, error, loading: false }));
    return () => { active = false; };
  }, [run]);

  useEffect(reload, [reload]);
  const setData = data => setState(s => ({ ...s, data: typeof data === 'function' ? data(s.data) : data }));
  return { ...state, reload, setData };
}

export function formatDate(iso) {
  if (!iso) return '';
  return new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });
}

export const STATUSES = [
  { id: 'todo', label: 'To do' },
  { id: 'doing', label: 'In progress' },
  { id: 'done', label: 'Done' },
];
