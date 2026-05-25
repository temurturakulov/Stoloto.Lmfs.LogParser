import { getStats, getLogs, getSettings } from './api.js';

const LEVEL_COLORS = {
  Info:  '#0d6efd',
  Warn:  '#fd7e14',
  Error: '#dc3545',
  Debug: '#6c757d',
};

const $ = id => document.getElementById(id);
let chartInstance = null;
let state = { path: '', isFile: false };

function esc(s) {
  return String(s ?? '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}

async function init() {
  const today = new Date().toISOString().slice(0, 10);
  $('date-from').value = today;
  $('date-to').value   = today;

  const settings = await getSettings();
  if (settings.lastLogPath) {
    state.path   = settings.lastLogPath;
    state.isFile = settings.lastPathIsFile;
    $('path-label').textContent = state.path;
    load();
  } else {
    $('no-path').style.display = '';
  }

  $('load-btn').addEventListener('click', load);
}

async function load() {
  if (!state.path) { $('no-path').style.display = ''; return; }
  $('no-path').style.display = 'none';

  let stats;
  try {
    stats = await getStats({
      path: state.path, isFile: state.isFile,
      dateFrom: $('date-from').value, dateTo: $('date-to').value
    });
  } catch (e) {
    alert('Ошибка: ' + e.message);
    return;
  }

  renderCards(stats.byLevel);
  renderChart(stats.byHour, stats.byLevel);
}

function renderCards(byLevel) {
  const container = $('level-cards');
  container.innerHTML = '';
  const levels = ['Error', 'Warn', 'Info', 'Debug'];

  levels.forEach(level => {
    const count = byLevel[level] ?? 0;
    const col = document.createElement('div');
    col.className = 'col-6 col-md-3';
    col.innerHTML = `
      <div class="card level-card level-${level} p-3" data-level="${level}" role="button">
        <div class="text-muted small">${level}</div>
        <div class="count-num" style="color:${LEVEL_COLORS[level] ?? '#333'}">${count.toLocaleString('ru')}</div>
        <div class="text-muted small mt-1">записей</div>
      </div>`;
    col.querySelector('.card').addEventListener('click', () => openEntries(level, count));
    container.appendChild(col);
  });

  // extra levels not in the standard list
  Object.entries(byLevel).forEach(([level, count]) => {
    if (levels.includes(level)) return;
    const col = document.createElement('div');
    col.className = 'col-6 col-md-3';
    col.innerHTML = `
      <div class="card level-card p-3" style="border-color:#adb5bd" data-level="${level}" role="button">
        <div class="text-muted small">${esc(level)}</div>
        <div class="count-num" style="color:#adb5bd">${count.toLocaleString('ru')}</div>
        <div class="text-muted small mt-1">записей</div>
      </div>`;
    col.querySelector('.card').addEventListener('click', () => openEntries(level, count));
    container.appendChild(col);
  });
}

function renderChart(byHour, byLevel) {
  $('chart-card').style.display = '';
  const levels = Object.keys(byLevel).filter(l => byLevel[l] > 0);
  const labels = byHour.map(h => `${String(h.hour).padStart(2,'0')}:00`);

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

async function openEntries(level, total) {
  $('entries-modal-title').textContent = `${level} — записей: ${total.toLocaleString('ru')}`;
  $('entries-count').textContent = 'Загрузка...';
  $('entries-tbody').innerHTML = '<tr><td colspan="5" class="text-center text-muted">Загрузка...</td></tr>';

  const modal = new bootstrap.Modal($('entries-modal'));
  modal.show();

  try {
    const result = await getLogs({
      path: state.path, isFile: state.isFile,
      dateFrom: $('date-from').value, dateTo: $('date-to').value,
      levels: [level],
      page: 1, pageSize: 200, sortAsc: false
    });

    $('entries-count').textContent = `Показано ${result.items.length} из ${result.total}`;
    const tbody = $('entries-tbody');
    tbody.innerHTML = '';

    result.items.forEach(entry => {
      const tr = document.createElement('tr');
      tr.className = 'log-row';
      tr.innerHTML = `
        <td class="text-nowrap small">${entry.datetime ? new Date(entry.datetime).toLocaleString('ru') : ''}</td>
        <td class="small" style="color:${LEVEL_COLORS[entry.level] ?? ''}">${esc(entry.level)}</td>
        <td class="small">${esc(entry.category)}</td>
        <td class="small text-truncate" style="max-width:200px" title="${esc(entry.url)}">${esc(entry.url)}</td>
        <td class="small text-truncate" style="max-width:300px" title="${esc(entry.message)}">${esc(entry.message)}</td>`;
      tr.addEventListener('click', () => toggleDetail(tr, entry, tbody));
      tbody.appendChild(tr);
    });
  } catch (e) {
    $('entries-tbody').innerHTML = `<tr><td colspan="5" class="text-danger">${esc(e.message)}</td></tr>`;
  }
}

function toggleDetail(tr, entry, tbody) {
  const next = tr.nextElementSibling;
  if (next?.classList.contains('detail-row')) { next.remove(); return; }

  const fields = [
    ['datetime', entry.datetime ? new Date(entry.datetime).toLocaleString('ru') : ''],
    ['level', entry.level], ['category', entry.category], ['type', entry.type],
    ['url', entry.url], ['uid', entry.uid], ['message', entry.message],
    ['body', entry.body],
    ['responseTime', entry.responseTime != null ? entry.responseTime + 's' : null],
    ['httpCode', entry.httpCode], ['logger', entry.logger],
    ...Object.entries(entry.extra ?? {})
  ].filter(([, v]) => v != null && v !== '');

  const rows = fields.map(([k, v]) =>
    `<tr><td class="text-muted" style="width:130px">${esc(k)}</td><td style="word-break:break-all">${esc(String(v))}</td></tr>`
  ).join('');

  const detail = document.createElement('tr');
  detail.className = 'detail-row';
  detail.innerHTML = `<td colspan="5"><div class="detail-card"><table class="table table-sm mb-0">${rows}</table></div></td>`;
  tr.after(detail);
}

init();
