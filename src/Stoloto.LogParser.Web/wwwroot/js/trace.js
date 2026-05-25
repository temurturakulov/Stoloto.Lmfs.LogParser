import { getTrace } from './api.js';

const CAT_COLORS = {
  api: '#0d6efd', kkt: '#fd7e14', db: '#6c757d', auth: '#198754',
  pos: '#6f42c1', queue: '#0dcaf0', schedule: '#d63384', file: '#795548'
};

function esc(s) {
  return String(s ?? '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}

async function init() {
  const p = new URLSearchParams(location.search);
  const uid    = p.get('uid');
  const path   = p.get('path');
  const isFile = p.get('isFile') === 'true';
  const date   = p.get('date');

  if (!uid || !path) {
    document.getElementById('trace-title').textContent = 'Нет параметров';
    return;
  }

  document.getElementById('trace-title').textContent = `Trace: ${uid}`;

  let result;
  try {
    result = await getTrace(uid, path, isFile, date);
  } catch (e) {
    document.getElementById('summary').textContent = 'Ошибка: ' + e.message;
    return;
  }

  const { entries, totalDurationMs } = result;
  if (!entries.length) {
    document.getElementById('summary').textContent = 'Записей не найдено';
    return;
  }

  const first = new Date(entries[0].datetime).getTime();
  const last  = new Date(entries[entries.length - 1].datetime).getTime();
  const span  = last - first || 1;

  document.getElementById('summary').innerHTML =
    `Шагов: <strong>${entries.length}</strong> &nbsp; Общее время: <strong>${totalDurationMs.toFixed(0)} ms</strong>`;

  const timeline = document.getElementById('timeline');
  timeline.innerHTML = '';

  entries.forEach((entry, i) => {
    const start = new Date(entry.datetime).getTime() - first;
    const dur   = entry.responseTime != null ? entry.responseTime * 1000 : 0;
    const left  = (start / span) * 100;
    const width = Math.max((dur / span) * 100, 0.5);
    const color = CAT_COLORS[entry.category] ?? '#adb5bd';
    const label = entry.category ?? entry.logger?.split('.').pop() ?? '?';
    const durLabel = dur > 0 ? `${dur.toFixed(0)}ms` : '';

    const row = document.createElement('div');
    row.className = 'bar-row';
    row.innerHTML = `
      <div class="bar-label text-muted">${esc(label)}</div>
      <div class="bar-track" title="${esc(entry.url ?? entry.message ?? '')}">
        <div class="bar-fill" style="left:${left}%;width:${Math.min(width, 100 - left)}%;background:${color}"></div>
      </div>
      <div class="bar-dur">${durLabel}</div>`;

    row.querySelector('.bar-track').addEventListener('click', () => showDetail(entry, i));
    timeline.appendChild(row);
  });
}

function showDetail(entry, i) {
  const detail = document.getElementById('detail');
  const fields = [
    ['datetime', entry.datetime ? new Date(entry.datetime).toLocaleString('ru') : ''],
    ['level',    entry.level],
    ['category', entry.category],
    ['type',     entry.type],
    ['url',      entry.url],
    ['uid',      entry.uid],
    ['message',  entry.message],
    ['body',     entry.body],
    ['responseTime', entry.responseTime != null ? entry.responseTime + 's' : null],
    ['httpCode', entry.httpCode],
    ['logger',   entry.logger],
    ...Object.entries(entry.extra ?? {})
  ].filter(([, v]) => v != null && v !== '');

  const rows = fields.map(([k, v]) =>
    `<tr><td class="text-muted" style="width:130px">${esc(k)}</td><td style="word-break:break-all">${esc(String(v))}</td></tr>`
  ).join('');

  detail.innerHTML = `<div class="detail-card"><table class="table table-sm mb-0">${rows}</table></div>`;
}

init();
