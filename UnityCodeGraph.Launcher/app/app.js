const api = window.chrome?.webview;
const RECENT_KEY = "UnityCodeGraph.launcher.recent";
const LAST_SETTINGS_KEY = "UnityCodeGraph.launcher.lastSettings";
const MAX_RECENTS = 6;

const els = {
  projectPath: document.querySelector("#projectPath"),
  gitUrl: document.querySelector("#gitUrl"),
  roots: document.querySelector("#roots"),
  outputPath: document.querySelector("#outputPath"),
  enhanceScope: document.querySelector("#enhanceScope"),
  enhanceSystem: document.querySelector("#enhanceSystem"),
  browse: document.querySelector("#browse"),
  clone: document.querySelector("#clone"),
  generate: document.querySelector("#generate"),
  watch: document.querySelector("#watch"),
  enhanceContext: document.querySelector("#enhanceContext"),
  stop: document.querySelector("#stop"),
  openCanvas: document.querySelector("#openCanvas"),
  clearLog: document.querySelector("#clearLog"),
  clearRecent: document.querySelector("#clearRecent"),
  recentList: document.querySelector("#recentList"),
  log: document.querySelector("#log"),
  statusPill: document.querySelector("#statusPill"),
  statusText: document.querySelector("#statusText")
};

const send = (message) => api?.postMessage(message);

els.browse.addEventListener("click", () => send({ type: "browse" }));
els.clone.addEventListener("click", () => send({ type: "clone", url: els.gitUrl.value }));
els.generate.addEventListener("click", () => {
  rememberCurrentSettings();
  send({ type: "generate", ...settings() });
});
els.watch.addEventListener("click", () => {
  rememberCurrentSettings();
  send({ type: "watch", ...settings() });
});
els.enhanceContext.addEventListener("click", () => {
  rememberCurrentSettings();
  send({ type: "enhanceContext", ...settings() });
});
els.stop.addEventListener("click", () => send({ type: "stop" }));
els.openCanvas.addEventListener("click", () => {
  rememberCurrentSettings();
  send({ type: "openCanvas" });
});
els.clearLog.addEventListener("click", () => {
  els.log.textContent = "";
});
els.clearRecent.addEventListener("click", () => {
  localStorage.removeItem(RECENT_KEY);
  renderRecentList();
});

for (const input of [els.projectPath, els.roots, els.outputPath]) {
  input.addEventListener("change", rememberLastSettings);
}
els.enhanceScope.addEventListener("change", renderEnhanceOptions);

api?.addEventListener("message", (event) => {
  const { type, payload } = event.data;
  if (type === "state") {
    const last = readLastSettings();
    if (last) {
      applySettings(last);
    } else {
      els.outputPath.value = payload.defaultOutput ?? "code-graph.json";
    }
    setRunning(Boolean(payload.running));
    renderRecentList();
  }
  if (type === "projectSelected") {
    els.projectPath.value = payload.path ?? "";
    els.outputPath.value = payload.output ?? "";
    rememberCurrentSettings();
  }
  if (type === "runningChanged") {
    setRunning(Boolean(payload.running), payload.mode);
  }
  if (type === "log") {
    log(payload.message);
  }
});

renderRecentList();
renderEnhanceOptions();
send({ type: "ready" });

function settings() {
  return {
    projectPath: els.projectPath.value,
    roots: els.roots.value,
    outputPath: els.outputPath.value,
    enhanceScope: els.enhanceScope.value,
    enhanceSystem: els.enhanceSystem.value.trim()
  };
}

function applySettings(item) {
  els.projectPath.value = item.projectPath ?? "";
  els.roots.value = item.roots || "Scripts,Source";
  els.outputPath.value = item.outputPath || "code-graph.json";
}

function rememberCurrentSettings() {
  const current = normalizedSettings();
  rememberLastSettings(current);
  if (!current.projectPath && !current.outputPath) {
    return;
  }

  const recents = readRecents()
    .filter(item => recentKey(item) !== recentKey(current));
  recents.unshift({ ...current, savedAt: new Date().toISOString() });
  writeRecents(recents.slice(0, MAX_RECENTS));
  renderRecentList();
}

function rememberLastSettings(item = normalizedSettings()) {
  localStorage.setItem(LAST_SETTINGS_KEY, JSON.stringify(item));
}

function readLastSettings() {
  try {
    const raw = localStorage.getItem(LAST_SETTINGS_KEY);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

function normalizedSettings() {
  return {
    projectPath: els.projectPath.value.trim(),
    roots: els.roots.value.trim() || "Scripts,Source",
    outputPath: els.outputPath.value.trim()
  };
}

function readRecents() {
  try {
    const raw = localStorage.getItem(RECENT_KEY);
    const parsed = raw ? JSON.parse(raw) : [];
    return Array.isArray(parsed) ? parsed.filter(isRecentItem) : [];
  } catch {
    return [];
  }
}

function writeRecents(items) {
  localStorage.setItem(RECENT_KEY, JSON.stringify(items));
}

function renderRecentList() {
  const recents = readRecents();
  els.recentList.innerHTML = recents.length
    ? recents.map((item, index) => recentItemHtml(item, index)).join("")
    : "<p class=\"recent-empty\">No recent projects</p>";

  for (const button of els.recentList.querySelectorAll("[data-recent-index]")) {
    button.addEventListener("click", () => {
      const item = readRecents()[Number(button.dataset.recentIndex)];
      if (!item) return;
      applySettings(item);
      rememberLastSettings(item);
    });
  }
}

function recentItemHtml(item, index) {
  return `
    <button class="recent-item" type="button" data-recent-index="${index}">
      <strong>${escapeHtml(displayName(item.projectPath || item.outputPath || "Graph"))}</strong>
      <span>${escapeHtml(shortPath(item.outputPath || item.projectPath || ""))}</span>
    </button>
  `;
}

function isRecentItem(item) {
  return item
    && typeof item === "object"
    && (typeof item.projectPath === "string" || typeof item.outputPath === "string");
}

function recentKey(item) {
  return `${item.projectPath ?? ""}|${item.outputPath ?? ""}`.toLowerCase();
}

function displayName(path) {
  const normalized = String(path).replaceAll("\\", "/").replace(/\/+$/, "");
  return normalized.split("/").pop() || normalized || "Graph";
}

function shortPath(path) {
  const normalized = String(path).replaceAll("\\", "/");
  const parts = normalized.split("/").filter(Boolean);
  return parts.length > 3 ? `.../${parts.slice(-3).join("/")}` : normalized;
}

function setRunning(running, mode = "") {
  els.statusPill.textContent = running ? "RUNNING" : "IDLE";
  els.statusPill.classList.toggle("running", running);
  els.statusText.textContent = running
    ? mode === "watch" ? "Watching for C# changes" : mode === "enhance" ? "Enhancing AI context" : "Generating graph"
    : "Ready to generate";
  els.generate.disabled = running;
  els.watch.disabled = running;
  els.enhanceContext.disabled = running;
  els.enhanceScope.disabled = running;
  els.enhanceSystem.disabled = running || els.enhanceScope.value !== "system";
  els.clone.disabled = running;
  els.browse.disabled = running;
  els.stop.disabled = !running;
}

function log(message) {
  if (!message) return;
  const time = new Date().toLocaleTimeString();
  els.log.textContent += `[${time}] ${message}\n`;
  els.log.scrollTop = els.log.scrollHeight;
}

function renderEnhanceOptions() {
  const single = els.enhanceScope.value === "system";
  els.enhanceSystem.disabled = !single;
  els.enhanceSystem.placeholder = single
    ? "battle-system or systems/battle-system.md"
    : "Used only for Single System";
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}
