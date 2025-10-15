/* ===== DASHBOARD JAVASCRIPT FUNCTIONS ===== */
// Global variables
let startTime = null; // Server start time
let uptimeInterval = null; // Interval ref

// ===== INITIALIZATION =====
document.addEventListener('DOMContentLoaded', function () {
  restoreStartTime();
  syncUptime();
  syncStartupStatus();

  // About widget click toggle
  try {
    const about = document.querySelector('.about-widget');
    if (about) {
      const handle = about.querySelector('.about-handle');
      if (handle) {
        handle.addEventListener('click', (e) => {
          e.stopPropagation();
          about.classList.toggle('open');
        });
      }
      document.addEventListener('click', (e) => {
        if (!about.contains(e.target)) about.classList.remove('open');
      });
      document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') about.classList.remove('open');
      });
    }
  } catch {}
});

// ===== UPTIME MANAGEMENT =====
function saveStartTime() {
  if (startTime) {
    localStorage.setItem('serverStartTime', startTime.toISOString());
  }
}

function restoreStartTime() {
  try {
    const savedStartTime = localStorage.getItem('serverStartTime');
    if (savedStartTime) {
      startTime = new Date(savedStartTime);
      const now = new Date();
      const diff = now - startTime;
      const maxAge = 24 * 60 * 60 * 1000;
      if (diff > maxAge) {
        localStorage.removeItem('serverStartTime');
        startTime = null;
      } else {
        startUptimeCounter();
      }
    }
  } catch {
    localStorage.removeItem('serverStartTime');
  }
}

function startUptimeCounter() {
  if (uptimeInterval) clearInterval(uptimeInterval);
  uptimeInterval = setInterval(updateUptimeDisplay, 1000);
}

function updateUptimeDisplay() {
  if (startTime) {
    const now = new Date();
    const diff = now - startTime;
    const h = Math.floor(diff / (1000 * 60 * 60));
    const m = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
    const s = Math.floor((diff % (1000 * 60)) / 1000);
    const el = document.getElementById('uptime');
    if (el) el.textContent = `${h}:${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  }
}

async function syncUptime() {
  try {
    const keyEl = document.getElementById('shutdownKey');
    const key = (keyEl && keyEl.value) || '1234';
    await syncWithServer(key);
  } catch {
    startTime = new Date();
    startUptimeCounter();
  }
}

async function syncWithServer(key) {
  const res = await fetch(`/api/status?key=${encodeURIComponent(key)}`);
  const data = await res.json();
  if (res.ok && data.start_time && data.status === 'running') {
    startTime = new Date(data.start_time);
    saveStartTime();
    startUptimeCounter();
  } else {
    startTime = new Date();
    saveStartTime();
    startUptimeCounter();
  }
}

// ===== ALERTS =====
function showAlert(message, type) {
  const el = document.getElementById(type === 'success' ? 'successAlert' : 'errorAlert');
  if (!el) return;
  el.textContent = message;
  el.style.display = 'block';
  setTimeout(() => (el.style.display = 'none'), 5000);
}

// ===== ACTIONS =====
async function shutdownComputer() {
  const key = document.getElementById('shutdownKey').value;
  if (!key) return showAlert('Please enter the password', 'error');
  if (!confirm('Are you sure you want to shutdown your computer?')) return;
  try {
    const res = await fetch('/api/shutdown', {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ key })
    });
    const data = await res.json();
    if (res.ok) showAlert('Shutdown command sent!', 'success'); else showAlert(data.error || 'Shutdown failed', 'error');
  } catch (e) { showAlert('Failed to send shutdown command: ' + e.message, 'error'); }
}

async function saveConfig() {
  const payload = {
    key: document.getElementById('shutdownKey').value,
    port: parseInt(document.getElementById('port').value),
    secret_key: document.getElementById('secretKey').value.trim()
  };
  try {
    const res = await fetch('/api/config', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
    const data = await res.json();
    if (res.ok) { showAlert(data.message, 'success'); setTimeout(() => location.reload(), 2000); }
    else showAlert(data.message, 'error');
  } catch (e) { showAlert('Failed to save configuration: ' + e.message, 'error'); }
}

async function toggleStartup(action) {
  try {
    const res = await fetch('/api/startup', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ key: document.getElementById('shutdownKey').value, action }) });
    const data = await res.json();
    if (res.ok) { showAlert(data.message, 'success'); setTimeout(() => location.reload(), 2000); }
    else showAlert(data.message, 'error');
  } catch (e) { showAlert('Failed to update startup settings: ' + e.message, 'error'); }
}

async function refreshStatus() {
  try {
    const key = document.getElementById('shutdownKey').value || '1234';
    const res = await fetch(`/api/status?key=${encodeURIComponent(key)}`);
    const data = await res.json();
    if (!res.ok) return showAlert(data.message || 'Failed to refresh status', 'error');
    if (data.start_time && data.status === 'running') { startTime = new Date(data.start_time); saveStartTime(); startUptimeCounter(); }
    if (data.run_on_startup !== undefined) updateStartupStatus(data.run_on_startup);
    if (data.port) { const el = document.getElementById('port-display'); if (el) el.textContent = data.port; }
    if (data.host) { const el = document.getElementById('host-display'); if (el) el.textContent = data.host; }
    showAlert('Status refreshed successfully!', 'success');
  } catch { showAlert('Failed to refresh status', 'error'); }
}

// ===== COPY =====
function copyShutdownUrl() {
  const url = `http://${window.location.hostname}:${window.location.port}/shutdown?key=${document.getElementById('shutdownKey').value}`;
  navigator.clipboard.writeText(url).then(() => showAlert('Shutdown URL copied to clipboard! This URL includes your current password.', 'success')).catch(() => {
    const ta = document.createElement('textarea'); ta.value = url; document.body.appendChild(ta); ta.select(); document.execCommand('copy'); document.body.removeChild(ta); showAlert('Shutdown URL copied to clipboard!', 'success');
  });
}

function copyDashboardUrl() {
  const url = `http://${window.location.hostname}:${window.location.port}`;
  navigator.clipboard.writeText(url).then(() => showAlert('Dashboard URL copied to clipboard! Password required for shutdown actions.', 'success')).catch(() => {
    const ta = document.createElement('textarea'); ta.value = url; document.body.appendChild(ta); ta.select(); document.execCommand('copy'); document.body.removeChild(ta); showAlert('Dashboard URL copied to clipboard!', 'success');
  });
}

// ===== STARTUP STATUS =====
async function syncStartupStatus() {
  try {
    const key = document.getElementById('shutdownKey').value;
    const res = await fetch(`/api/status?key=${encodeURIComponent(key)}`);
    const data = await res.json();
    if (res.ok && data.run_on_startup !== undefined) updateStartupStatus(data.run_on_startup);
  } catch {}
}

function updateStartupStatus(isInStartup) {
  const ind = document.getElementById('startup-status-indicator');
  const txt = document.getElementById('startup-status-text');
  if (!ind || !txt) return;
  if (isInStartup) { ind.className = 'status-indicator status-running'; txt.textContent = 'Added to startup'; }
  else { ind.className = 'status-indicator status-stopped'; txt.textContent = 'Not in startup'; }

  const qa = document.querySelector('#startup-card .quick-actions');
  if (qa) {
    if (isInStartup) {
      qa.innerHTML = '<button class="btn btn-danger" onclick="toggleStartup(\'remove\')">❌ Remove from Startup</button>';
    } else {
      qa.innerHTML = '<button class="btn btn-success" onclick="toggleStartup(\'add\')">✅ Add to Startup</button>';
    }
  }
}
