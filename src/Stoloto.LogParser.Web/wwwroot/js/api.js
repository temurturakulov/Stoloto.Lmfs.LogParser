export async function getLogs(params) {
  const q = new URLSearchParams();
  if (params.path)        q.set('path', params.path);
  if (params.isFile)      q.set('isFile', 'true');
  if (params.dateFrom)    q.set('dateFrom', params.dateFrom);
  if (params.dateTo)      q.set('dateTo', params.dateTo);
  if (params.levels?.length)      q.set('levels', params.levels.join(','));
  if (params.categories?.length)  q.set('categories', params.categories.join(','));
  if (params.type)        q.set('type', params.type);
  if (params.uid)         q.set('uid', params.uid);
  if (params.urlContains) q.set('urlContains', params.urlContains);
  if (params.search)      q.set('search', params.search);
  q.set('page',     params.page     ?? 1);
  q.set('pageSize', params.pageSize ?? 100);

  const res = await fetch(`/api/logs?${q}`);
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: res.statusText }));
    throw new Error(err.error ?? res.statusText);
  }
  return res.json();
}

export async function getTrace(uid, path, isFile, date) {
  const q = new URLSearchParams({ path, uid });
  if (isFile) q.set('isFile', 'true');
  if (date)   q.set('date', date);
  const res = await fetch(`/api/logs/trace/${uid}?${q}`);
  if (!res.ok) throw new Error(res.statusText);
  return res.json();
}

export function connectLive(params, onData, onError) {
  const q = new URLSearchParams();
  q.set('path', params.path);
  if (params.isFile)     q.set('isFile', 'true');
  if (params.levels?.length)     q.set('levels', params.levels.join(','));
  if (params.categories?.length) q.set('categories', params.categories.join(','));

  const es = new EventSource(`/api/logs/live?${q}`);
  es.onmessage = e => { try { onData(JSON.parse(e.data)); } catch {} };
  es.onerror   = onError ?? (() => {});
  return es;
}

export async function browse(isFile = false) {
  const res = await fetch(`/api/browse?isFile=${isFile}`);
  if (!res.ok) throw new Error(res.statusText);
  return res.json(); // { path: string | null }
}

export async function getSettings() {
  const res = await fetch('/api/settings');
  return res.json();
}

export async function saveSettings(settings) {
  const res = await fetch('/api/settings', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(settings)
  });
  return res.json();
}
