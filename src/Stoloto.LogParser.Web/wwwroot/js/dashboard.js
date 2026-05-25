import { getStats, getLogs, getSettings } from './api.js';

const LEVEL_COLORS = {
  Info:  '#0d6efd',
  Warn:  '#fd7e14',
  Error: '#dc3545',
  Debug: '#6c757d',
};

const $ = id => document.getElementById(id);
let chartInstance = null;
let appSettings   = null;
let state = { path: '', isFile: false };

let modalState = {
  level: '', page: 1, pageSize: 100,
  total: 0, dateFrom: '', dateTo: ''
};

function esc(s) {
  return String(s ?? '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}

async function init() {
  appSettings = await getSettings();
  if (appSettings.lastLogPath) {
    state.path   = appSettings.lastLogPath;
    state.isFile = appSettings.lastPathIsFile;
    $('path-label').textContent = state.path;
    load();
  } else {
    $('no-path').style.display = '';
  }

  $('modal-prev-btn').addEventListener('click', () => { modalState.page--; loadModalPage(); });
  $('modal-next-btn').addEventListener('click', () => { modalState.page++; loadModalPage(); });
  $('modal-page-size').addEventListener('change', () => { modalState.page = 1; loadModalPage(); });
}

// ── Stats ────────────────────────────────────────────────────────────────────

async function load() {
  if (!state.path) { $('no-path').style.display = ''; return; }
  $('no-path').style.display = 'none';

  let stats;
  try {
    stats = await getStats({ path: state.path, isFile: state.isFile });
  } catch (e) { alert('Ошибка: ' + e.message); return; }

  renderCards(stats.byLevel);
  renderChart(stats.byHour, stats.byLevel);
}

function renderCards(byLevel) {
  const container = $('level-cards');
  container.innerHTML = '';
  const priority = ['Error', 'Warn', 'Info', 'Debug'];
  const all = [...priority, ...Object.keys(byLevel).filter(l => !priority.includes(l))];

  all.forEach(level => {
    const count = byLevel[level] ?? 0;
    const color = LEVEL_COLORS[level] ?? '#adb5bd';
    const col = document.createElement('div');
    col.className = 'col-6 col-md-3';
    col.innerHTML = `
      <div class="card level-card level-${level} p-3" role="button">
        <div class="text-muted small">${esc(level)}</div>
        <div class="count-num" style="color:${color}">${count.toLocaleString('ru')}</div>
        <div class="text-muted small mt-1">записей</div>
      </div>`;
    col.querySelector('.card').addEventListener('click', () => openEntries(level));
    container.appendChild(col);
  });
}

function renderChart(byHour, byLevel) {
  $('chart-card').style.display = '';
  const levels   = Object.keys(byLevel).filter(l => byLevel[l] > 0);
  const labels   = byHour.map(h => `${String(h.hour).padStart(2,'0')}:00`);
  const datasets = levels.map(level => ({
    label: level,
    data: byHour.map(h => h[level] ?? 0),
    backgroundColor: LEVEL_COLORS[level] ?? '#adb5bd',
  }));

  if (chartInstance) chartInstance.destroy();
  chartInstance = new Chart($('hourChart'), {
    type: 'bar',
    data: { labels, datasets },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: { legend: { position: 'top' } },
      scales: { x: { stacked: true }, y: { stacked: true, beginAtZero: true } }
    }
  });
}

// ── Entries modal ─────────────────────────────────────────────────────────────

function visibleCols() {
  if (!appSettings?.columns) return ['datetime','level','category','url','message'];
  return appSettings.columns
    .filter(c => c.visible)
    .sort((a, b) => a.order - b.order)
    .map(c => c.name);
}

async function openEntries(level) {
  modalState.level    = level;
  modalState.page     = 1;
  modalState.dateFrom = '';
  modalState.dateTo   = '';
  modalState.pageSize = Number($('modal-page-size').value) || 100;

  $('entries-modal-title').textContent = `${level}`;
  new bootstrap.Modal($('entries-modal')).show();
  await loadModalPage();
}

async function loadModalPage() {
  modalState.pageSize = Number($('modal-page-size').value) || 100;
  $('entries-tbody').innerHTML = '<tr><td colspan="99" class="text-center text-muted p-3">Загрузка...</td></tr>';

  try {
    const result = await getLogs({
      path: state.path, isFile: state.isFile,
      dateFrom: modalState.dateFrom, dateTo: modalState.dateTo,
      levels: [modalState.level],
      page: modalState.page, pageSize: modalState.pageSize,
      sortAsc: false
    });

    modalState.total = result.total;
    const cols = visibleCols();

    // update header
    $('entries-thead-row').innerHTML = cols.map(c => `<th class="text-nowrap">${esc(c)}</th>`).join('');

    // update pagination info
    const from = (modalState.page - 1) * modalState.pageSize + 1;
    const to   = Math.min(modalState.page * modalState.pageSize, result.total);
    $('entries-count').textContent  = `Показано ${from}–${to} из ${result.total.toLocaleString('ru')}`;
    $('modal-prev-btn').disabled    = modalState.page <= 1;
    $('modal-next-btn').disabled    = to >= result.total;
    $('modal-page-label').textContent = `Стр. ${modalState.page}`;

    const tbody = $('entries-tbody');
    tbody.innerHTML = '';
    result.items.forEach(entry => {
      const tr = document.createElement('tr');
      tr.className = 'log-row';
      tr.innerHTML = cols.map(c => {
        const val = cellValue(entry, c);
        let cls = '';
        if (c === 'level') cls = `style="color:${LEVEL_COLORS[entry.level] ?? ''}"`;
        return `<td ${cls} class="text-nowrap" style="max-width:280px;overflow:hidden;text-overflow:ellipsis" title="${esc(val)}">${esc(val)}</td>`;
      }).join('');
      tr.addEventListener('click', () => toggleDetail(tr, entry));
      tbody.appendChild(tr);
    });
  } catch (e) {
    $('entries-tbody').innerHTML = `<tr><td colspan="99" class="text-danger p-3">${esc(e.message)}</td></tr>`;
  }
}

function cellValue(entry, col) {
  if (col === 'datetime') return entry.datetime ? new Date(entry.datetime).toLocaleString('ru') : '';
  return entry[col] ?? entry.extra?.[col] ?? '';
}

function toggleDetail(tr, entry) {
  const next = tr.nextElementSibling;
  if (next?.classList.contains('detail-row')) { next.remove(); return; }

  const fields = [
    ['datetime',     entry.datetime ? new Date(entry.datetime).toLocaleString('ru') : ''],
    ['level',        entry.level],
    ['category',     entry.category],
    ['type',         entry.type],
    ['url',          entry.url],
    ['uid',          entry.uid],
    ['message',      entry.message],
    ['body',         entry.body],
    ['responseTime', entry.responseTime != null ? entry.responseTime + 's' : null],
    ['httpCode',     entry.httpCode],
    ['logger',       entry.logger],
    ['sourceFile',   entry.sourceFile],
    ...Object.entries(entry.extra ?? {})
  ].filter(([, v]) => v != null && v !== '');

  const rows = fields.map(([k, v]) => {
    const traceLink = k === 'uid' && v
      ? ` <a href="/trace.html?uid=${encodeURIComponent(v)}&path=${encodeURIComponent(state.path)}&isFile=${state.isFile}&date=${entry.datetime?.slice(0,10) ?? ''}" target="_blank">[→ trace]</a>`
      : '';
    return `<tr>
      <td class="text-muted" style="width:130px">${esc(k)}</td>
      <td style="word-break:break-all">${esc(String(v))}${traceLink}</td>
    </tr>`;
  }).join('');

  const cols = visibleCols().length || 5;
  const detail = document.createElement('tr');
  detail.className = 'detail-row';
  detail.innerHTML = `<td colspan="${cols}"><div class="detail-card"><table class="table table-sm mb-0">${rows}</table></div></td>`;
  tr.after(detail);
}

init();
