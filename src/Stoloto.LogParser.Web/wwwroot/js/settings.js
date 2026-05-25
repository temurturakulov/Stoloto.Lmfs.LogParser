import { getSettings, saveSettings } from './api.js';

let settings = null;
let dragSrc  = null;

async function init() {
  settings = await getSettings();
  renderColumns();
  document.getElementById('interval-input').value = settings.livePollingIntervalSec ?? 2;
  renderSavedFilters();
  renderRecentPaths();

  document.getElementById('save-btn').addEventListener('click', save);
  document.getElementById('reset-btn').addEventListener('click', async () => {
    if (!confirm('Сбросить все настройки?')) return;
    settings.columns      = null;
    settings.savedFilters = [];
    settings.recentPaths  = [];
    await saveSettings(settings);
    location.reload();
  });
  document.getElementById('add-filter-btn').addEventListener('click', addFilter);
}

function renderColumns() {
  const list = document.getElementById('column-list');
  list.innerHTML = '';
  settings.columns.sort((a, b) => a.order - b.order).forEach((col, i) => {
    const item = document.createElement('div');
    item.className = 'list-group-item col-item d-flex align-items-center gap-2';
    item.draggable = true;
    item.dataset.name = col.name;
    item.innerHTML = `
      <span class="drag-handle">⠿</span>
      <input type="checkbox" class="form-check-input" ${col.visible ? 'checked' : ''}>
      <span>${col.name}</span>`;
    item.querySelector('input').addEventListener('change', e => { col.visible = e.target.checked; });
    item.addEventListener('dragstart', e => { dragSrc = item; e.dataTransfer.effectAllowed = 'move'; });
    item.addEventListener('dragover', e => { e.preventDefault(); item.classList.add('drag-over'); });
    item.addEventListener('dragleave', () => item.classList.remove('drag-over'));
    item.addEventListener('drop', e => {
      e.preventDefault();
      item.classList.remove('drag-over');
      if (dragSrc === item) return;
      list.insertBefore(dragSrc, item);
      reorderColumns();
    });
    list.appendChild(item);
  });
}

function reorderColumns() {
  const items = document.querySelectorAll('.col-item');
  items.forEach((item, i) => {
    const col = settings.columns.find(c => c.name === item.dataset.name);
    if (col) col.order = i;
  });
}

function renderSavedFilters() {
  const el = document.getElementById('saved-filters');
  el.innerHTML = '';
  (settings.savedFilters ?? []).forEach((f, i) => {
    const row = document.createElement('div');
    row.className = 'd-flex align-items-center gap-2 mb-1';
    row.innerHTML = `<span class="badge bg-info text-dark">${f.name}</span>
      <small class="text-muted">${[f.level, f.category, f.type, f.search].filter(Boolean).join(', ')}</small>
      <button class="btn btn-sm btn-outline-danger ms-auto py-0" data-idx="${i}">✕</button>`;
    row.querySelector('button').addEventListener('click', () => {
      settings.savedFilters.splice(i, 1);
      renderSavedFilters();
    });
    el.appendChild(row);
  });
}

function addFilter() {
  const name = prompt('Название фильтра:');
  if (!name) return;
  settings.savedFilters.push({ name, level: null, category: null, type: null, search: null });
  renderSavedFilters();
}

function renderRecentPaths() {
  const el = document.getElementById('recent-paths');
  el.innerHTML = '';
  (settings.recentPaths ?? []).forEach((p, i) => {
    const row = document.createElement('div');
    row.className = 'd-flex align-items-center gap-2 mb-1';
    row.innerHTML = `<small class="text-truncate">${p}</small>
      <button class="btn btn-sm btn-outline-danger ms-auto py-0" data-idx="${i}">✕</button>`;
    row.querySelector('button').addEventListener('click', () => {
      settings.recentPaths.splice(i, 1);
      renderRecentPaths();
    });
    el.appendChild(row);
  });
}

async function save() {
  reorderColumns();
  settings.livePollingIntervalSec = Number(document.getElementById('interval-input').value) || 2;
  await saveSettings(settings);
  alert('Настройки сохранены');
}

init();
