const API_BASE = '';
let lastResult = null;
const SAMPLE_PAYLOAD = {
  tasks: [
    { taskId: 1, firstLocation: 'PX-MZ-D08-171F', todoQuantity: 6 },
    { taskId: 2, firstLocation: 'PX-MC-C03-020F', todoQuantity: 4 }
  ],
  personnel: [
    { id: 'p1', name: 'Ayşe', lastLocationCode: 'PX-MZ-D08-171F', pickerExperience: 3, speedFactor: 1.0 },
    { id: 'p2', name: 'Mehmet', lastLocationCode: 'PX-MC-C03-020F', pickerExperience: 2, speedFactor: 0.9 }
  ]
};

function rowTemplate(p = { id:'', name:'', lastLocationCode:'', pickerExperience:3, speedFactor:1, status:'Atama Bekliyor' }, readOnly = true) {
  if (readOnly) {
    return `<tr>
      <td><span class="cell-text" data-key="name" data-id="${p.id||''}" title="${p.name||''}">${p.name||''}</span></td>
      <td>
        <div class="d-flex align-items-center gap-2">
          <span class="cell-text" data-key="lastLocationCode" title="${p.lastLocationCode||''}">${p.lastLocationCode||''}</span>
          <span class="xy-badge liveXY" title="Canlı XY" data-id="${p.id||''}">-,-</span>
        </div>
      </td>
      <td><span class="badge bg-info-subtle text-info">${p.status||'Atama Bekliyor'}</span></td>
      <td><input class="form-control form-control-sm" type="number" min="1" max="5" value="${p.pickerExperience||3}" data-key="pickerExperience"></td>
      <td><input class="form-control form-control-sm" type="number" step="0.1" value="${p.speedFactor||1}" data-key="speedFactor"></td>
      <td><span class="perf-badge" data-id="${p.id||''}">—</span></td>
      <td class="text-end"><button class="btn btn-outline-danger btn-sm del"><i class="bi bi-trash"></i></button></td>
    </tr>`;
  }
  // Düzenlenebilir satır
  return `<tr>
      <td><input class="form-control form-control-sm" value="${p.name||''}" data-key="name" data-id="${p.id||''}"></td>
      <td>
        <div class="input-group input-group-sm">
          <input class="form-control" value="${p.lastLocationCode||''}" placeholder="PX-..." data-key="lastLocationCode">
          <span class="input-group-text liveXY" title="Canlı XY" data-id="${p.id||''}">-,-</span>
        </div>
      </td>
      <td><span class="badge bg-info-subtle text-info">${p.status||'Atama Bekliyor'}</span></td>
      <td><input class="form-control form-control-sm" type="number" min="1" max="5" value="${p.pickerExperience||3}" data-key="pickerExperience"></td>
      <td><input class="form-control form-control-sm" type="number" step="0.1" value="${p.speedFactor||1}" data-key="speedFactor"></td>
      <td><span class="perf-badge" data-id="${p.id||''}">—</span></td>
      <td class="text-end"><button class="btn btn-outline-danger btn-sm del"><i class="bi bi-trash"></i></button></td>
    </tr>`;
}

async function loadPersonnel() {
  try {
    const res = await fetch(`${API_BASE}/personnel`);
    const data = await res.json();
    const tbody = document.getElementById('personTableBody');
    tbody.innerHTML = data.map(p => rowTemplate(p, true)).join('');
    // ardından canlı veri
    await loadLiveData();
  } catch (e) {
    console.warn('Personnel fetch failed', e);
  }
}

async function loadLiveData() {
  try {
    const [locRes, perfRes] = await Promise.all([
      fetch(`${API_BASE}/personnel/locations`),
      fetch(`${API_BASE}/personnel/performance`)
    ]);
    const locs = await locRes.json();
    const perf = await perfRes.json();
    // XY etiketlerini ve performans rozetlerini güncelle
    document.querySelectorAll('.liveXY').forEach(span => {
      const id = span.dataset.id;
      const v = locs[id];
      if (v) span.textContent = `${Math.round(v.Item1||v.x||0)},${Math.round(v.Item2||v.y||0)}`;
    });
    document.querySelectorAll('.perf-badge').forEach(b => {
      const id = b.dataset.id;
      const p = perf[id];
      if (p != null) b.textContent = `PF: ${p.toFixed(2)}`;
    });
  } catch (e) {
    console.warn('Live data fetch failed', e);
  }
}

function getPersonnelFromTable() {
  const rows = Array.from(document.querySelectorAll('#personTableBody tr'));
  return rows.map(r => {
    const fields = r.querySelectorAll('[data-key]');
    const obj = {};
    const idEl = r.querySelector('[data-id]');
    obj.id = (idEl && idEl.dataset.id) ? idEl.dataset.id : crypto.randomUUID();
    fields.forEach(el => {
      const key = el.dataset.key;
      const val = el.tagName === 'INPUT' ? el.value : (el.textContent || '').trim();
      obj[key] = val;
    });
    obj.pickerExperience = parseInt(obj.pickerExperience || '3', 10);
    obj.speedFactor = parseFloat(obj.speedFactor || '1');
    return obj;
  });
}

function renderAssignments(result) {
  const box = document.getElementById('results');
  const items = result.assignments || [];
  const overall = Math.round((result.estimatedCompletionSeconds || 0) / 60);
  document.getElementById('summary').textContent = `Toplam tamamlanma tahmini: ${overall} dk | Personel: ${result.personnelCount} | Görev: ${result.taskCount}`;
  lastResult = result;

  box.innerHTML = items.map(it => {
    const minutes = Math.round((it.totalPredictedTimeSeconds || 0) / 60);
    const percent = overall ? Math.min(100, Math.round(100 * (it.totalPredictedTimeSeconds/60) / overall)) : 0;
    const tasks = (it.assignments || []).map(t => `<span class="pill me-1 mb-1">${t.taskId} • ${t.startLocationCode || t.firstLocation || ''}</span>`).join('');
    return `<div class="card assignment-card mb-3">
        <div class="card-body">
          <div class="d-flex justify-content-between align-items-center mb-1">
            <strong>${it.personnelName || 'Personel'}</strong>
            <small class="text-secondary">Tahmin: ${minutes} dk</small>
          </div>
          <div class="progress mb-2"><div class="progress-bar" role="progressbar" style="width:${percent}%"></div></div>
          <div>${tasks || '<em class="text-secondary">Görev atılmadı</em>'}</div>
        </div>
      </div>`;
  }).join('');
}

// Basit depo haritası çizimi (zone→x, corridor numarası→y)
function codeToXY(code){
  if(!code) return {x:200,y:200};
  try{
    const parts = code.split('-');
    const zone = parts[1];
    const corr = parts[2]||'';
    const num = parseInt((corr.match(/\d+/)||['0'])[0],10);
    const x = zone==='MZ'?460: zone==='MC'?110: zone==='PM02'?200:810;
    const y = 60 + num*10;
    return {x,y};
  }catch{ return {x:200,y:200}; }
}

function drawMap(assignments){
  const svg = document.getElementById('warehouseMap');
  if(!svg) return;
  svg.innerHTML = '';
  // Koridor çizgileri
  for(let i=1;i<=20;i++){
    const y = 60 + i*10;
    const line = `<line x1="60" y1="${y}" x2="860" y2="${y}" stroke="#eef2f7" />`;
    svg.insertAdjacentHTML('beforeend', line);
  }
  // Zone sütunları
  const zones = [{name:'MC',x:110},{name:'PM02',x:200},{name:'MZ',x:460},{name:'Other',x:810}];
  zones.forEach(z=>{
    svg.insertAdjacentHTML('beforeend', `<text x="${z.x}" y="40" font-size="12" fill="#667085">${z.name}</text>`);
  });

  // Isı noktaları ve rota çizgileri
  assignments.forEach(a=>{
    let prev = null;
    (a.assignments||[]).forEach(t=>{
      const start = codeToXY(t.startLocationCode||t.firstLocation);
      svg.insertAdjacentHTML('beforeend', `<circle cx="${start.x}" cy="${start.y}" r="4" fill="rgba(13,110,253,.7)" />`);
      if(prev){
        svg.insertAdjacentHTML('beforeend', `<path d="M ${prev.x} ${prev.y} L ${start.x} ${start.y}" stroke="rgba(13,110,253,.4)" fill="none" />`);
      }
      prev = start;
    });
    // Personel adı
    if(prev){
      svg.insertAdjacentHTML('beforeend', `<text x="${prev.x+6}" y="${prev.y-6}" font-size="11" fill="#0d6efd">${a.personnelName||''}</text>`);
    }
  });
}

async function assign() {
  const excel = document.getElementById('excelInput');
  const jsonText = document.getElementById('jsonInput').value.trim();
  const personnel = getPersonnelFromTable();
  const criteria = {
    mlTimeWeight: parseFloat(document.getElementById('wTime').value),
    distanceWeight: parseFloat(document.getElementById('wDist').value),
    timeWindowWeight: parseFloat(document.getElementById('wTimeWindow').value),
    customerPriorityWeight: parseFloat(document.getElementById('wCustomer').value),
    experienceWeight: parseFloat(document.getElementById('wExp').value),
    speedWeight: parseFloat(document.getElementById('wSpeed').value),
    balanceLoadWeight: parseFloat(document.getElementById('wBalance').value),
    zoneMatchBonus: parseFloat(document.getElementById('wZone').value),
    corridorClusterBonus: parseFloat(document.getElementById('wCorr').value),
    maxTasksPerPerson: parseInt(document.getElementById('maxTasks').value || '0', 10),
    clusterByCorridor: document.getElementById('clusterCorr').checked,
    prioritizeUrgent: document.getElementById('priorUrgent').checked,
    // Birleşik aciliyet kalibrasyonu
    urgencyWeight: parseFloat(document.getElementById('wUrgency').value),
    timeWindowToleranceMinutes: parseInt(document.getElementById('wTol').value||'0',10),
    timeWindowScaleMinutes: parseInt(document.getElementById('wScale').value||'0',10),
    urgentBaseBoost: parseFloat(document.getElementById('wBase').value||'0')
  };

  let response;
  if (excel.files && excel.files.length > 0) {
    const file = excel.files[0];
    const fd = new FormData();
    fd.append('file', file);
    // Backend artık CSV veya Excel dosyasını kabul ediyor
    response = await fetch(`${API_BASE}/assign-picking`, { method:'POST', body: fd });
  } else {
    let payload = {};
    if (jsonText) {
      try { payload = JSON.parse(jsonText); } catch { alert('Geçersiz JSON'); return; }
    }
    payload.personnel = personnel;
    payload.criteria = criteria;
    response = await fetch(`${API_BASE}/assign-picking`, { method:'POST', headers:{ 'Content-Type':'application/json' }, body: JSON.stringify(payload) });
  }
  const result = await response.json();
  if (!response.ok) { alert(result.error || 'Hata oluştu'); return; }
  renderAssignments(result);
  drawMap(result.assignments||[]);
}

function wireEvents() {
  console.log('[assign.js] wireEvents init v1');
  const refreshBtn = document.getElementById('refreshPersonnel');
  refreshBtn?.addEventListener('click', async () => { await loadPersonnel(); });
  const assignEl = document.getElementById('assignBtn');
  assignEl?.addEventListener('click', assign);
  const clearEl = document.getElementById('clearBtn');
  clearEl?.addEventListener('click', () => {
    const res = document.getElementById('results'); if (res) res.innerHTML = '';
    const sum = document.getElementById('summary'); if (sum) sum.textContent = 'Hazır.';
    const map = document.getElementById('warehouseMap'); if (map) map.innerHTML = '';
  });
  const tbody = document.getElementById('personTableBody');
  tbody?.addEventListener('click', (e) => {
    const rowDel = e.target.closest?.('.del');
    if (rowDel) e.target.closest('tr')?.remove();
  });
  const flattenAssignments = () => {
    const rows = [];
    (lastResult?.assignments||[]).forEach(a=>{
      (a.assignments||[]).forEach(t=>{
        rows.push({
          personnel:a.personnelName,
          taskId:t.taskId,
          startLocation:t.startLocationCode,
          distance:t.distanceFromLast,
          predictedMinutes: ((t.predictedTimeSeconds)||0)/60
        });
      });
    });
    return rows;
  };

  const exportCsv = () => {
    if(!lastResult){ alert('Önce atama yapın'); return; }
    const rows = flattenAssignments();
    const csv = ['personnel,taskId,startLocation,distance,predictedMinutes']
      .concat(rows.map(r=>`${r.personnel},${r.taskId},${r.startLocation},${r.distance},${r.predictedMinutes}`)).join('\n');
    const blob = new Blob([csv], {type:'text/csv'});
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a'); a.href=url; a.download='assignments.csv'; a.click(); URL.revokeObjectURL(url);
  };

  const exportXlsx = () => {
    if(!lastResult){ alert('Önce atama yapın'); return; }
    const rows = flattenAssignments();
    const ws = XLSX.utils.json_to_sheet(rows);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Assignments');
    XLSX.writeFile(wb, 'assignments.xlsx');
  };

  const templateHeaders = [
    'Görev No','Müşteri','Marka','İş Emri No','Depo Çıkış Siparişi','Hareket Tipi','Görev Durumu','Malzeme Kodu','Malzeme','Barkod','Dağılım No','Zone','Koridor','Mega İş Listedi ID','Split Tamamlandı Mı?','Miktar','Yapılacak Miktar','Tamamlanan Miktar','PK İş İstasyonu','İlk Lokasyonu','Son Lokasyonu','İş İstasyonu','Rack','Görev Başlama Zamanı','Görev Bitiş Zamanı','İlk Paleti','Son Paleti','İlk Kolisi','Orjinal Sipariş No','Son Kolisi','İlk Paket Tipi','Son Paket Tipi'
  ];
  const downloadTemplateCsv = () => {
    const csv = templateHeaders.join(',') + '\n';
    const blob = new Blob([csv], {type:'text/csv'});
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a'); a.href=url; a.download='task_template.csv'; a.click(); URL.revokeObjectURL(url);
  };
  const downloadTemplateXlsx = () => {
    const ws = XLSX.utils.aoa_to_sheet([templateHeaders]);
    const wb = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(wb, ws, 'Tasks');
    XLSX.writeFile(wb, 'task_template.xlsx');
  };

  const exportCsvBtn = document.getElementById('exportCsv'); exportCsvBtn?.addEventListener('click', (e)=>{ e.preventDefault(); exportCsv(); });
  const exportXlsxBtn = document.getElementById('exportXlsx'); exportXlsxBtn?.addEventListener('click', (e)=>{ e.preventDefault(); exportXlsx(); });
  const templateCsvBtn = document.getElementById('templateCsv'); templateCsvBtn?.addEventListener('click', (e)=>{ e.preventDefault(); downloadTemplateCsv(); });
  const templateXlsxBtn = document.getElementById('templateXlsx'); templateXlsxBtn?.addEventListener('click', (e)=>{ e.preventDefault(); downloadTemplateXlsx(); });
  // Preset butonları
  const setVals = v=>{
    Object.entries(v).forEach(([id,val])=>{ const el=document.getElementById(id); if(el) el.value=val; });
  };
  const presetSpeedBtn = document.getElementById('presetSpeed'); presetSpeedBtn?.addEventListener('click',()=>{
    setVals({ wTime:'0.8', wDist:'0.2', wExp:'0.2', wSpeed:'1.0', wBalance:'0.02', wZone:'0.4', wCorr:'0.2', wTimeWindow:'0.1', wCustomer:'0.2' });
  });
  const presetDistanceBtn = document.getElementById('presetDistance'); presetDistanceBtn?.addEventListener('click',()=>{
    setVals({ wTime:'0.8', wDist:'1.0', wExp:'0.05', wSpeed:'0.1', wBalance:'0.01', wZone:'0.2', wCorr:'0.5', wTimeWindow:'0.0', wCustomer:'0.0' });
  });
  const presetBalancedBtn = document.getElementById('presetBalanced'); presetBalancedBtn?.addEventListener('click',()=>{
    setVals({ wTime:'1.0', wDist:'0.3', wExp:'0.1', wSpeed:'0.2', wBalance:'0.05', wZone:'0.5', wCorr:'0.3', wTimeWindow:'0.1', wCustomer:'0.1' });
  });
  const sampleEl = document.getElementById('sampleJson');
  if (sampleEl) sampleEl.textContent = JSON.stringify(SAMPLE_PAYLOAD, null, 2);
  const copyBtn = document.getElementById('copySampleJson');
  if (copyBtn) {
    const safeCopy = async (text) => {
      try {
        if (navigator.clipboard && navigator.clipboard.writeText) {
          await navigator.clipboard.writeText(text);
          return true;
        }
        throw new Error('Clipboard API unavailable');
      } catch {
        try {
          const ta = document.createElement('textarea');
          ta.value = text;
          ta.style.position = 'fixed';
          ta.style.left = '-9999px';
          document.body.appendChild(ta);
          ta.focus(); ta.select();
          const ok = document.execCommand('copy');
          document.body.removeChild(ta);
          return ok;
        } catch {
          return false;
        }
      }
    };
    copyBtn.addEventListener('click', async () => {
      const text = JSON.stringify(SAMPLE_PAYLOAD, null, 2);
      const ok = await safeCopy(text);
      if (ok) {
        copyBtn.disabled = true;
        copyBtn.textContent = 'Kopyalandı';
        setTimeout(() => { copyBtn.disabled = false; copyBtn.innerHTML = '<i class="bi bi-clipboard"></i> Kopyala'; }, 1200);
      } else {
        alert('Kopyalama başarısız. Metni seçip Ctrl+C ile kopyalayabilirsiniz.');
      }
    });
  }
  const fillBtn = document.getElementById('fillSampleJson');
  if (fillBtn) {
    fillBtn.addEventListener('click', () => {
      const ta = document.getElementById('jsonInput');
      if (ta) {
        ta.value = JSON.stringify(SAMPLE_PAYLOAD, null, 2);
        ta.focus();
      }
    });
  }
}

document.addEventListener('DOMContentLoaded', async () => {
  console.log('[assign.js] DOMContentLoaded');
  wireEvents();
  await loadPersonnel();
});