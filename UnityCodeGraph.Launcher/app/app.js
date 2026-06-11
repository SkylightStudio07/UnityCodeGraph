const api = window.chrome?.webview;

const els = {
  projectPath: document.querySelector("#projectPath"),
  gitUrl: document.querySelector("#gitUrl"),
  roots: document.querySelector("#roots"),
  outputPath: document.querySelector("#outputPath"),
  browse: document.querySelector("#browse"),
  clone: document.querySelector("#clone"),
  generate: document.querySelector("#generate"),
  watch: document.querySelector("#watch"),
  stop: document.querySelector("#stop"),
  openCanvas: document.querySelector("#openCanvas"),
  clearLog: document.querySelector("#clearLog"),
  log: document.querySelector("#log"),
  statusPill: document.querySelector("#statusPill"),
  statusText: document.querySelector("#statusText")
};

const send = (message) => api?.postMessage(message);

els.browse.addEventListener("click", () => send({ type: "browse" }));
els.clone.addEventListener("click", () => send({ type: "clone", url: els.gitUrl.value }));
els.generate.addEventListener("click", () => send({ type: "generate", ...settings() }));
els.watch.addEventListener("click", () => send({ type: "watch", ...settings() }));
els.stop.addEventListener("click", () => send({ type: "stop" }));
els.openCanvas.addEventListener("click", () => send({ type: "openCanvas" }));
els.clearLog.addEventListener("click", () => {
  els.log.textContent = "";
});

api?.addEventListener("message", (event) => {
  const { type, payload } = event.data;
  if (type === "state") {
    els.outputPath.value = payload.defaultOutput ?? "code-graph.json";
    setRunning(Boolean(payload.running));
  }
  if (type === "projectSelected") {
    els.projectPath.value = payload.path ?? "";
    els.outputPath.value = payload.output ?? "";
  }
  if (type === "runningChanged") {
    setRunning(Boolean(payload.running), payload.mode);
  }
  if (type === "log") {
    log(payload.message);
  }
});

send({ type: "ready" });

function settings() {
  return {
    projectPath: els.projectPath.value,
    roots: els.roots.value,
    outputPath: els.outputPath.value
  };
}

function setRunning(running, mode = "") {
  els.statusPill.textContent = running ? "RUNNING" : "IDLE";
  els.statusPill.classList.toggle("running", running);
  els.statusText.textContent = running
    ? mode === "watch" ? "Watching for C# changes" : "Generating graph"
    : "Ready to generate";
  els.generate.disabled = running;
  els.watch.disabled = running;
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
