import { getLogs, connectLive, getSettings, saveSettings, browse } from './api.js';

let state = {
  path: '', isFile: false, page: 1, pageSize: 100, sortAsc: true,
  skippedLines: [], settings: null,
  liveEs: null, livePaused: false, missedCount: 0
};

const $ = id => document.getElementById(id);

// ── Init ────────────────────────────────────────────────────────────────────
async function init() {
  state.settings = await getSettings();
  renderColumns();
  renderRecentPaths();
  if (state.settings.lastLogPath) {
    state.path   = state.settings.lastLogPath;
    state.isFile = state.settings.lastPathIsFile;
    $('path-label').textContent = shortPath(state.path);
  }
  const today = new Date().toISOString().slice(0, 10);
  $('date-from').value = today;
  $('date-to').value   = today;
  bindEvents();
}

// ── Columns ──────────────────────────────────────────────────────────────────
function renderColumns() {
  const list = $('column-list');
  list.innerHTML = '';
  const cols = state.settings.columns.sort((a, b) => a.order - b.order);
  cols.forEach(col => {
    const d = document.createElement('div');
    d.className = 'form-check';
    d.innerHTML = `<input class="form-check-input" type="checkbox" id="col-${col.name}" ${col.visible ? 'checked' : ''}>
      <label class="form-check-label small" for="col-${col.name}">${col.name}</label>`;
    d.querySelector('input').addEventListener('change', async e => {
      col.visible = e.target.checked;
      await saveSettings(state.settings);
      if (state.lastResult) renderTable(state.lastResult);
    });
    list.appendChild(d);
  });
}

function visibleCols() {
  return state.settings.columns
    .filter(c => c.visible)
    .sort((a, b) => a.order - b.order)
    .map(c => c.name);
}

// ── Table ────────────────────────────────────────────────────────────────────
function renderTable(result) {
  state.lastResult = result;
  const cols = visibleCols();

  const thead = $('thead-row');
  thead.innerHTML = cols.map(c => `<th class="text-nowrap">${c}</th>`).join('');

  const tbody = $('tbody');
  tbody.innerHTML = '';
  result.items.forEach((entry, i) => {
    const tr = document.createElement('tr');
    tr.className = 'log-row';
    tr.dataset.idx = i;
    tr.innerHTML = cols.map(c => {
      const val = cellValue(entry, c);
      let cls = '';
      if (c === 'level') cls = `level-${entry.level}`;
      if (c === 'category') cls = `cat-${entry.category}`;
      return `<td class="${cls} text-nowrap" style="max-width:300px;overflow:hidden;text-overflow:ellipsis" title="${esc(val)}">${esc(val)}</td>`;
    }).join('');
    tr.addEventListener('click', () => toggleDetail(tr, entry, result.items));
    tbody.appendChild(tr);
  });

  $('total-label').textContent = `Записей: ${result.total}`;
  $('page-label').textContent  = `Стр. ${result.page}`;
  $('prev-btn').disabled = result.page <= 1;
  $('next-btn').disabled = result.page * result.pageSize >= result.total;

  const sk = result.skippedLines ?? [];
  state.skippedLines = sk;
  const btn = $('skipped-btn');
  if (sk.length > 0) {
    btn.classList.remove('d-none');
    $('skipped-count').textContent = sk.length;
  } else {
    btn.classList.add('d-none');
  }
}

function cellValue(entry, col) {
  if (col === 'datetime') return entry.datetime ? new Date(entry.datetime).toLocaleString('ru') : '';
  return entry[col] ?? entry.extra?.[col] ?? '';
}

function toggleDetail(tr, entry, all) {
  const existing = tr.nextElementSibling;
  if (existing?.classList.contains('detail-row')) { existing.remove(); return; }

  const detail = document.createElement('tr');
  detail.className = 'detail-row';
  const fields = [
    ['datetime', new Date(entry.datetime).toLocaleString('ru')],
    ['level', entry.level],
    ['category', entry.category],
    ['type', entry.type],
    ['url', entry.url],
    ['uid', entry.uid],
    ['message', entry.message],
    ['body', entry.body],
    ['responseTime', entry.responseTime != null ? entry.responseTime + 's' : ''],
    ['httpCode', entry.httpCode],
    ['logger', entry.logger],
    ['sourceFile', entry.sourceFile],
    ...Object.entries(entry.extra ?? {})
  ].filter(([, v]) => v != null && v !== '');

  const rows = fields.map(([k, v]) => {
    const traceLink = k === 'uid' && v
      ? ` <a href="/trace.html?uid=${encodeURIComponent(v)}&path=${encodeURIComponent(state.path)}&isFile=${state.isFile}&date=${entry.datetime?.slice(0,10) ?? ''}" target="_blank">[→ trace]</a>`
      : '';
    return `<tr><td class="text-muted" style="width:130px">${esc(k)}</td><td style="word-break:break-all">${esc(String(v))}${traceLink}</td></tr>`;
  }).join('');

  detail.innerHTML = `<td colspan="99"><div class="detail-card"><table class="table table-sm mb-0">${rows}</table></div></td>`;
  tr.after(detail);
}

// ── Load ─────────────────────────────────────────────────────────────────────
async function loadLogs() {
  if (!state.path) { alert('Укажите путь к папке или файлу'); return; }

  const params = buildQuery();
  try {
    const result = await getLogs(params);
    renderTable(result);
    await saveSettings({ ...state.settings, lastLogPath: state.path, lastPathIsFile: state.isFile });
  } catch (e) {
    alert('Ошибка: ' + e.message);
  }
}

function buildQuery() {
  const levels     = Array.from($('level-select').selectedOptions).map(o => o.value);
  const categories = Array.from($('category-select').selectedOptions).map(o => o.value);
  return {
    path: state.path, isFile: state.isFile,
    dateFrom: $('date-from').value, dateTo: $('date-to').value,
    levels, categories,
    type: $('type-select').value,
    uid: $('uid-input').value,
    urlContains: $('url-input').value,
    search: $('search-input').value,
    page: state.page, pageSize: Number($('page-size-select').value),
    sortAsc: state.sortAsc
  };
}

// ── Live ─────────────────────────────────────────────────────────────────────
function startLive() {
  if (!state.path) { $('live-toggle').checked = false; return; }
  state.livePaused = false;
  state.missedCount = 0;
  $('live-dot').classList.remove('d-none');
  $('pause-btn').classList.remove('d-none');
  $('live-controls').style.removeProperty('display');

  const params = buildQuery();
  state.liveEs = connectLive(params, data => {
    if (state.livePaused) { state.missedCount += data.entries?.length ?? 0; updateMissed(); return; }
    prependRows(data.entries ?? []);
  }, () => setTimeout(startLive, 3000));
}

function stopLive() {
  state.liveEs?.close();
  state.liveEs = null;
  $('live-dot').classList.add('d-none');
  $('pause-btn').classList.add('d-none');
  $('missed-badge').classList.add('d-none');
}

function prependRows(entries) {
  const cols = visibleCols();
  const tbody = $('tbody');
  entries.forEach(entry => {
    const tr = document.createElement('tr');
    tr.className = 'log-row table-warning';
    tr.innerHTML = cols.map(c => `<td class="text-nowrap">${esc(cellValue(entry, c))}</td>`).join('');
    tr.addEventListener('click', () => toggleDetail(tr, entry, entries));
    tbody.prepend(tr);
  });
}

function updateMissed() {
  const b = $('missed-badge');
  if (state.missedCount > 0) { b.textContent = `Пропущено: ${state.missedCount}`; b.classList.remove('d-none'); }
  else b.classList.add('d-none');
}

// ── Recent paths ──────────────────────────────────────────────────────────────
function renderRecentPaths() {
  const list = $('recent-list');
  list.innerHTML = '';
  (state.settings.recentPaths ?? []).forEach(p => {
    const a = document.createElement('a');
    a.className = 'list-group-item list-group-item-action small py-1';
    a.textContent = p;
    a.href = '#';
    a.addEventListener('click', e => { e.preventDefault(); $('path-input').value = p; });
    list.appendChild(a);
  });
}

// ── Skipped modal ─────────────────────────────────────────────────────────────
function showSkipped() {
  $('skipped-modal-count').textContent = state.skippedLines.length;
  const list = $('skipped-list');
  list.innerHTML = '';
  state.skippedLines.forEach(sl => {
    const a = document.createElement('a');
    a.className = 'list-group-item list-group-item-action small py-1 font-monospace';
    a.textContent = `#${sl.lineNumber}  ${sl.rawText.slice(0, 80)}`;
    a.href = '#';
    a.addEventListener('click', e => {
      e.preventDefault();
      $('skipped-detail').textContent = sl.rawText;
    });
    list.appendChild(a);
  });
  new bootstrap.Modal($('skipped-modal')).show();
}

// ── Helpers ──────────────────────────────────────────────────────────────────
function esc(s) {
  return String(s ?? '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

function shortPath(p) { return p.length > 40 ? '...' + p.slice(-38) : p; }

// ── Events ───────────────────────────────────────────────────────────────────
function bindEvents() {
  $('load-btn').addEventListener('click', () => { state.page = 1; loadLogs(); });
  $('reset-btn').addEventListener('click', () => {
    $('level-select').selectedIndex = -1;
    $('category-select').selectedIndex = -1;
    $('type-select').value = '';
    $('uid-input').value = '';
    $('url-input').value = '';
    $('search-input').value = '';
  });
  $('prev-btn').addEventListener('click', () => { state.page--; loadLogs(); });
  $('next-btn').addEventListener('click', () => { state.page++; loadLogs(); });
  $('page-size-select').addEventListener('change', () => { state.page = 1; loadLogs(); });

  $('sort-btn').addEventListener('click', () => {
    state.sortAsc = !state.sortAsc;
    $('sort-btn').textContent = state.sortAsc ? '↑ Время' : '↓ Время';
    state.page = 1;
    if (state.path) loadLogs();
  });

  $('live-toggle').addEventListener('change', e => e.target.checked ? startLive() : stopLive());
  $('pause-btn').addEventListener('click', () => {
    state.livePaused = !state.livePaused;
    $('pause-btn').textContent = state.livePaused ? '▶ Продолжить' : '⏸ Пауза';
    if (!state.livePaused) { state.missedCount = 0; updateMissed(); }
  });

  $('path-btn').addEventListener('click', () => {
    renderRecentPaths();
    new bootstrap.Modal($('source-modal')).show();
  });

  $('browse-btn').addEventListener('click', async () => {
    const isFile = document.querySelector('input[name="path-mode"]:checked').value === 'file';
    try {
      const { path } = await browse(isFile);
      if (path) $('path-input').value = path;
    } catch {}
  });

  $('apply-path-btn').addEventListener('click', async () => {
    const p = $('path-input').value.trim();
    if (!p) return;
    state.path   = p;
    state.isFile = document.querySelector('input[name="path-mode"]:checked').value === 'file';
    $('path-label').textContent = shortPath(p);
    bootstrap.Modal.getInstance($('source-modal')).hide();
    state.settings.recentPaths = [p, ...(state.settings.recentPaths ?? []).filter(x => x !== p)].slice(0, 10);
    state.settings.lastLogPath   = p;
    state.settings.lastPathIsFile = state.isFile;
    await saveSettings(state.settings);
    loadLogs();
  });

  $('skipped-btn').addEventListener('click', showSkipped);
}

init();
