const NODE_WIDTH = 220;
const NODE_HEIGHT = 92;
const NODE_GAP_X = 28;
const NODE_GAP_Y = 24;
const SECTION_PADDING = 24;
const SECTION_HEADER = 54;
const SECTION_GAP = 46;
const SECTION_ROW_WIDTH = 3200;
const SYSTEM_WIDTH = 260;
const SYSTEM_HEIGHT = 132;
const SYSTEM_GAP_X = 80;
const SYSTEM_GAP_Y = 70;
const SAMPLE_URL = "../samples/mini-graph.json";
const VIEW_STATE_VERSION = 1;
const VIEW_STATE_PREFIX = "UnityCodeGraph:view:";
const AUTO_RELOAD_INTERVAL_MS = 2500;
const LANGUAGE_STORAGE_KEY = "UnityCodeGraph:language";
const AI_CONFIG_STORAGE_KEY = "UnityCodeGraph:ai-config";
const AI_CACHE_VERSION = 2;
const AI_PROVIDER_PRESETS = {
  disabled: {
    baseUrl: "",
    models: []
  },
  openai: {
    baseUrl: "https://api.openai.com/v1",
    models: ["gpt-5.4-mini", "gpt-5.5", "gpt-5.4", "gpt-5.4-nano", "gpt-5"]
  },
  openrouter: {
    baseUrl: "https://openrouter.ai/api/v1",
    models: [
      "openai/gpt-5.2",
      "anthropic/claude-sonnet-4.6",
      "google/gemini-3.5-flash",
      "deepseek/deepseek-v4-flash",
      "qwen/qwen3.7-max",
      "moonshotai/kimi-k2.7-code"
    ]
  },
  deepseek: {
    baseUrl: "https://api.deepseek.com",
    models: ["deepseek-v4-flash", "deepseek-v4-pro", "deepseek-chat", "deepseek-reasoner"]
  },
  compatible: {
    baseUrl: "https://api.openai.com/v1",
    models: [
      "deepseek-v4-flash",
      "deepseek-v4-pro",
      "claude-sonnet-4-6",
      "claude-opus-4-8",
      "gemini-3.5-flash",
      "gemini-3.1-pro",
      "moonshotai/kimi-k2.7-code",
      "qwen/qwen3.7-max"
    ]
  },
  ollama: {
    baseUrl: "http://127.0.0.1:11434",
    models: ["qwen3-coder", "gpt-oss", "gemma4", "deepseek-r1", "qwen3.6", "llama4", "glm-4.7-flash"]
  },
  vertex: {
    baseUrl: "https://aiplatform.googleapis.com",
    models: ["gemini-3-flash-preview", "gemini-2.5-flash", "gemini-2.5-pro", "gemini-2.0-flash", "gemini-1.5-pro"]
  }
};

const sampleGraph = {
  RootPath: "embedded sample",
  Nodes: [
    { Id: "Sample.Gameplay.PlayerController", Name: "PlayerController", Namespace: "Sample.Gameplay", Kind: "class", IsUnityType: true, File: "PlayerController.cs", Line: 10, BaseTypes: ["MonoBehaviour", "IDamageable"], Attributes: [] },
    { Id: "Sample.Gameplay.HealthView", Name: "HealthView", Namespace: "Sample.Gameplay", Kind: "class", IsUnityType: true, File: "HealthView.cs", Line: 5, BaseTypes: ["MonoBehaviour"], Attributes: [] },
    { Id: "Sample.Gameplay.InventoryModel", Name: "InventoryModel", Namespace: "Sample.Gameplay", Kind: "class", IsUnityType: false, File: "InventoryModel.cs", Line: 3, BaseTypes: [], Attributes: [] },
    { Id: "Sample.Gameplay.Weapon", Name: "Weapon", Namespace: "Sample.Gameplay", Kind: "class", IsUnityType: true, File: "Weapon.cs", Line: 6, BaseTypes: ["ScriptableObject"], Attributes: ["CreateAssetMenu"] },
    { Id: "Sample.Gameplay.IDamageable", Name: "IDamageable", Namespace: "Sample.Gameplay", Kind: "interface", IsUnityType: false, File: "PlayerController.cs", Line: 5, BaseTypes: [], Attributes: [] }
  ],
  SystemClusters: [
    { Id: "player", Name: "Player / Input", Score: 12, NodeIds: ["Sample.Gameplay.PlayerController"], EntryMethodIds: [], Keywords: ["player", "controller"], InternalEdges: 0, ExternalEdges: 4 },
    { Id: "ui", Name: "UI Layer", Score: 10, NodeIds: ["Sample.Gameplay.HealthView"], EntryMethodIds: [], Keywords: ["health", "view"], InternalEdges: 0, ExternalEdges: 2 },
    { Id: "data", Name: "Data / Config", Score: 9, NodeIds: ["Sample.Gameplay.InventoryModel", "Sample.Gameplay.Weapon"], EntryMethodIds: [], Keywords: ["inventory", "weapon"], InternalEdges: 1, ExternalEdges: 3 }
  ],
  Edges: [
    { Source: "Sample.Gameplay.PlayerController", Target: "Sample.Gameplay.IDamageable", Kind: "implements", Weight: 1, Examples: [{ File: "PlayerController.cs", Line: 10, Text: "IDamageable" }] },
    { Source: "Sample.Gameplay.PlayerController", Target: "Sample.Gameplay.HealthView", Kind: "has_field_type", Weight: 1, Examples: [{ File: "PlayerController.cs", Line: 12, Text: "HealthView" }] },
    { Source: "Sample.Gameplay.PlayerController", Target: "Sample.Gameplay.HealthView", Kind: "calls_member", Weight: 2, Examples: [{ File: "PlayerController.cs", Line: 19, Text: "healthView.Bind(inventory)" }] },
    { Source: "Sample.Gameplay.PlayerController", Target: "Sample.Gameplay.Weapon", Kind: "unity_get_component", Weight: 1, Examples: [{ File: "PlayerController.cs", Line: 17, Text: "Weapon" }] },
    { Source: "Sample.Gameplay.PlayerController", Target: "Sample.Gameplay.InventoryModel", Kind: "creates", Weight: 1, Examples: [{ File: "PlayerController.cs", Line: 18, Text: "InventoryModel" }] },
    { Source: "Sample.Gameplay.HealthView", Target: "Sample.Gameplay.InventoryModel", Kind: "accepts_parameter", Weight: 1, Examples: [{ File: "HealthView.cs", Line: 7, Text: "InventoryModel" }] },
    { Source: "Sample.Gameplay.InventoryModel", Target: "Sample.Gameplay.Weapon", Kind: "has_field_type", Weight: 1, Examples: [{ File: "InventoryModel.cs", Line: 5, Text: "Weapon" }] }
  ],
  Methods: [
    { Id: "Sample.Gameplay.PlayerController.Awake@15", TypeId: "Sample.Gameplay.PlayerController", Name: "Awake", Signature: "Awake()", Kind: "method", File: "PlayerController.cs", Line: 15, IsEntryPoint: true, EntryKind: "unity_lifecycle" },
    { Id: "Sample.Gameplay.PlayerController.TakeDamage@22", TypeId: "Sample.Gameplay.PlayerController", Name: "TakeDamage", Signature: "TakeDamage(int)", Kind: "method", File: "PlayerController.cs", Line: 22, IsEntryPoint: false, EntryKind: "" },
    { Id: "Sample.Gameplay.HealthView.Bind@7", TypeId: "Sample.Gameplay.HealthView", Name: "Bind", Signature: "Bind(InventoryModel)", Kind: "method", File: "HealthView.cs", Line: 7, IsEntryPoint: false, EntryKind: "" },
    { Id: "Sample.Gameplay.HealthView.ShowDamage@11", TypeId: "Sample.Gameplay.HealthView", Name: "ShowDamage", Signature: "ShowDamage(int)", Kind: "method", File: "HealthView.cs", Line: 11, IsEntryPoint: false, EntryKind: "" }
  ],
  MethodEdges: [
    { Source: "Sample.Gameplay.PlayerController.Awake@15", Target: "Sample.Gameplay.HealthView.Bind@7", Kind: "calls", Weight: 1, Examples: [{ File: "PlayerController.cs", Line: 19, Text: "healthView.Bind(inventory)" }] },
    { Source: "Sample.Gameplay.PlayerController.TakeDamage@22", Target: "Sample.Gameplay.HealthView.ShowDamage@11", Kind: "calls", Weight: 1, Examples: [{ File: "PlayerController.cs", Line: 24, Text: "healthView.ShowDamage(amount)" }] }
  ]
};

const state = {
  graph: null,
  positions: new Map(),
  sections: new Map(),
  edgeKinds: new Set(),
  enabledKinds: new Set(),
  selected: null,
  search: "",
  viewMode: "type",
  groupBy: "namespace",
  edgeMode: "focused",
  neighborhoodDepth: 2,
  neighborhoodDirection: "both",
  selectedSystemId: "",
  selectedEntry: "",
  flowDepth: 3,
  pinView: false,
  autoReload: false,
  graphUrl: "",
  graphLabel: "",
  graphFingerprint: "",
  reloadTimer: 0,
  reloadBusy: false,
  lastReloadAt: "",
  aiStatus: {
    checked: false,
    enabled: false,
    provider: "",
    model: "",
    baseUrl: "",
    apiKeyConfigured: false,
    apiKeyStored: false,
    vertexProjectId: "",
    vertexLocation: "",
    vertexCredentialsConfigured: false,
    reason: "AI status has not been checked yet."
  },
  aiConfig: {
    provider: "disabled",
    baseUrl: "",
    model: "",
    vertexProjectId: "",
    vertexLocation: "us-central1"
  },
  aiModels: [],
  aiSettingsOpen: false,
  language: "ko",
  languageSettingsOpen: false,
  aiBusyNodeId: "",
  aiError: "",
  explainOpen: false,
  explainTarget: null,
  explainBusyKey: "",
  explainError: "",
  explainWidth: Number(localStorage.getItem("UnityCodeGraph:explain-width") || 620),
  explainResize: null,
  workflowFocus: null,
  storageKey: "",
  saveTimer: 0,
  transform: { x: 40, y: 40, scale: 1 },
  drag: null,
  pan: null
};

const els = {
  svg: document.getElementById("graphCanvas"),
  viewport: document.getElementById("viewport"),
  nodes: document.getElementById("nodesLayer"),
  edges: document.getElementById("edgesLayer"),
  subtitle: document.getElementById("graphSubtitle"),
  edgeFilters: document.getElementById("edgeFilters"),
  systemList: document.getElementById("systemList"),
  search: document.getElementById("searchInput"),
  file: document.getElementById("fileInput"),
  layoutFile: document.getElementById("layoutInput"),
  exportLayout: document.getElementById("exportLayoutButton"),
  sample: document.getElementById("sampleButton"),
  autoReload: document.getElementById("autoReloadButton"),
  pinMode: document.getElementById("pinModeButton"),
  aiSettingsButton: document.getElementById("aiSettingsButton"),
  aiSettingsPanel: document.getElementById("aiSettingsPanel"),
  aiSettingsClose: document.getElementById("aiSettingsCloseButton"),
  languageSettingsButton: document.getElementById("languageSettingsButton"),
  languageSettingsPanel: document.getElementById("languageSettingsPanel"),
  languageSettingsClose: document.getElementById("languageSettingsCloseButton"),
  languageOptions: document.getElementById("languageSettingsPanel"),
  languageStatus: document.getElementById("languageSettingsStatus"),
  aiProvider: document.getElementById("aiProviderSelect"),
  aiBaseUrl: document.getElementById("aiBaseUrlInput"),
  aiApiKey: document.getElementById("aiApiKeyInput"),
  aiSaveApiKey: document.getElementById("aiSaveApiKeyInput"),
  vertexSettings: document.getElementById("vertexSettings"),
  vertexProjectId: document.getElementById("vertexProjectIdInput"),
  vertexLocation: document.getElementById("vertexLocationInput"),
  vertexServiceAccount: document.getElementById("vertexServiceAccountInput"),
  vertexServiceAccountFile: document.getElementById("vertexServiceAccountFileInput"),
  aiModel: document.getElementById("aiModelInput"),
  aiModelList: document.getElementById("aiModelList"),
  aiRefreshModels: document.getElementById("aiRefreshModelsButton"),
  aiSaveSettings: document.getElementById("aiSaveSettingsButton"),
  aiSettingsStatus: document.getElementById("aiSettingsStatus"),
  fit: document.getElementById("fitButton"),
  reset: document.getElementById("resetButton"),
  viewMode: document.getElementById("viewModeSelect"),
  group: document.getElementById("groupSelect"),
  edgeMode: document.getElementById("edgeModeSelect"),
  neighborhoodDepth: document.getElementById("neighborhoodDepthSelect"),
  neighborhoodDirection: document.getElementById("neighborhoodDirectionSelect"),
  nodeCount: document.getElementById("nodeCount"),
  edgeCount: document.getElementById("edgeCount"),
  unityCount: document.getElementById("unityCount"),
  detailTitle: document.getElementById("detailTitle"),
  detailSubtitle: document.getElementById("detailSubtitle"),
  detailList: document.getElementById("detailList"),
  secondaryTitle: document.getElementById("secondaryTitle"),
  examples: document.getElementById("exampleList"),
  aiSummary: document.getElementById("aiSummary"),
  explainDrawer: document.getElementById("explainDrawer"),
  explainResize: document.getElementById("explainResizeHandle"),
  explainTitle: document.getElementById("explainTitle"),
  explainSubtitle: document.getElementById("explainSubtitle"),
  explainContent: document.getElementById("explainContent"),
  explainClose: document.getElementById("explainCloseButton"),
  explainExport: document.getElementById("explainExportButton"),
  explainRegenerate: document.getElementById("explainRegenerateButton"),
  entrySelect: document.getElementById("entrySelect"),
  flowDepth: document.getElementById("flowDepthSelect"),
  flowList: document.getElementById("flowList"),
  empty: document.getElementById("emptyState"),
  emptyTitle: document.getElementById("emptyTitle"),
  emptyMessage: document.getElementById("emptyMessage")
};

init();

async function init() {
  loadSavedLanguage();
  loadSavedAiConfig();
  bindEvents();
  await syncSavedAiConfig();
  await loadAiStatus();
  renderLanguageSettings();
  renderAiSettings();
  await loadInitialGraph();
  requestAnimationFrame(() => document.body.classList.add("graph-loaded"));
}

function loadSavedLanguage() {
  const saved = localStorage.getItem(LANGUAGE_STORAGE_KEY);
  state.language = saved === "en" || saved === "ko" ? saved : "ko";
  applyLanguageSetting();
}

function applyLanguageSetting() {
  document.documentElement.lang = state.language;
}

function loadSavedAiConfig() {
  const fallback = providerPreset("disabled");
  try {
    const saved = JSON.parse(localStorage.getItem(AI_CONFIG_STORAGE_KEY) || "{}");
    const provider = saved.provider || "disabled";
    const preset = providerPreset(provider);
    state.aiConfig = {
      provider,
      baseUrl: saved.baseUrl ?? preset.baseUrl ?? fallback.baseUrl,
      model: saved.model ?? preset.models?.[0] ?? "",
      vertexProjectId: saved.vertexProjectId ?? "",
      vertexLocation: saved.vertexLocation ?? "us-central1"
    };
  } catch {
    state.aiConfig = {
      provider: "disabled",
      baseUrl: fallback.baseUrl,
      model: "",
      vertexProjectId: "",
      vertexLocation: "us-central1"
    };
  }
  state.aiModels = providerPreset(state.aiConfig.provider).models ?? [];
}

async function syncSavedAiConfig() {
  if (!window.location.protocol.startsWith("http")) return;
  if (!state.aiConfig.provider || state.aiConfig.provider === "disabled") return;
  try {
    await fetch("/ai/config", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(state.aiConfig)
    });
  } catch {
    // Saved AI preferences are optional.
  }
}

async function loadAiStatus() {
  if (!window.location.protocol.startsWith("http")) {
    state.aiStatus = {
      checked: true,
      enabled: false,
      provider: "",
      model: "",
      baseUrl: "",
      apiKeyConfigured: false,
      apiKeyStored: false,
      vertexProjectId: "",
      vertexLocation: "",
      vertexCredentialsConfigured: false,
      reason: "Open through the launcher to check AI status."
    };
    return;
  }

  const controller = new AbortController();
  const timeoutId = window.setTimeout(() => controller.abort(), 1200);
  try {
    const response = await fetch("/ai/status", {
      cache: "no-store",
      signal: controller.signal
    });
    if (!response.ok) throw new Error(`AI status unavailable: ${response.status}`);
    const status = await response.json();
    state.aiStatus = {
      checked: true,
      enabled: Boolean(status.enabled),
      provider: status.provider || "",
      model: status.model || "",
      baseUrl: status.baseUrl || "",
      apiKeyConfigured: Boolean(status.apiKeyConfigured),
      apiKeyStored: Boolean(status.apiKeyStored),
      vertexProjectId: status.vertexProjectId || "",
      vertexLocation: status.vertexLocation || "",
      vertexCredentialsConfigured: Boolean(status.vertexCredentialsConfigured),
      reason: status.reason || ""
    };
    state.aiConfig = {
      provider: state.aiStatus.provider || state.aiConfig.provider,
      baseUrl: state.aiStatus.baseUrl || state.aiConfig.baseUrl,
      model: state.aiStatus.model || state.aiConfig.model,
      vertexProjectId: state.aiStatus.vertexProjectId || state.aiConfig.vertexProjectId,
      vertexLocation: state.aiStatus.vertexLocation || state.aiConfig.vertexLocation
    };
    state.aiModels = uniqueModels([state.aiConfig.model, ...(status.modelSuggestions ?? []), ...state.aiModels]);
  } catch (error) {
    console.warn(error);
    state.aiStatus = {
      checked: true,
      enabled: false,
      provider: "",
      model: "",
      baseUrl: "",
      apiKeyConfigured: false,
      apiKeyStored: false,
      vertexProjectId: "",
      vertexLocation: "",
      vertexCredentialsConfigured: false,
      reason: "AI status endpoint is unavailable."
    };
  } finally {
    window.clearTimeout(timeoutId);
  }
}

function providerPreset(provider) {
  return AI_PROVIDER_PRESETS[provider] ?? AI_PROVIDER_PRESETS.disabled;
}

function bindEvents() {
  els.search.addEventListener("input", () => {
    state.search = els.search.value.trim().toLowerCase();
    render();
  });

  els.file.addEventListener("change", async (event) => {
    const file = event.target.files[0];
    if (!file) return;
    const graph = JSON.parse(await file.text());
    stopAutoReload();
    loadGraph(graph, file.name);
    event.target.value = "";
  });

  els.layoutFile.addEventListener("change", async (event) => {
    const file = event.target.files[0];
    if (!file) return;
    try {
      importViewState(JSON.parse(await file.text()));
    } catch (error) {
      console.warn(error);
      window.alert("Could not import layout JSON.");
    } finally {
      event.target.value = "";
    }
  });

  els.exportLayout.addEventListener("click", exportViewState);
  els.sample.addEventListener("click", loadSampleGraph);
  els.autoReload.addEventListener("click", () => {
    if (!state.graphUrl) return;
    state.autoReload = !state.autoReload;
    syncAutoReloadButton();
    if (state.autoReload) {
      startAutoReload();
      reloadGraphFromUrl();
    } else {
      stopAutoReload(false);
    }
  });
  els.pinMode.addEventListener("click", () => {
    state.pinView = !state.pinView;
    syncPinModeButton();
  });
  els.aiSettingsButton.addEventListener("click", () => {
    state.aiSettingsOpen = !state.aiSettingsOpen;
    if (state.aiSettingsOpen) state.languageSettingsOpen = false;
    renderAiSettings();
    renderLanguageSettings();
  });
  els.aiSettingsClose.addEventListener("click", () => {
    state.aiSettingsOpen = false;
    renderAiSettings();
  });
  els.languageSettingsButton.addEventListener("click", () => {
    state.languageSettingsOpen = !state.languageSettingsOpen;
    if (state.languageSettingsOpen) state.aiSettingsOpen = false;
    renderLanguageSettings();
    renderAiSettings();
  });
  els.languageSettingsClose.addEventListener("click", () => {
    state.languageSettingsOpen = false;
    renderLanguageSettings();
  });
  els.languageOptions.querySelectorAll("[data-language]").forEach(button => {
    button.addEventListener("click", () => {
      state.language = button.dataset.language === "en" ? "en" : "ko";
      localStorage.setItem(LANGUAGE_STORAGE_KEY, state.language);
      applyLanguageSetting();
      renderLanguageSettings();
      renderDetails();
    });
  });
  els.aiProvider.addEventListener("change", () => {
    const provider = els.aiProvider.value;
    const preset = providerPreset(provider);
    state.aiConfig = {
      provider,
      baseUrl: preset.baseUrl ?? "",
      model: preset.models?.[0] ?? "",
      vertexProjectId: state.aiConfig.vertexProjectId || "",
      vertexLocation: state.aiConfig.vertexLocation || "us-central1"
    };
    state.aiModels = preset.models ?? [];
    renderAiSettings();
  });
  els.aiBaseUrl.addEventListener("input", () => {
    state.aiConfig.baseUrl = els.aiBaseUrl.value.trim();
  });
  els.aiModel.addEventListener("input", () => {
    state.aiConfig.model = els.aiModel.value.trim();
    renderAiModelList();
  });
  els.vertexProjectId.addEventListener("input", () => {
    state.aiConfig.vertexProjectId = els.vertexProjectId.value.trim();
  });
  els.vertexLocation.addEventListener("input", () => {
    state.aiConfig.vertexLocation = els.vertexLocation.value.trim();
  });
  els.vertexServiceAccountFile.addEventListener("change", async () => {
    const file = els.vertexServiceAccountFile.files?.[0];
    if (!file) return;
    const text = await file.text();
    els.vertexServiceAccount.value = text;
    fillVertexProjectFromJson(text);
    els.vertexServiceAccountFile.value = "";
  });
  els.vertexServiceAccount.addEventListener("input", () => {
    fillVertexProjectFromJson(els.vertexServiceAccount.value, false);
  });
  els.aiRefreshModels.addEventListener("click", refreshAiModels);
  els.aiSaveSettings.addEventListener("click", saveAiSettings);
  els.fit.addEventListener("click", () => fitToView());
  els.reset.addEventListener("click", () => {
    state.search = "";
    els.search.value = "";
    state.viewMode = "type";
    state.groupBy = "namespace";
    state.edgeMode = "focused";
    state.neighborhoodDepth = 2;
    state.neighborhoodDirection = "both";
    state.selectedSystemId = "";
    state.pinView = false;
    els.viewMode.value = state.viewMode;
    els.group.value = state.groupBy;
    els.edgeMode.value = state.edgeMode;
    els.neighborhoodDepth.value = String(state.neighborhoodDepth);
    els.neighborhoodDirection.value = state.neighborhoodDirection;
    els.flowDepth.value = String(state.flowDepth);
    syncPinModeButton();
    state.enabledKinds = new Set(state.edgeKinds);
    state.selected = null;
    clearSavedViewState();
    relayout();
    fitToView();
    render();
  });

  els.viewMode.addEventListener("change", () => {
    state.viewMode = els.viewMode.value;
    state.selected = null;
    state.selectedEntry = "";
    relayout();
    render();
    fitToView();
    scheduleSaveViewState();
  });

  els.group.addEventListener("change", () => {
    state.groupBy = els.group.value;
    relayout();
    render();
    fitToView();
    scheduleSaveViewState();
  });

  els.edgeMode.addEventListener("change", () => {
    state.edgeMode = els.edgeMode.value;
    render();
    scheduleSaveViewState();
  });

  els.neighborhoodDepth.addEventListener("change", () => {
    state.neighborhoodDepth = Number(els.neighborhoodDepth.value);
    render();
    if (state.edgeMode === "selected" && state.selected?.type === "node") fitToView();
    scheduleSaveViewState();
  });

  els.neighborhoodDirection.addEventListener("change", () => {
    state.neighborhoodDirection = els.neighborhoodDirection.value;
    render();
    if (state.edgeMode === "selected" && state.selected?.type === "node") fitToView();
    scheduleSaveViewState();
  });

  els.entrySelect.addEventListener("change", () => {
    state.selectedEntry = els.entrySelect.value;
    renderFlowTrace();
  });

  els.flowDepth.addEventListener("change", () => {
    state.flowDepth = Number(els.flowDepth.value);
    renderFlowTrace();
    scheduleSaveViewState();
  });

  els.aiSummary.addEventListener("click", (event) => {
    const button = event.target.closest("[data-ai-action]");
    if (!button) return;
    if (button.dataset.aiAction === "explain-node") {
      explainSelectedNode(button.dataset.force === "true");
    }
    if (button.dataset.aiAction === "explain-system") {
      explainSelectedSystem(button.dataset.force === "true");
    }
    if (button.dataset.aiAction === "workflow") {
      openWorkflowForSelection(button.dataset.force === "true");
    }
  });

  els.explainClose.addEventListener("click", closeExplainDrawer);
  els.explainExport.addEventListener("click", exportCurrentWorkflowMarkdown);
  els.explainRegenerate.addEventListener("click", () => requestWorkflowForCurrentTarget(true));
  els.explainContent.addEventListener("click", (event) => {
    const focusButton = event.target.closest("[data-workflow-focus]");
    if (focusButton) {
      focusWorkflowReference(focusButton);
      return;
    }

    const button = event.target.closest("[data-ai-action='workflow']");
    if (button) requestWorkflowForCurrentTarget(button.dataset.force === "true");
  });
  els.explainResize.addEventListener("pointerdown", startExplainResize);
  els.svg.addEventListener("wheel", onWheel, { passive: false });
  els.svg.addEventListener("pointerdown", onPointerDown);
  window.addEventListener("pointermove", onPointerMove);
  window.addEventListener("pointerup", onPointerUp);
}

function renderAiSettings() {
  els.aiSettingsPanel.hidden = !state.aiSettingsOpen;
  els.aiSettingsButton.classList.toggle("is-active", state.aiSettingsOpen);
  els.aiSettingsButton.setAttribute("aria-expanded", String(state.aiSettingsOpen));
  els.aiSettingsPanel.classList.toggle("is-vertex-provider", state.aiConfig.provider === "vertex");
  els.aiProvider.value = state.aiConfig.provider || "disabled";
  els.aiBaseUrl.value = state.aiConfig.baseUrl || "";
  els.aiModel.value = state.aiConfig.model || "";
  els.vertexProjectId.value = state.aiConfig.vertexProjectId || "";
  els.vertexLocation.value = state.aiConfig.vertexLocation || "us-central1";
  els.aiSaveApiKey.checked = state.aiStatus.apiKeyStored || els.aiSaveApiKey.checked;
  els.aiSettingsStatus.textContent = aiSettingsStatusText();
  renderAiModelList();
}

function renderLanguageSettings() {
  els.languageSettingsPanel.hidden = !state.languageSettingsOpen;
  els.languageSettingsButton.classList.toggle("is-active", state.languageSettingsOpen);
  els.languageSettingsButton.setAttribute("aria-expanded", String(state.languageSettingsOpen));
  els.languageOptions.querySelectorAll("[data-language]").forEach(button => {
    const selected = button.dataset.language === state.language;
    button.classList.toggle("is-selected", selected);
    button.setAttribute("aria-pressed", String(selected));
  });
  els.languageStatus.textContent = state.language === "ko"
    ? "AI summaries will be requested in Korean."
    : "AI summaries will be requested in English.";
}

function renderAiModelList() {
  const models = uniqueModels([state.aiConfig.model, ...state.aiModels, ...(providerPreset(state.aiConfig.provider).models ?? [])]);
  els.aiModelList.innerHTML = models.length
    ? models.map(model => `
        <button class="${model === state.aiConfig.model ? "is-selected" : ""}" type="button" data-model="${escapeHtml(model)}">
          ${escapeHtml(model)}
        </button>
      `).join("")
    : "<p class=\"muted-text\">Choose a provider to see model suggestions.</p>";

  els.aiModelList.querySelectorAll("[data-model]").forEach(button => {
    button.addEventListener("click", () => {
      state.aiConfig.model = button.dataset.model || "";
      els.aiModel.value = state.aiConfig.model;
      renderAiModelList();
    });
  });
}

function aiSettingsStatusText() {
  if (!state.aiStatus.checked) return "Checking AI status.";
  if (state.aiStatus.enabled) {
    const secretName = state.aiStatus.provider === "vertex" ? "Vertex credentials" : "API key";
    const keyState = state.aiStatus.apiKeyStored
      ? `${secretName} are remembered in this Windows user profile.`
      : `${secretName} are active for this launcher session.`;
    return `${state.aiStatus.provider || "AI"} / ${state.aiStatus.model || "model"} is ready. ${keyState}`;
  }
  return state.aiStatus.reason || "AI is not configured.";
}

async function refreshAiModels() {
  els.aiSettingsStatus.textContent = "Refreshing model list...";
  await saveAiSettings({ quiet: true });
  try {
    const response = await fetch("/ai/models", { cache: "no-store" });
    if (!response.ok) throw new Error(`Model list unavailable (${response.status})`);
    const data = await response.json();
    state.aiModels = uniqueModels([state.aiConfig.model, ...(data.models ?? []), ...(providerPreset(state.aiConfig.provider).models ?? [])]);
    els.aiSettingsStatus.textContent = `${state.aiModels.length} model candidates loaded.`;
  } catch (error) {
    state.aiModels = uniqueModels([state.aiConfig.model, ...(providerPreset(state.aiConfig.provider).models ?? [])]);
    els.aiSettingsStatus.textContent = error instanceof Error ? error.message : "Model refresh failed.";
  }
  renderAiSettings();
}

async function saveAiSettings(options = {}) {
  state.aiConfig = {
    provider: els.aiProvider.value,
    baseUrl: els.aiBaseUrl.value.trim(),
    model: els.aiModel.value.trim(),
    vertexProjectId: els.vertexProjectId.value.trim(),
    vertexLocation: els.vertexLocation.value.trim()
  };

  localStorage.setItem(AI_CONFIG_STORAGE_KEY, JSON.stringify(state.aiConfig));
  if (!window.location.protocol.startsWith("http")) {
    if (!options.quiet) {
      els.aiSettingsStatus.textContent = "Open through the launcher to apply AI settings.";
    }
    return;
  }

  const payload = {
    ...state.aiConfig,
    saveApiKey: Boolean(els.aiSaveApiKey.checked)
  };
  if (els.aiApiKey.value.trim()) {
    payload.apiKey = els.aiApiKey.value.trim();
  }
  if (state.aiConfig.provider === "vertex" && els.vertexServiceAccount.value.trim()) {
    payload.vertexServiceAccountJson = els.vertexServiceAccount.value.trim();
  }

  try {
    const response = await fetch("/ai/config", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    const data = await response.json();
    if (!response.ok) throw new Error(data.error || `AI config failed (${response.status})`);
    els.aiApiKey.value = "";
    els.vertexServiceAccount.value = "";
    await loadAiStatus();
    state.aiModels = uniqueModels([state.aiConfig.model, ...(data.modelSuggestions ?? []), ...state.aiModels]);
    if (!options.quiet) {
      els.aiSettingsStatus.textContent = data.enabled
        ? `${data.provider} / ${data.model} is ready.${data.saved ? " Secret remembered." : ""}`
        : data.reason || "AI settings saved, but provider is not ready.";
    }
    renderDetails();
  } catch (error) {
    if (!options.quiet) {
      els.aiSettingsStatus.textContent = error instanceof Error ? error.message : "AI settings failed.";
    }
  }
}

function uniqueModels(models) {
  return [...new Set(models.map(model => String(model ?? "").trim()).filter(Boolean))];
}

function fillVertexProjectFromJson(text, overwrite = true) {
  if (!text.trim()) return;
  try {
    const parsed = JSON.parse(text);
    if (parsed?.project_id && (overwrite || !els.vertexProjectId.value.trim())) {
      els.vertexProjectId.value = parsed.project_id;
      state.aiConfig.vertexProjectId = parsed.project_id;
    }
  } catch {
    // Keep free-form paste editing quiet until Save AI validates it server-side.
  }
}

async function loadSampleGraph() {
  stopAutoReload();
  try {
    const response = await fetch(SAMPLE_URL);
    if (!response.ok) throw new Error("sample unavailable");
    loadGraph(await response.json(), "samples/mini-graph.json");
  } catch {
    loadGraph(sampleGraph, "embedded sample");
  }
}

function loadGraph(graph, label, options = {}) {
  const previousViewState = options.preserveView && state.graph ? createViewStateData() : null;
  if (previousViewState) {
    previousViewState.edgeMode = state.edgeMode;
  }
  const previousSelected = options.preserveView ? state.selected : null;
  if (!options.preserveView) {
    document.body.classList.remove("graph-loaded");
  }
  const normalized = {
    ...graph,
    Nodes: graph.Nodes ?? graph.nodes ?? [],
    Edges: graph.Edges ?? graph.edges ?? [],
    Methods: graph.Methods ?? graph.methods ?? [],
    MethodEdges: graph.MethodEdges ?? graph.methodEdges ?? [],
    SystemClusters: graph.SystemClusters ?? graph.systemClusters ?? []
  };
  state.graph = normalized;
  state.graphUrl = options.sourceUrl ?? state.graphUrl;
  state.graphLabel = label;
  state.graphFingerprint = options.fingerprint ?? state.graphFingerprint;
  state.storageKey = graphStorageKey(normalized);
  state.edgeKinds = new Set(normalized.Edges.map(edge => edge.Kind));
  state.enabledKinds = new Set(state.edgeKinds);
  state.search = "";
  state.viewMode = "type";
  state.groupBy = "namespace";
  state.edgeMode = "focused";
  state.neighborhoodDepth = 2;
  state.neighborhoodDirection = "both";
  state.flowDepth = 3;
  state.selectedSystemId = "";
  state.transform = { x: 40, y: 40, scale: 1 };
  const savedViewState = previousViewState ?? readSavedViewState();
  const restored = restoreViewState(savedViewState);
  const layout = layoutGraph(normalized, state.groupBy);
  state.positions = layout.positions;
  state.sections = layout.sections;
  if (restored) {
    restoreSavedPositions(savedViewState);
  }
  state.selected = restoreSelection(previousSelected, normalized);
  state.pinView = false;
  syncGraphControls();
  syncPinModeButton();
  syncAutoReloadButton();
  updateSubtitle();
  renderFilters();
  renderSystems();
  render();
  requestAnimationFrame(() => document.body.classList.add("graph-loaded"));
  if (!restored && !options.preserveView) {
    requestAnimationFrame(() => fitToView());
  }
}

async function loadInitialGraph() {
  const graphUrl = new URLSearchParams(window.location.search).get("graph");
  if (!graphUrl) {
    await loadSampleGraph();
    return;
  }

  try {
    const result = await fetchGraphJson(graphUrl);
    const label = graphUrl.endsWith("/graph/current.json") ? "launcher output" : graphUrl;
    state.autoReload = true;
    loadGraph(result.graph, label, {
      sourceUrl: graphUrl,
      fingerprint: result.fingerprint
    });
    startAutoReload();
  } catch (error) {
    console.warn(error);
    await loadSampleGraph();
  }
}

async function fetchGraphJson(url) {
  const response = await fetch(cacheBustedUrl(url), { cache: "no-store" });
  if (!response.ok) throw new Error(`graph unavailable: ${response.status}`);
  const text = await response.text();
  return {
    graph: JSON.parse(text),
    fingerprint: hashString(text)
  };
}

function cacheBustedUrl(url) {
  const parsed = new URL(url, window.location.href);
  parsed.searchParams.set("_ucg", String(Date.now()));
  return parsed.toString();
}

function startAutoReload() {
  window.clearInterval(state.reloadTimer);
  if (!state.graphUrl || !state.autoReload) {
    syncAutoReloadButton();
    return;
  }

  state.reloadTimer = window.setInterval(reloadGraphFromUrl, AUTO_RELOAD_INTERVAL_MS);
  syncAutoReloadButton();
}

function stopAutoReload(clearSource = true) {
  window.clearInterval(state.reloadTimer);
  state.reloadTimer = 0;
  state.reloadBusy = false;
  state.autoReload = false;
  if (clearSource) {
    state.graphUrl = "";
    state.graphFingerprint = "";
    state.lastReloadAt = "";
  }
  syncAutoReloadButton();
}

async function reloadGraphFromUrl() {
  if (!state.graphUrl || state.reloadBusy) return;
  state.reloadBusy = true;
  syncAutoReloadButton();

  try {
    const result = await fetchGraphJson(state.graphUrl);
    if (result.fingerprint !== state.graphFingerprint) {
      state.lastReloadAt = new Date().toLocaleTimeString();
      loadGraph(result.graph, state.graphLabel || state.graphUrl, {
        sourceUrl: state.graphUrl,
        fingerprint: result.fingerprint,
        preserveView: true
      });
    }
  } catch (error) {
    console.warn(error);
  } finally {
    state.reloadBusy = false;
    syncAutoReloadButton();
  }
}

function relayout() {
  if (!state.graph) return;
  const layout = layoutGraph(state.graph, state.groupBy);
  state.positions = layout.positions;
  state.sections = layout.sections;
}

function graphStorageKey(graph) {
  const nodes = (graph.Nodes ?? [])
    .map(node => `${node.Id}|${node.File ?? ""}|${node.Line ?? ""}`)
    .sort()
    .join("\n");
  const signature = `${graph.RootPath ?? ""}\n${nodes}\n${(graph.Edges ?? []).length}`;
  return `${VIEW_STATE_PREFIX}${hashString(signature)}`;
}

function readSavedViewState() {
  if (!state.storageKey) return null;
  try {
    const raw = localStorage.getItem(state.storageKey);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

function restoreViewState(saved) {
  if (!saved || saved.version !== VIEW_STATE_VERSION) {
    return false;
  }

  state.viewMode = validOption(els.viewMode, saved.viewMode, state.viewMode);
  state.groupBy = validOption(els.group, saved.groupBy, state.groupBy);
  state.edgeMode = validOption(els.edgeMode, saved.edgeMode, state.edgeMode);
  if (state.edgeMode === "selected") {
    state.edgeMode = "focused";
  }
  state.neighborhoodDepth = clamp(Number(saved.neighborhoodDepth) || state.neighborhoodDepth, 1, 4);
  state.neighborhoodDirection = validOption(els.neighborhoodDirection, saved.neighborhoodDirection, state.neighborhoodDirection);
  state.flowDepth = clamp(Number(saved.flowDepth) || state.flowDepth, 2, 5);

  if (Array.isArray(saved.enabledKinds)) {
    state.enabledKinds = new Set(saved.enabledKinds.filter(kind => state.edgeKinds.has(kind)));
  }

  const clusterIds = new Set((state.graph?.SystemClusters ?? []).map(cluster => cluster.Id));
  state.selectedSystemId = clusterIds.has(saved.selectedSystemId) ? saved.selectedSystemId : "";

  if (isFiniteNumber(saved.transform?.x)
      && isFiniteNumber(saved.transform?.y)
      && isFiniteNumber(saved.transform?.scale)) {
    state.transform = {
      x: saved.transform.x,
      y: saved.transform.y,
      scale: clamp(saved.transform.scale, 0.18, 2.5)
    };
  }

  return true;
}

function restoreSavedPositions(saved) {
  if (!saved?.positions || typeof saved.positions !== "object") {
    return;
  }

  const nodeIds = new Set((state.graph?.Nodes ?? []).map(node => node.Id));
  for (const [id, position] of Object.entries(saved.positions)) {
    if (!nodeIds.has(id) || !isFiniteNumber(position?.x) || !isFiniteNumber(position?.y)) {
      continue;
    }

    state.positions.set(id, { x: position.x, y: position.y });
  }
}

function syncGraphControls() {
  els.search.value = state.search;
  els.viewMode.value = state.viewMode;
  els.group.value = state.groupBy;
  els.edgeMode.value = state.edgeMode;
  els.neighborhoodDepth.value = String(state.neighborhoodDepth);
  els.neighborhoodDirection.value = state.neighborhoodDirection;
  els.flowDepth.value = String(state.flowDepth);
}

function syncAutoReloadButton() {
  els.autoReload.disabled = !state.graphUrl;
  els.autoReload.classList.toggle("is-active", Boolean(state.graphUrl && state.autoReload));
  els.autoReload.setAttribute("aria-pressed", String(Boolean(state.graphUrl && state.autoReload)));
  els.autoReload.textContent = state.reloadBusy ? "Checking" : state.autoReload ? "Auto On" : "Auto Reload";
}

function updateSubtitle() {
  if (!state.graph) return;
  const reloadText = state.graphUrl && state.autoReload
    ? ` · auto reload${state.lastReloadAt ? ` ${state.lastReloadAt}` : ""}`
    : "";
  els.subtitle.textContent = `${state.graphLabel} · ${state.graph.Nodes.length} types · ${state.graph.Edges.length} relationships${reloadText}`;
}

function restoreSelection(previous, graph) {
  if (!previous) return null;
  if (previous.type === "node" && graph.Nodes.some(node => node.Id === previous.id)) {
    return previous;
  }

  if (previous.type === "system" && (graph.SystemClusters ?? []).some(cluster => cluster.Id === previous.id)) {
    state.selectedSystemId = previous.id;
    return previous;
  }

  if (previous.type === "edge" && graph.Edges.some(edge => edgeKey(edge) === previous.key)) {
    return previous;
  }

  return null;
}

function scheduleSaveViewState() {
  if (!state.storageKey) return;
  window.clearTimeout(state.saveTimer);
  state.saveTimer = window.setTimeout(saveViewState, 180);
}

function saveViewState() {
  if (!state.storageKey || !state.graph) return;

  const data = createViewStateData();
  try {
    localStorage.setItem(state.storageKey, JSON.stringify(data));
  } catch {
    // Persisting view state is a convenience feature; rendering should never depend on it.
  }
}

function createViewStateData() {
  const positions = {};
  for (const [id, position] of state.positions) {
    positions[id] = {
      x: Math.round(position.x * 100) / 100,
      y: Math.round(position.y * 100) / 100
    };
  }

  const data = {
    version: VIEW_STATE_VERSION,
    savedAt: new Date().toISOString(),
    viewMode: state.viewMode,
    groupBy: state.groupBy,
    edgeMode: state.edgeMode === "selected" ? "focused" : state.edgeMode,
    neighborhoodDepth: state.neighborhoodDepth,
    neighborhoodDirection: state.neighborhoodDirection,
    flowDepth: state.flowDepth,
    selectedSystemId: state.selectedSystemId,
    enabledKinds: [...state.enabledKinds],
    transform: {
      x: Math.round(state.transform.x * 100) / 100,
      y: Math.round(state.transform.y * 100) / 100,
      scale: Math.round(state.transform.scale * 1000) / 1000
    },
    positions
  };
}

function exportViewState() {
  if (!state.graph) return;

  const data = {
    ...createViewStateData(),
    exportedAt: new Date().toISOString(),
    graph: {
      rootPath: state.graph.RootPath ?? "",
      nodeCount: state.graph.Nodes?.length ?? 0,
      edgeCount: state.graph.Edges?.length ?? 0,
      storageKey: state.storageKey
    }
  };

  const label = safeFileName(state.graph.RootPath || "code-graph");
  const blob = new Blob([JSON.stringify(data, null, 2)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = `${label}-layout.json`;
  document.body.append(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

function importViewState(data) {
  if (!state.graph) return;
  if (!data || data.version !== VIEW_STATE_VERSION) {
    window.alert("This layout file uses an unsupported format.");
    return;
  }

  const restoredPositions = countRestorablePositions(data);
  if (data.positions && restoredPositions === 0) {
    window.alert("No nodes in this layout file match the current graph.");
    return;
  }

  const baseLayout = layoutGraph(state.graph, validOption(els.group, data.groupBy, state.groupBy));
  state.positions = baseLayout.positions;
  state.sections = baseLayout.sections;
  const restored = restoreViewState(data);
  restoreSavedPositions(data);
  syncGraphControls();
  renderFilters();
  renderSystems();
  render();
  if (!restored) {
    fitToView();
  }
  scheduleSaveViewState();
}

function safeFileName(value) {
  const name = String(value)
    .replaceAll("\\", "/")
    .split("/")
    .filter(Boolean)
    .pop() || "code-graph";

  return name
    .replace(/[^a-z0-9._-]+/gi, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 80) || "code-graph";
}

function countRestorablePositions(saved) {
  if (!saved?.positions || typeof saved.positions !== "object") {
    return 0;
  }

  const nodeIds = new Set((state.graph?.Nodes ?? []).map(node => node.Id));
  return Object.keys(saved.positions).filter(id => nodeIds.has(id)).length;
}

function clearSavedViewState() {
  if (!state.storageKey) return;
  window.clearTimeout(state.saveTimer);
  try {
    localStorage.removeItem(state.storageKey);
  } catch {
    // Ignore unavailable storage.
  }
}

function layoutGraph(graph, groupBy) {
  const positions = new Map();
  const sections = new Map();
  const ids = new Set(graph.Nodes.map(node => node.Id));
  const incoming = new Map(graph.Nodes.map(node => [node.Id, 0]));
  const outgoing = new Map(graph.Nodes.map(node => [node.Id, []]));
  const degree = new Map(graph.Nodes.map(node => [node.Id, 0]));

  for (const edge of graph.Edges) {
    if (!ids.has(edge.Source) || !ids.has(edge.Target)) continue;
    incoming.set(edge.Target, (incoming.get(edge.Target) ?? 0) + 1);
    outgoing.get(edge.Source).push(edge.Target);
    degree.set(edge.Source, (degree.get(edge.Source) ?? 0) + 1);
    degree.set(edge.Target, (degree.get(edge.Target) ?? 0) + 1);
  }

  const groups = groupNodes(graph.Nodes, groupBy);
  const orderedGroups = [...groups.entries()]
    .sort((a, b) => sectionWeight(b[1], degree) - sectionWeight(a[1], degree) || a[0].localeCompare(b[0]));

  let x = 0;
  let y = 0;
  let rowHeight = 0;
  for (const [sectionName, nodes] of orderedGroups) {
    const sorted = [...nodes].sort((a, b) =>
      (degree.get(b.Id) ?? 0) - (degree.get(a.Id) ?? 0) ||
      (outgoing.get(b.Id)?.length ?? 0) - (outgoing.get(a.Id)?.length ?? 0) ||
      a.Name.localeCompare(b.Name)
    );
    const columns = sectionColumns(sorted.length);
    const rows = Math.ceil(sorted.length / columns);
    const width = SECTION_PADDING * 2 + columns * NODE_WIDTH + (columns - 1) * NODE_GAP_X;
    const height = SECTION_HEADER + SECTION_PADDING + rows * NODE_HEIGHT + Math.max(0, rows - 1) * NODE_GAP_Y;

    if (x > 0 && x + width > SECTION_ROW_WIDTH) {
      x = 0;
      y += rowHeight + SECTION_GAP;
      rowHeight = 0;
    }

    sections.set(sectionName, {
      x,
      y,
      width,
      height,
      count: sorted.length,
      columns,
      rows
    });
    sorted.forEach((node, index) => {
      const col = index % columns;
      const row = Math.floor(index / columns);
      positions.set(node.Id, {
        x: x + SECTION_PADDING + col * (NODE_WIDTH + NODE_GAP_X),
        y: y + SECTION_HEADER + row * (NODE_HEIGHT + NODE_GAP_Y)
      });
    });
    x += width + SECTION_GAP;
    rowHeight = Math.max(rowHeight, height);
  }

  return { positions, sections };
}

function sectionColumns(count) {
  if (count <= 1) return 1;
  return clamp(Math.ceil(Math.sqrt(count * 2.2)), 1, 10);
}

function groupNodes(nodes, groupBy) {
  const groups = new Map();
  for (const node of nodes) {
    const key = sectionKey(node, groupBy);
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key).push(node);
  }
  return groups;
}

function sectionKey(node, groupBy) {
  if (groupBy === "kind") return node.Kind ?? "type";
  if (groupBy === "unity") return node.IsUnityType ? "Unity types" : "Plain C#";
  if (groupBy === "folder") {
    const file = String(node.File ?? "").replaceAll("\\", "/");
    const parts = file.split("/");
    const scriptsIndex = parts.findIndex(part => part.toLowerCase() === "scripts");
    if (scriptsIndex >= 0 && parts[scriptsIndex + 1]) return parts[scriptsIndex + 1];
    return parts.at(-2) ?? "Source";
  }
  const namespace = node.Namespace || "global";
  const parts = namespace.split(".");
  if (parts.length <= 2) return namespace;
  return `${parts[0]}.${parts[1]}`;
}

function sectionWeight(nodes, degree) {
  return nodes.reduce((sum, node) => sum + (degree.get(node.Id) ?? 0), 0) + nodes.length;
}

function renderFilters() {
  const counts = countBy(state.graph.Edges, edge => edge.Kind);
  els.edgeFilters.innerHTML = "";
  for (const kind of [...state.edgeKinds].sort()) {
    const item = document.createElement("div");
    item.className = "filter-item";
    item.innerHTML = `
      <label>
        <input type="checkbox" ${state.enabledKinds.has(kind) ? "checked" : ""} data-kind="${escapeHtml(kind)}" />
        <span>${formatKind(kind)}</span>
      </label>
      <span class="filter-count">${counts.get(kind) ?? 0}</span>
    `;
    item.querySelector("input").addEventListener("change", (event) => {
      if (event.target.checked) state.enabledKinds.add(kind);
      else state.enabledKinds.delete(kind);
      render();
      scheduleSaveViewState();
    });
    els.edgeFilters.appendChild(item);
  }
}

function renderSystems() {
  const clusters = state.graph.SystemClusters ?? [];
  if (!clusters.length) {
    els.systemList.innerHTML = "<p class=\"muted-text\">No system clusters found.</p>";
    return;
  }

  els.systemList.innerHTML = `
    <button class="system-item${state.selectedSystemId ? "" : " is-selected"}" type="button" data-system-id="">
      <strong>All Types</strong>
      <span>${state.graph.Nodes.length} types / full graph</span>
    </button>
  ` + clusters
    .slice(0, 18)
    .map(cluster => `
      <button class="system-item${cluster.Id === state.selectedSystemId ? " is-selected" : ""}" type="button" data-system-id="${escapeHtml(cluster.Id)}">
        <strong>${escapeHtml(cluster.Name)}</strong>
        <span>${cluster.NodeIds?.length ?? 0} types / ${cluster.InternalEdges ?? 0} internal</span>
      </button>
    `)
    .join("");

  for (const button of els.systemList.querySelectorAll("[data-system-id]")) {
    button.addEventListener("click", () => {
      state.selectedSystemId = button.dataset.systemId;
      state.selected = state.selectedSystemId ? { type: "system", id: state.selectedSystemId } : null;
      if (state.selectedSystemId) {
        state.edgeMode = "all";
        els.edgeMode.value = state.edgeMode;
      }
      relayout();
      renderSystems();
      render();
      requestAnimationFrame(() => fitToView());
      scheduleSaveViewState();
    });
  }
}

function render() {
  if (!state.graph) return;
  if (state.viewMode === "system") {
    renderSystemView();
    return;
  }

  const visibleNodes = getVisibleNodes();
  const visibleIds = new Set(visibleNodes.map(node => node.Id));
  const visibleEdges = filterEdges(state.graph.Edges.filter(edge =>
    state.enabledKinds.has(edge.Kind) && visibleIds.has(edge.Source) && visibleIds.has(edge.Target)
  ));

  els.nodeCount.textContent = visibleNodes.length;
  els.edgeCount.textContent = visibleEdges.length;
  els.unityCount.textContent = visibleNodes.filter(node => node.IsUnityType).length;
  setEmptyState(visibleNodes.length > 0, state.search
    ? "No types found"
    : "No type nodes", state.search
      ? "Clear the search or try a namespace, class, or system word."
      : "Load a generated graph JSON or reload the sample graph.");

  applyTransform();
  renderSections(visibleNodes);
  renderEdges(visibleEdges);
  renderNodes(visibleNodes);
  renderDetails();
}

function renderSystemView() {
  const clusters = filteredSystemClusters();
  const positions = layoutSystems(clusters);
  const edges = buildSystemEdges(clusters);

  els.nodeCount.textContent = clusters.length;
  els.edgeCount.textContent = edges.length;
  els.unityCount.textContent = "-";
  setEmptyState(clusters.length > 0, state.search
    ? "No systems found"
    : "No system clusters", state.search
      ? "Clear the search or switch back to Type View."
      : "Load a larger graph or use Type View for individual nodes.");

  applyTransform();
  clearSections();
  renderSystemEdges(edges, positions);
  renderSystemNodes(clusters, positions);
  renderDetails();
}

function setEmptyState(hasResults, title, message) {
  els.empty.hidden = hasResults;
  if (hasResults) return;
  els.emptyTitle.textContent = title;
  els.emptyMessage.textContent = message;
}

function filteredSystemClusters() {
  const clusters = state.graph.SystemClusters ?? [];
  if (!state.search) return clusters;
  return clusters.filter(cluster =>
    cluster.Name.toLowerCase().includes(state.search) ||
    String(cluster.Id).toLowerCase().includes(state.search) ||
    (cluster.Keywords ?? []).some(keyword => String(keyword).toLowerCase().includes(state.search))
  );
}

function layoutSystems(clusters) {
  const positions = new Map();
  const columns = Math.max(1, Math.ceil(Math.sqrt(clusters.length * 1.6)));
  clusters.forEach((cluster, index) => {
    const col = index % columns;
    const row = Math.floor(index / columns);
    positions.set(cluster.Id, {
      x: col * (SYSTEM_WIDTH + SYSTEM_GAP_X),
      y: row * (SYSTEM_HEIGHT + SYSTEM_GAP_Y)
    });
  });
  return positions;
}

function buildSystemEdges(clusters) {
  const clusterByNode = new Map();
  for (const cluster of clusters) {
    for (const nodeId of cluster.NodeIds ?? []) {
      clusterByNode.set(nodeId, cluster.Id);
    }
  }

  const edges = new Map();
  for (const edge of state.graph.Edges) {
    const source = clusterByNode.get(edge.Source);
    const target = clusterByNode.get(edge.Target);
    if (!source || !target || source === target) continue;
    const key = `${source}|${target}`;
    const current = edges.get(key) ?? { Source: source, Target: target, Weight: 0, Kinds: new Set() };
    current.Weight += edge.Weight ?? 1;
    current.Kinds.add(edge.Kind);
    edges.set(key, current);
  }

  return [...edges.values()]
    .sort((a, b) => b.Weight - a.Weight || a.Source.localeCompare(b.Source));
}

function renderSystemEdges(edges, positions) {
  els.edges.innerHTML = "";
  const selectedId = state.selected?.type === "system" ? state.selected.id : "";
  const visibleEdges = selectedId
    ? edges.filter(edge => edge.Source === selectedId || edge.Target === selectedId)
    : state.edgeMode === "all" ? edges : [];

  for (const edge of visibleEdges) {
    const source = systemCenter(edge.Source, positions);
    const target = systemCenter(edge.Target, positions);
    if (!source || !target) continue;
    const path = document.createElementNS("http://www.w3.org/2000/svg", "path");
    path.setAttribute("class", `edge system-edge ${selectedId ? "is-selected" : ""}`);
    path.setAttribute("d", edgePath(source, target, { Source: edge.Source, Target: edge.Target, Kind: "system" }));
    path.dataset.edgeKey = `${edge.Source}|${edge.Target}|system`;
    els.edges.appendChild(path);
  }
}

function renderSystemNodes(clusters, positions) {
  els.nodes.innerHTML = "";
  for (const cluster of clusters) {
    const p = positions.get(cluster.Id) ?? { x: 0, y: 0 };
    const selected = state.selected?.type === "system" && state.selected.id === cluster.Id;
    const group = document.createElementNS("http://www.w3.org/2000/svg", "g");
    group.setAttribute("class", `system-node ${selected ? "is-selected" : ""}`);
    group.setAttribute("transform", `translate(${p.x}, ${p.y})`);
    group.dataset.systemId = cluster.Id;
    group.innerHTML = `
      <rect class="system-node-rect" width="${SYSTEM_WIDTH}" height="${SYSTEM_HEIGHT}" rx="8"></rect>
      <text class="system-node-title" x="16" y="30">${escapeHtml(cluster.Name)}</text>
      <text class="system-node-subtitle" x="16" y="54">${cluster.NodeIds?.length ?? 0} types / ${cluster.InternalEdges ?? 0} internal edges</text>
      <text class="system-node-subtitle" x="16" y="76">${cluster.ExternalEdges ?? 0} external refs / score ${cluster.Score ?? 0}</text>
      <text class="system-node-keywords" x="16" y="106">${escapeHtml((cluster.Keywords ?? []).slice(0, 4).join(", "))}</text>
    `;
    group.addEventListener("click", (event) => {
      event.stopPropagation();
      state.selectedSystemId = cluster.Id;
      state.selected = { type: "system", id: cluster.Id };
      renderSystems();
      render();
    });
    els.nodes.appendChild(group);
  }
}

function systemCenter(id, positions) {
  const p = positions.get(id);
  if (!p) return null;
  return { x: p.x + SYSTEM_WIDTH / 2, y: p.y + SYSTEM_HEIGHT / 2 };
}

function clearSections() {
  const layer = document.getElementById("sectionsLayer");
  if (layer) layer.innerHTML = "";
}

function getVisibleNodes() {
  let nodes = state.graph.Nodes;
  const system = selectedSystem();
  if (system) {
    const systemIds = new Set(system.NodeIds ?? []);
    nodes = nodes.filter(node => systemIds.has(node.Id));
  }
  const neighborhood = selectedNeighborhoodIds();
  if (neighborhood) {
    nodes = nodes.filter(node => neighborhood.has(node.Id));
  }
  if (!state.search) return nodes;
  return nodes.filter(node =>
    node.Name.toLowerCase().includes(state.search) ||
    node.Namespace.toLowerCase().includes(state.search) ||
    node.Id.toLowerCase().includes(state.search)
  );
}

function filterEdges(edges) {
  if (state.edgeMode === "focused") {
    if (state.selected?.type === "node") {
      const id = state.selected.id;
      return edges.filter(edge => edge.Source === id || edge.Target === id);
    }
    if (state.selected?.type === "system") {
      const system = selectedSystem();
      const ids = new Set(system?.NodeIds ?? []);
      return edges.filter(edge => ids.has(edge.Source) && ids.has(edge.Target));
    }
    return [];
  }
  if (state.edgeMode === "structural") {
    const structural = new Set(["inherits", "implements", "has_field_type", "has_property_type", "has_event_type"]);
    return edges.filter(edge => structural.has(edge.Kind));
  }
  if (state.edgeMode === "selected" && state.selected?.type === "node") {
    const neighborhood = selectedNeighborhoodIds();
    if (!neighborhood) return edges;
    return edges.filter(edge => neighborhood.has(edge.Source) && neighborhood.has(edge.Target));
  }
  return edges;
}

function selectedNeighborhoodIds() {
  if (state.edgeMode !== "selected" || state.selected?.type !== "node") return null;
  const rootId = state.selected.id;
  const ids = new Set(state.graph.Nodes.map(node => node.Id));
  if (!ids.has(rootId)) return null;

  const adjacency = buildNeighborhoodAdjacency();
  const visited = new Set([rootId]);
  let frontier = new Set([rootId]);
  const maxDepth = clamp(Number(state.neighborhoodDepth) || 1, 1, 4);

  for (let depth = 0; depth < maxDepth; depth++) {
    const next = new Set();
    for (const id of frontier) {
      for (const neighbor of adjacency.get(id) ?? []) {
        if (visited.has(neighbor)) continue;
        visited.add(neighbor);
        next.add(neighbor);
      }
    }
    if (!next.size) break;
    frontier = next;
  }

  return visited;
}

function buildNeighborhoodAdjacency() {
  const adjacency = new Map(state.graph.Nodes.map(node => [node.Id, new Set()]));
  const direction = state.neighborhoodDirection;
  for (const edge of state.graph.Edges) {
    if (!state.enabledKinds.has(edge.Kind)) continue;
    if (!adjacency.has(edge.Source) || !adjacency.has(edge.Target)) continue;
    if (direction === "out" || direction === "both") adjacency.get(edge.Source).add(edge.Target);
    if (direction === "in" || direction === "both") adjacency.get(edge.Target).add(edge.Source);
  }
  return adjacency;
}

function renderSections(visibleNodes) {
  let layer = document.getElementById("sectionsLayer");
  if (!layer) {
    layer = document.createElementNS("http://www.w3.org/2000/svg", "g");
    layer.setAttribute("id", "sectionsLayer");
    els.viewport.insertBefore(layer, els.edges);
  }
  layer.innerHTML = "";

  const visibleIds = new Set(visibleNodes.map(node => node.Id));
  for (const [name, section] of state.sections) {
    const sectionNodes = state.graph.Nodes.filter(node => sectionKey(node, state.groupBy) === name && visibleIds.has(node.Id));
    if (!sectionNodes.length) continue;
    const rect = document.createElementNS("http://www.w3.org/2000/svg", "rect");
    rect.setAttribute("class", "section-band");
    rect.setAttribute("x", section.x);
    rect.setAttribute("y", section.y);
    rect.setAttribute("width", section.width);
    rect.setAttribute("height", section.height);
    rect.setAttribute("rx", 14);
    layer.appendChild(rect);

    const label = document.createElementNS("http://www.w3.org/2000/svg", "text");
    label.setAttribute("class", "section-label");
    label.setAttribute("x", section.x + 16);
    label.setAttribute("y", section.y + 32);
    label.textContent = `${name} · ${sectionNodes.length}`;
    layer.appendChild(label);
  }
}

function renderEdges(edges) {
  els.edges.innerHTML = "";
  for (const edge of edges) {
    const source = centerOf(edge.Source);
    const target = centerOf(edge.Target);
    if (!source || !target) continue;

    const path = document.createElementNS("http://www.w3.org/2000/svg", "path");
    path.setAttribute("class", `edge ${isSelected(edge) ? "is-selected" : ""} ${isWorkflowFocusedEdge(edge) ? "is-workflow-focus" : ""}`);
    path.setAttribute("d", edgePath(source, target, edge));
    path.dataset.edgeKey = edgeKey(edge);
    path.addEventListener("click", (event) => {
      event.stopPropagation();
      if (state.pinView && state.selected) return;
      state.workflowFocus = null;
      state.selected = { type: "edge", key: edgeKey(edge) };
      render();
    });
    els.edges.appendChild(path);
  }
}

function renderNodes(nodes) {
  els.nodes.innerHTML = "";
  for (const node of nodes) {
    const position = state.positions.get(node.Id) ?? { x: 0, y: 0 };
    const group = document.createElementNS("http://www.w3.org/2000/svg", "g");
    const selected = state.selected?.type === "node" && state.selected.id === node.Id;
    const workflowFocus = isWorkflowFocusedNode(node.Id);
    group.setAttribute("class", `node ${selected ? "is-selected" : ""} ${workflowFocus ? "is-workflow-focus" : ""}`);
    group.setAttribute("transform", `translate(${position.x}, ${position.y})`);
    group.dataset.nodeId = node.Id;

    group.innerHTML = `
      <rect class="node-rect" width="${NODE_WIDTH}" height="${NODE_HEIGHT}" rx="8"></rect>
      <rect class="${accentClass(node)}" width="6" height="${NODE_HEIGHT}" rx="3"></rect>
      <text class="node-title" x="18" y="30">${escapeHtml(node.Name)}</text>
      <text class="node-subtitle" x="18" y="50">${escapeHtml(shortNamespace(node.Namespace))}</text>
      <rect class="node-chip" x="18" y="64" width="${chipWidth(node)}" height="20" rx="6"></rect>
      <text class="node-chip-text" x="28" y="78">${escapeHtml(node.IsUnityType ? "Unity" : node.Kind)}</text>
    `;

    group.addEventListener("pointerdown", (event) => {
      event.stopPropagation();
      const point = screenToWorld(event);
      state.drag = { id: node.Id, dx: point.x - position.x, dy: point.y - position.y };
      selectNodeForInteraction(node.Id);
      render();
    });

    group.addEventListener("click", (event) => {
      event.stopPropagation();
      selectNodeForInteraction(node.Id);
      render();
    });

    els.nodes.appendChild(group);
  }
}

function renderDetails() {
  if (!state.selected) {
    els.detailTitle.textContent = "No Selection";
    els.detailSubtitle.textContent = "Select a type or relationship on the canvas.";
    els.detailList.innerHTML = "";
    els.secondaryTitle.textContent = "Examples";
    els.examples.innerHTML = "";
    renderAiSummary();
    renderFlowTrace();
    return;
  }

  if (state.selected.type === "system") {
    const cluster = selectedSystem();
    if (!cluster) return;
    els.detailTitle.textContent = cluster.Name;
    els.detailSubtitle.textContent = "System Cluster";
    setDetailRows([
      ["Types", cluster.NodeIds?.length ?? 0],
      ["Internal Edges", cluster.InternalEdges ?? 0],
      ["External Edges", cluster.ExternalEdges ?? 0],
      ["Score", cluster.Score ?? 0],
      ["Keywords", (cluster.Keywords ?? []).join(", ") || "-"],
      ["Entry Candidates", (cluster.EntryMethodIds ?? []).length]
    ]);
    els.secondaryTitle.textContent = "System Report";
    els.examples.innerHTML = systemReportHtml(cluster);
    renderAiSummary(null, "Select a type node to prepare an AI summary.", cluster);
    renderFlowTrace();
    return;
  }

  if (state.selected.type === "node") {
    const node = state.graph.Nodes.find(item => item.Id === state.selected.id);
    if (!node) return;
    const incoming = state.graph.Edges.filter(edge => edge.Target === node.Id).length;
    const outgoing = state.graph.Edges.filter(edge => edge.Source === node.Id).length;
    const neighborhood = selectedNeighborhoodIds();
    els.detailTitle.textContent = node.Name;
    els.detailSubtitle.textContent = node.Id;
    setDetailRows([
      ["Kind", node.IsUnityType ? `${node.Kind} · Unity` : node.Kind],
      ["Namespace", node.Namespace || "-"],
      ["File", `${node.File}:${node.Line}`],
      ["Base Types", (node.BaseTypes ?? []).join(", ") || "-"],
      ["Attributes", (node.Attributes ?? []).join(", ") || "-"],
      ["Degree", `${outgoing} out · ${incoming} in`]
    ]);
    appendDetailRow("Neighborhood", neighborhood ? `${neighborhood.size} types / depth ${state.neighborhoodDepth} / ${state.neighborhoodDirection}` : "-");
    els.secondaryTitle.textContent = "Code Calls";
    els.examples.innerHTML = codeCallSummaryHtml(node);
    renderAiSummary(node);
    renderFlowTrace(node.Id);
    return;
  }

  const edge = state.graph.Edges.find(item => edgeKey(item) === state.selected.key);
  if (!edge) return;
  els.detailTitle.textContent = formatKind(edge.Kind);
  els.detailSubtitle.textContent = `${edge.Source} -> ${edge.Target}`;
  setDetailRows([
    ["Source", edge.Source],
    ["Target", edge.Target],
    ["Weight", edge.Weight],
    ["Kind", edge.Kind]
  ]);
  els.secondaryTitle.textContent = "Examples";
  renderExamples(edge.Examples ?? []);
  renderAiSummary(null, "Select a type node or system cluster to prepare an AI summary.");
  renderFlowTrace();
}

function renderAiSummary(node = null, readyMessage = "Select a type node to prepare an AI summary.", system = null) {
  if (!els.aiSummary) return;

  const status = state.aiStatus;
  if (!status.checked) {
    els.aiSummary.innerHTML = `
      <div class="ai-state is-muted">
        <span class="ai-badge">checking</span>
        <strong>AI status is being checked.</strong>
      </div>`;
    return;
  }

  if (!status.enabled) {
    els.aiSummary.innerHTML = `
      <div class="ai-state is-muted">
        <span class="ai-badge">offline</span>
        <strong>AI summary unavailable</strong>
        <span>${escapeHtml(status.reason || "Configure OPENAI_API_KEY to enable AI summaries.")}</span>
      </div>`;
    return;
  }

  if (system) {
    renderAiSystemSummary(system);
    return;
  }

  if (!node) {
    els.aiSummary.innerHTML = `
      <div class="ai-state">
        <span class="ai-badge">ready</span>
        <strong>${escapeHtml(aiProviderLabel())} is connected.</strong>
        <span>${escapeHtml(readyMessage)}</span>
      </div>`;
    return;
  }

  const key = aiTargetKey("node", node.Id);
  if (state.aiBusyNodeId === key) {
    els.aiSummary.innerHTML = `
      <div class="ai-state">
        <span class="ai-badge">generating</span>
        <strong>Reading graph evidence for ${escapeHtml(node.Name)}.</strong>
        <span>This uses extracted relationships and method calls only.</span>
      </div>`;
    return;
  }

  const cached = readAiSummaryCache("node", node.Id);
  if (cached?.result) {
    els.aiSummary.innerHTML = aiSummaryResultHtml("node", node.Name, cached);
    return;
  }

  const error = state.aiError ? `<span class="ai-error">${escapeHtml(state.aiError)}</span>` : "";
  els.aiSummary.innerHTML = `
    <div class="ai-state">
      <span class="ai-badge">ready</span>
      <strong>${escapeHtml(node.Name)} can be summarized.</strong>
      <span>${escapeHtml(aiProviderLabel())} will use extracted graph data, not full source files.</span>
      ${error}
      <div class="ai-actions">
        <button type="button" data-ai-action="explain-node">Explain Node</button>
        <button type="button" data-ai-action="workflow">AI Walkthrough</button>
      </div>
    </div>`;
}

function renderAiSystemSummary(system) {
  const key = aiTargetKey("system", system.Id);
  if (state.aiBusyNodeId === key) {
    els.aiSummary.innerHTML = `
      <div class="ai-state">
        <span class="ai-badge">generating</span>
        <strong>Reading system evidence for ${escapeHtml(system.Name)}.</strong>
        <span>This uses cluster types, internal flows, and external touchpoints.</span>
      </div>`;
    return;
  }

  const cached = readAiSummaryCache("system", system.Id);
  if (cached?.result) {
    els.aiSummary.innerHTML = aiSummaryResultHtml("system", system.Name, cached);
    return;
  }

  const error = state.aiError ? `<span class="ai-error">${escapeHtml(state.aiError)}</span>` : "";
  els.aiSummary.innerHTML = `
    <div class="ai-state">
      <span class="ai-badge">ready</span>
      <strong>${escapeHtml(system.Name)} can be summarized.</strong>
      <span>${escapeHtml(aiProviderLabel())} will use cluster graph data, not full source files.</span>
      ${error}
      <div class="ai-actions">
        <button type="button" data-ai-action="explain-system">Explain System</button>
        <button type="button" data-ai-action="workflow">AI Walkthrough</button>
      </div>
    </div>`;
}

async function explainSelectedNode(force = false) {
  if (state.selected?.type !== "node" || !state.graph) return;
  const node = state.graph.Nodes.find(item => item.Id === state.selected.id);
  if (!node || !state.aiStatus.enabled || state.aiBusyNodeId) return;
  const key = aiTargetKey("node", node.Id);

  if (!force && readAiSummaryCache("node", node.Id)?.result) {
    renderAiSummary(node);
    return;
  }

  state.aiBusyNodeId = key;
  state.aiError = "";
  renderAiSummary(node);
  const payload = buildNodeAiPayload(node);

  try {
    const response = await fetch("/ai/explain-node", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(data.error || `AI request failed (${response.status})`);
    }

    writeAiSummaryCache("node", node.Id, {
      schemaVersion: 1,
      createdAt: data.createdAt || new Date().toISOString(),
      model: data.model || state.aiStatus.model || "",
      result: data.result || data,
      evidence: payload.evidence ?? []
    });
  } catch (error) {
    state.aiError = error instanceof Error ? error.message : "AI request failed.";
  } finally {
    state.aiBusyNodeId = "";
    renderAiSummary(node);
  }
}

async function explainSelectedSystem(force = false) {
  if (state.selected?.type !== "system" || !state.graph) return;
  const system = selectedSystem();
  if (!system || !state.aiStatus.enabled || state.aiBusyNodeId) return;
  const key = aiTargetKey("system", system.Id);

  if (!force && readAiSummaryCache("system", system.Id)?.result) {
    renderAiSummary(null, "", system);
    return;
  }

  state.aiBusyNodeId = key;
  state.aiError = "";
  renderAiSummary(null, "", system);
  const payload = buildSystemAiPayload(system);

  try {
    const response = await fetch("/ai/explain-system", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(data.error || `AI request failed (${response.status})`);
    }

    writeAiSummaryCache("system", system.Id, {
      schemaVersion: 1,
      createdAt: data.createdAt || new Date().toISOString(),
      model: data.model || state.aiStatus.model || "",
      result: data.result || data,
      evidence: payload.evidence ?? []
    });
  } catch (error) {
    state.aiError = error instanceof Error ? error.message : "AI request failed.";
  } finally {
    state.aiBusyNodeId = "";
    renderAiSummary(null, "", system);
  }
}

function openWorkflowForSelection(force = false) {
  const target = currentWorkflowTarget();
  if (!target) {
    state.explainOpen = true;
    state.explainTarget = null;
    state.explainError = "Select a type node or system cluster first.";
    renderExplainDrawer();
    return;
  }

  state.explainOpen = true;
  state.explainTarget = target;
  state.explainError = "";
  renderExplainDrawer();
  requestWorkflowForCurrentTarget(force);
}

function currentWorkflowTarget() {
  if (state.selected?.type === "system") {
    const system = selectedSystem();
    return system ? { kind: "system", id: system.Id, title: system.Name } : null;
  }
  if (state.selected?.type === "node") {
    const node = state.graph?.Nodes?.find(item => item.Id === state.selected.id);
    return node ? { kind: "node", id: node.Id, title: node.Name } : null;
  }
  const system = selectedSystem();
  return system ? { kind: "system", id: system.Id, title: system.Name } : null;
}

async function requestWorkflowForCurrentTarget(force = false) {
  const target = state.explainTarget;
  if (!target || !state.graph || !state.aiStatus.enabled || state.explainBusyKey) {
    renderExplainDrawer();
    return;
  }

  if (!force && readAiSummaryCache(`workflow-${target.kind}`, target.id)?.result) {
    renderExplainDrawer();
    return;
  }

  const payload = buildWorkflowAiPayload(target);
  if (!payload) {
    state.explainError = "Could not build a workflow payload for the current selection.";
    renderExplainDrawer();
    return;
  }

  state.explainBusyKey = aiTargetKey(`workflow-${target.kind}`, target.id);
  state.explainError = "";
  renderExplainDrawer();

  try {
    const response = await fetch("/ai/explain-workflow", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(data.error || `AI request failed (${response.status})`);
    }

    writeAiSummaryCache(`workflow-${target.kind}`, target.id, {
      schemaVersion: 1,
      createdAt: data.createdAt || new Date().toISOString(),
      model: data.model || state.aiStatus.model || "",
      result: data.result || data,
      evidence: payload.evidence ?? []
    });
  } catch (error) {
    state.explainError = error instanceof Error ? error.message : "AI workflow request failed.";
  } finally {
    state.explainBusyKey = "";
    renderExplainDrawer();
  }
}

function buildWorkflowAiPayload(target) {
  if (target.kind === "system") {
    const system = (state.graph?.SystemClusters ?? []).find(cluster => cluster.Id === target.id);
    if (!system) return null;
    return {
      purpose: "Create a long-form code reading walkthrough for this system. Explain where to start, what to inspect next, important flows, and review risks.",
      targetKind: "system",
      ...buildSystemAiPayload(system)
    };
  }

  const node = state.graph?.Nodes?.find(item => item.Id === target.id);
  if (!node) return null;
  return {
    purpose: "Create a long-form code reading walkthrough for this type. Explain caller/callee context, nearby systems, and the next files or methods to inspect.",
    targetKind: "node",
    ...buildNodeAiPayload(node)
  };
}

function renderExplainDrawer() {
  if (!els.explainDrawer) return;
  const target = state.explainTarget;
  els.explainDrawer.hidden = !state.explainOpen;
  els.explainDrawer.style.width = `${clampExplainWidth(state.explainWidth)}px`;
  if (!state.explainOpen) return;

  els.explainTitle.textContent = target?.title || "Explanation";
  els.explainSubtitle.textContent = target
    ? `${target.kind === "system" ? "System" : "Type"} walkthrough / ${aiProviderLabel() || "AI"}`
    : "Select a type node or system cluster.";
  const cached = target ? readAiSummaryCache(`workflow-${target.kind}`, target.id) : null;
  els.explainExport.disabled = !cached?.result;
  els.explainRegenerate.disabled = !target || !state.aiStatus.enabled || Boolean(state.explainBusyKey);

  if (!target) {
    els.explainContent.innerHTML = explainStateHtml("ready", "No selection", state.explainError || "Select a system or type node, then open AI Walkthrough.");
    return;
  }

  if (!state.aiStatus.enabled) {
    els.explainContent.innerHTML = explainStateHtml("offline", "AI walkthrough unavailable", state.aiStatus.reason || "Configure an AI provider first.");
    return;
  }

  const key = aiTargetKey(`workflow-${target.kind}`, target.id);
  if (state.explainBusyKey === key) {
    els.explainContent.innerHTML = explainStateHtml("generating", `Building a reading guide for ${target.title}`, "The AI is using graph edges, method calls, examples, and inferred flow traces.");
    return;
  }

  if (cached?.result) {
    els.explainContent.innerHTML = workflowResultHtml(cached);
    return;
  }

  const error = state.explainError ? `<span class="ai-error">${escapeHtml(state.explainError)}</span>` : "";
  els.explainContent.innerHTML = `
    ${explainStateHtml("ready", `${target.title} can be walked through.`, "This creates a longer reading guide with code examples and evidence.")}
    ${error}
    <div class="explain-inline-actions">
      <button type="button" data-ai-action="workflow">Generate Walkthrough</button>
    </div>`;
}

function explainStateHtml(badge, title, detail) {
  return `
    <div class="explain-state">
      <span class="ai-badge">${escapeHtml(badge)}</span>
      <strong>${escapeHtml(title)}</strong>
      <p>${escapeHtml(detail)}</p>
    </div>`;
}

function workflowResultHtml(cached) {
  const result = normalizeWorkflowResult(cached.result ?? {});
  return `
    <div class="workflow-document">
      <section class="workflow-hero">
        <span class="ai-badge">walkthrough</span>
        <h1>${escapeHtml(result.title || "Code Walkthrough")}</h1>
        <p>${escapeHtml(result.overview || "No overview returned.")}</p>
        <span>${escapeHtml([cached.model, result.confidence ? `confidence: ${result.confidence}` : ""].filter(Boolean).join(" / "))}</span>
      </section>
      ${workflowGuideHtml()}
      ${workflowPathHtml(result.readingPath)}
      ${workflowListHtml("Important Flows", result.importantFlows)}
      ${workflowCodeExamplesHtml(result.codeExamples)}
      ${workflowListHtml("Review Risks", result.risks)}
      ${workflowListHtml("Questions To Ask Next", result.nextQuestions)}
      ${aiEvidenceHtml(cached.evidence)}
      <footer>${escapeHtml(result.disclaimer || "AI walkthrough is based on extracted graph data, not full semantic compilation.")}</footer>
    </div>`;
}

function workflowGuideHtml() {
  return `
    <section class="workflow-guide">
      <span class="ai-badge">graph jump</span>
      <strong>Reading Path 아래의 파란 칩을 누르면 해당 노드나 edge로 이동합니다.</strong>
      <p>예: <code>BattleManager.StartBattle()</code>, <code>DamageCalculator</code>, <code>BattleManager -> BattleState</code> 칩을 눌러 AI 설명을 따라 그래프를 탐색하세요.</p>
    </section>`;
}

function exportCurrentWorkflowMarkdown() {
  const target = state.explainTarget;
  if (!target) return;
  const cached = readAiSummaryCache(`workflow-${target.kind}`, target.id);
  if (!cached?.result) return;

  const result = normalizeWorkflowResult(cached.result);
  const lines = [
    `# ${result.title || target.title || "Code Walkthrough"}`,
    "",
    `- Target: ${target.title}`,
    `- Kind: ${target.kind}`,
    `- Model: ${cached.model || "unknown"}`,
    `- Exported: ${new Date().toISOString()}`,
    "",
    "## Overview",
    "",
    result.overview || "",
    ""
  ];

  if (result.readingPath.length) {
    lines.push("## Recommended Reading Path", "");
    result.readingPath.forEach((item, index) => {
      lines.push(`${index + 1}. **${item.stepTitle || "Inspect this part"}**`);
      if (item.why) lines.push(`   - ${item.why}`);
      if (item.inspect) lines.push(`   - Inspect: ${item.inspect}`);
      if (Array.isArray(item.evidenceRefs) && item.evidenceRefs.length) {
        lines.push(`   - Evidence: ${item.evidenceRefs.slice(0, 4).join("; ")}`);
      }
    });
    lines.push("");
  }

  appendMarkdownList(lines, "Important Flows", result.importantFlows);
  appendMarkdownCodeExamples(lines, result.codeExamples);
  appendMarkdownList(lines, "Review Risks", result.risks);
  appendMarkdownList(lines, "Questions To Ask Next", result.nextQuestions);
  appendMarkdownEvidence(lines, cached.evidence);

  if (result.disclaimer) {
    lines.push("## Disclaimer", "", result.disclaimer, "");
  }

  const fileName = `${safeFileName(`${target.title}-walkthrough`)}.md`;
  downloadTextFile(fileName, lines.join("\n"), "text/markdown;charset=utf-8");
}

function appendMarkdownList(lines, title, items) {
  if (!Array.isArray(items) || !items.length) return;
  lines.push(`## ${title}`, "");
  for (const item of items) {
    lines.push(`- ${item}`);
  }
  lines.push("");
}

function appendMarkdownCodeExamples(lines, items) {
  if (!Array.isArray(items) || !items.length) return;
  lines.push("## Code Examples", "");
  for (const item of items) {
    lines.push(`### ${item.title || "Example"}`);
    const location = [shortFile(item.file), item.line || ""].filter(Boolean).join(":");
    if (location) lines.push(`Source: \`${location}\``);
    lines.push("", "```csharp", item.code || "", "```");
    if (item.why) lines.push("", item.why);
    lines.push("");
  }
}

function appendMarkdownEvidence(lines, evidence) {
  if (!Array.isArray(evidence) || !evidence.length) return;
  lines.push("## Evidence", "");
  for (const item of evidence.slice(0, 10)) {
    const detail = [item.title || item.kind || "Evidence", item.detail || ""].filter(Boolean).join(" - ");
    lines.push(`- ${detail}`);
    if (item.example) {
      lines.push(`  - ${exampleLabel(item.example)}`);
    }
  }
  lines.push("");
}

function downloadTextFile(fileName, text, type) {
  const blob = new Blob([text], { type });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.append(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

function normalizeWorkflowResult(result) {
  const readingPath = Array.isArray(result.readingPath) ? result.readingPath : [];
  const importantFlows = Array.isArray(result.importantFlows)
    ? result.importantFlows
    : Array.isArray(result.touchpoints)
      ? result.touchpoints
      : [];
  const codeExamples = Array.isArray(result.codeExamples) ? result.codeExamples : [];
  const risks = Array.isArray(result.risks) ? result.risks : [];
  const nextQuestions = Array.isArray(result.nextQuestions) ? result.nextQuestions : [];

  return {
    title: result.title || result.name || "Code Walkthrough",
    overview: result.overview || result.summary || "The AI response did not include a walkthrough overview. Regenerate after updating the launcher to request the full workflow schema.",
    readingPath: readingPath.length
      ? readingPath
      : Array.isArray(result.responsibilities)
        ? result.responsibilities.slice(0, 6).map((item, index) => ({
          stepTitle: `Step ${index + 1}`,
          why: item,
          inspect: "",
          evidenceRefs: []
        }))
        : [],
    importantFlows,
    codeExamples,
    risks,
    nextQuestions,
    confidence: result.confidence || "",
    disclaimer: result.disclaimer || "AI walkthrough is based on extracted graph data, not full semantic compilation."
  };
}

function workflowPathHtml(items) {
  if (!Array.isArray(items) || !items.length) return "";
  return `
    <section class="workflow-section">
      <h3>Recommended Reading Path</h3>
      <ol class="workflow-path">
        ${items.slice(0, 8).map(item => {
          const refs = workflowRefsFromText(
            item.stepTitle,
            item.why,
            item.inspect,
            Array.isArray(item.evidenceRefs) ? item.evidenceRefs : []
          );
          return `
          <li>
            <strong>${escapeHtml(item.stepTitle || "Inspect this part")}</strong>
            <p>${escapeHtml(item.why || "")}</p>
            ${item.inspect ? `<span>${escapeHtml(item.inspect)}</span>` : ""}
            ${Array.isArray(item.evidenceRefs) && item.evidenceRefs.length
              ? workflowEvidenceRefsHtml(item.evidenceRefs)
              : ""}
            ${workflowNavRefsHtml(refs)}
          </li>
        `;
        }).join("")}
      </ol>
    </section>`;
}

function workflowEvidenceRefsHtml(items) {
  return `
    <div class="workflow-evidence-refs">
      ${items.slice(0, 4).map(item => `<span>${escapeHtml(item)}</span>`).join("")}
    </div>`;
}

function workflowListHtml(title, items) {
  if (!Array.isArray(items) || !items.length) return "";
  return `
    <section class="workflow-section">
      <h3>${escapeHtml(title)}</h3>
      <ul>${items.slice(0, 8).map(item => `
        <li>
          ${escapeHtml(item)}
          ${workflowNavRefsHtml(workflowRefsFromText(item))}
        </li>
      `).join("")}</ul>
    </section>`;
}

function workflowCodeExamplesHtml(items) {
  if (!Array.isArray(items) || !items.length) return "";
  return `
    <section class="workflow-section">
      <h3>Code Examples</h3>
      ${items.slice(0, 6).map(item => {
        const refs = workflowRefsFromText(item.title, item.file, item.code, item.why);
        return `
        <div class="workflow-code-example">
          <strong>${escapeHtml(item.title || "Example")}</strong>
          <span>${escapeHtml([shortFile(item.file), item.line || ""].filter(Boolean).join(":"))}</span>
          <pre><code>${escapeHtml(item.code || "")}</code></pre>
          ${item.why ? `<p>${escapeHtml(item.why)}</p>` : ""}
          ${workflowNavRefsHtml(refs)}
        </div>
      `;
      }).join("")}
    </section>`;
}

function workflowRefsFromText(...parts) {
  if (!state.graph) return [];
  const text = parts.flat(Infinity).filter(Boolean).map(String).join(" ");
  if (!text.trim()) return [];

  const refs = [];
  const seen = new Set();
  const addRef = (ref) => {
    const key = ref.kind === "edge"
      ? `edge:${ref.source}|${ref.target}|${ref.edgeKind || ""}`
      : `${ref.kind}:${ref.id || ref.nodeId}`;
    if (seen.has(key)) return;
    seen.add(key);
    refs.push(ref);
  };

  const methods = (state.graph.Methods ?? [])
    .slice()
    .sort((a, b) => methodLabel(b).length - methodLabel(a).length);
  for (const method of methods) {
    const type = shortTypeId(method.TypeId);
    const signature = String(method.Signature || method.Name || "");
    const candidates = [
      `${type}.${signature}`,
      `${type}.${method.Name || ""}`,
      signature
    ].filter(value => value && value.length > 3);
    if (candidates.some(candidate => containsWorkflowTerm(text, candidate))) {
      addRef({
        kind: "method",
        id: method.Id,
        nodeId: method.TypeId,
        label: `${type}.${signature}`
      });
    }
    if (refs.filter(ref => ref.kind === "method").length >= 3) break;
  }

  const nodes = (state.graph.Nodes ?? [])
    .slice()
    .sort((a, b) => typeName(b).length - typeName(a).length);
  for (const node of nodes) {
    const name = typeName(node);
    if (name.length < 3 || !containsWorkflowTerm(text, name)) continue;
    if (refs.some(ref => ref.nodeId === node.Id || ref.id === node.Id)) continue;
    addRef({ kind: "node", id: node.Id, label: name });
    if (refs.filter(ref => ref.kind === "node").length >= 3) break;
  }

  const nodeById = new Map((state.graph.Nodes ?? []).map(node => [node.Id, node]));
  for (const edge of state.graph.Edges ?? []) {
    const sourceName = typeName(nodeById.get(edge.Source) ?? { Id: edge.Source });
    const targetName = typeName(nodeById.get(edge.Target) ?? { Id: edge.Target });
    if (!sourceName || !targetName) continue;
    if (!containsWorkflowEdge(text, sourceName, targetName)) continue;
    addRef({
      kind: "edge",
      source: edge.Source,
      target: edge.Target,
      edgeKind: edge.Kind,
      label: `${sourceName} -> ${targetName}`
    });
    if (refs.filter(ref => ref.kind === "edge").length >= 2) break;
  }

  return refs.slice(0, 6);
}

function workflowNavRefsHtml(refs) {
  if (!Array.isArray(refs) || !refs.length) return "";
  return `
    <div class="workflow-nav-refs">
      ${refs.map(ref => {
        if (ref.kind === "method") {
          return `<button type="button" data-workflow-focus="method" data-method-id="${escapeHtml(ref.id)}" data-node-id="${escapeHtml(ref.nodeId)}">${escapeHtml(ref.label)}</button>`;
        }
        if (ref.kind === "edge") {
          return `<button type="button" data-workflow-focus="edge" data-source-id="${escapeHtml(ref.source)}" data-target-id="${escapeHtml(ref.target)}" data-edge-kind="${escapeHtml(ref.edgeKind || "")}">${escapeHtml(ref.label)}</button>`;
        }
        return `<button type="button" data-workflow-focus="node" data-node-id="${escapeHtml(ref.id)}">${escapeHtml(ref.label)}</button>`;
      }).join("")}
    </div>`;
}

function closeExplainDrawer() {
  state.explainOpen = false;
  state.explainError = "";
  renderExplainDrawer();
}

function startExplainResize(event) {
  event.preventDefault();
  state.explainResize = {
    startX: event.clientX,
    startWidth: clampExplainWidth(state.explainWidth)
  };
  els.explainResize.setPointerCapture?.(event.pointerId);
  document.body.classList.add("is-resizing-explain");
}

function resizeExplainDrawer(event) {
  if (!state.explainResize) return false;
  state.explainWidth = clampExplainWidth(state.explainResize.startWidth + state.explainResize.startX - event.clientX);
  els.explainDrawer.style.width = `${state.explainWidth}px`;
  return true;
}

function endExplainResize() {
  if (!state.explainResize) return false;
  state.explainResize = null;
  document.body.classList.remove("is-resizing-explain");
  try {
    localStorage.setItem("UnityCodeGraph:explain-width", String(state.explainWidth));
  } catch {
    // Width persistence is optional.
  }
  return true;
}

function clampExplainWidth(width) {
  return clamp(Number(width) || 620, 460, Math.max(460, window.innerWidth - 280));
}

function aiProviderLabel() {
  return [state.aiStatus.provider || "AI", state.aiStatus.model].filter(Boolean).join(" / ");
}

function aiSummaryResultHtml(kind, title, cached) {
  const result = cached.result ?? {};
  return `
    <div class="ai-state">
      <span class="ai-badge">summary</span>
      <strong>${escapeHtml(title)}</strong>
      <p>${escapeHtml(result.summary || "No summary returned.")}</p>
      ${aiSummaryListHtml("Responsibilities", result.responsibilities)}
      ${aiSummaryListHtml("Touchpoints", result.touchpoints)}
      ${aiSummaryListHtml("Risks", result.risks)}
      ${aiEvidenceHtml(cached.evidence)}
      <span>${escapeHtml([cached.model, result.confidence ? `confidence: ${result.confidence}` : ""].filter(Boolean).join(" · "))}</span>
      <span>${escapeHtml(result.disclaimer || "AI summary is based on extracted graph data.")}</span>
      <div class="ai-actions">
        <button type="button" data-ai-action="${kind === "system" ? "explain-system" : "explain-node"}" data-force="true">Regenerate</button>
        <button type="button" data-ai-action="workflow">AI Walkthrough</button>
      </div>
    </div>`;
}

function aiSummaryListHtml(title, items) {
  if (!Array.isArray(items) || !items.length) return "";
  return `
    <section class="ai-result-section">
      <h3>${escapeHtml(title)}</h3>
      <ul>${items.slice(0, 5).map(item => `<li>${escapeHtml(item)}</li>`).join("")}</ul>
    </section>`;
}

function aiEvidenceHtml(evidence) {
  if (!Array.isArray(evidence) || !evidence.length) return "";
  return `
    <section class="ai-result-section ai-evidence">
      <h3>Evidence</h3>
      ${evidence.slice(0, 8).map(item => `
        <div class="ai-evidence-row">
          <strong>${escapeHtml(item.title || item.kind || "graph evidence")}</strong>
          <span>${escapeHtml(item.detail || "")}</span>
          ${item.example ? `<code>${escapeHtml(exampleLabel(item.example))}</code>` : ""}
        </div>
      `).join("")}
    </section>`;
}

function buildNodeAiPayload(node) {
  const graph = state.graph;
  const methods = graph?.Methods ?? [];
  const methodEdges = graph?.MethodEdges ?? [];
  const edges = graph?.Edges ?? [];
  const nodeById = new Map((graph?.Nodes ?? []).map(item => [item.Id, item]));
  const methodById = new Map(methods.map(method => [method.Id, method]));

  const outgoing = edges
    .filter(edge => edge.Source === node.Id)
    .sort(edgePayloadSort)
    .slice(0, 12)
    .map(edge => relationshipPayload(edge, "out", nodeById));
  const incoming = edges
    .filter(edge => edge.Target === node.Id)
    .sort(edgePayloadSort)
    .slice(0, 12)
    .map(edge => relationshipPayload(edge, "in", nodeById));

  const outgoingCalls = methodEdges
    .map(edge => ({ edge, source: methodById.get(edge.Source), target: methodById.get(edge.Target) }))
    .filter(item => item.source?.TypeId === node.Id && item.target?.TypeId !== node.Id)
    .sort(callSummarySort)
    .slice(0, 12)
    .map(item => methodCallPayload(item, "out"));
  const incomingCalls = methodEdges
    .map(edge => ({ edge, source: methodById.get(edge.Source), target: methodById.get(edge.Target) }))
    .filter(item => item.target?.TypeId === node.Id && item.source?.TypeId !== node.Id)
    .sort(callSummarySort)
    .slice(0, 12)
    .map(item => methodCallPayload(item, "in"));
  const evidence = nodeEvidence(node, outgoing, incoming, outgoingCalls, incomingCalls);

  return {
    schemaVersion: 1,
    language: state.language,
    graph: {
      rootPath: graph?.RootPath ?? "",
      nodeCount: graph?.Nodes?.length ?? 0,
      edgeCount: graph?.Edges?.length ?? 0,
      methodCount: graph?.Methods?.length ?? 0,
      methodEdgeCount: graph?.MethodEdges?.length ?? 0
    },
    node: {
      id: node.Id,
      name: node.Name,
      namespace: node.Namespace || "",
      kind: node.Kind,
      isUnityType: Boolean(node.IsUnityType),
      file: node.File || "",
      line: node.Line || 0,
      baseTypes: node.BaseTypes ?? [],
      attributes: node.Attributes ?? []
    },
    degree: {
      incoming: edges.filter(edge => edge.Target === node.Id).length,
      outgoing: edges.filter(edge => edge.Source === node.Id).length
    },
    relationships: {
      outgoing,
      incoming
    },
    methods: methods
      .filter(method => method.TypeId === node.Id)
      .sort((a, b) => entryRank(a) - entryRank(b) || a.Line - b.Line)
      .slice(0, 16)
      .map(method => ({
        id: method.Id,
        name: method.Name,
        signature: method.Signature,
        entryKind: method.EntryKind || "",
        isEntryPoint: Boolean(method.IsEntryPoint),
        file: method.File || "",
        line: method.Line || 0
      })),
    methodCalls: {
      outgoing: outgoingCalls,
      incoming: incomingCalls
    },
    evidence,
    limits: {
      maxSentRelationships: 12,
      maxSentMethodCalls: 12,
      maxSentMethods: 16
    }
  };
}

function buildSystemAiPayload(cluster) {
  const graph = state.graph;
  const nodes = graph?.Nodes ?? [];
  const edges = graph?.Edges ?? [];
  const methods = graph?.Methods ?? [];
  const methodEdges = graph?.MethodEdges ?? [];
  const nodeById = new Map(nodes.map(node => [node.Id, node]));
  const methodById = new Map(methods.map(method => [method.Id, method]));
  const clusterIds = new Set(cluster.NodeIds ?? []);
  const report = buildSystemReport(cluster);

  const clusterNodes = [...clusterIds]
    .map(id => nodeById.get(id))
    .filter(Boolean)
    .sort((a, b) => a.Name.localeCompare(b.Name))
    .slice(0, 28)
    .map(node => ({
      id: node.Id,
      name: node.Name,
      namespace: node.Namespace || "",
      kind: node.Kind,
      isUnityType: Boolean(node.IsUnityType),
      file: node.File || "",
      line: node.Line || 0
    }));

  const internalRelationships = edges
    .filter(edge => clusterIds.has(edge.Source) && clusterIds.has(edge.Target))
    .sort(edgePayloadSort)
    .slice(0, 16)
    .map(edge => relationshipPayload(edge, "both", nodeById));
  const externalRelationships = edges
    .filter(edge => clusterIds.has(edge.Source) !== clusterIds.has(edge.Target))
    .sort(edgePayloadSort)
    .slice(0, 16)
    .map(edge => ({
      ...relationshipPayload(edge, "both", nodeById),
      direction: clusterIds.has(edge.Source) ? "outgoing" : "incoming"
    }));
  const internalCalls = methodEdges
    .map(edge => ({ edge, source: methodById.get(edge.Source), target: methodById.get(edge.Target) }))
    .filter(item => item.source && item.target && clusterIds.has(item.source.TypeId) && clusterIds.has(item.target.TypeId))
    .sort(callSummarySort)
    .slice(0, 16)
    .map(item => methodCallPayload(item, "both"));
  const entryMethods = (cluster.EntryMethodIds ?? [])
    .map(id => methodById.get(id))
    .filter(Boolean)
    .slice(0, 10)
    .map(method => ({
      id: method.Id,
      typeId: method.TypeId,
      signature: method.Signature,
      entryKind: method.EntryKind || "",
      file: method.File || "",
      line: method.Line || 0
    }));
  const evidence = systemEvidence(cluster, internalRelationships, externalRelationships, internalCalls, report);

  return {
    schemaVersion: 1,
    language: state.language,
    graph: {
      rootPath: graph?.RootPath ?? "",
      nodeCount: nodes.length,
      edgeCount: edges.length,
      methodCount: methods.length,
      methodEdgeCount: methodEdges.length
    },
    system: {
      id: cluster.Id,
      name: cluster.Name,
      keywords: cluster.Keywords ?? [],
      nodeCount: cluster.NodeCount ?? clusterNodes.length,
      internalEdges: cluster.InternalEdges ?? 0,
      externalEdges: cluster.ExternalEdges ?? 0,
      entryMethodCount: (cluster.EntryMethodIds ?? []).length
    },
    types: clusterNodes,
    entryMethods,
    report,
    relationships: {
      internal: internalRelationships,
      external: externalRelationships
    },
    methodCalls: {
      internal: internalCalls
    },
    evidence,
    limits: {
      maxSentTypes: 28,
      maxSentRelationships: 16,
      maxSentMethodCalls: 16
    }
  };
}

function relationshipPayload(edge, direction, nodeById = new Map()) {
  const example = normalizeExample(edge.Examples?.[0]);
  const sourceNode = nodeById.get(edge.Source);
  const targetNode = nodeById.get(edge.Target);
  const includeSource = direction === "in" || direction === "both";
  const includeTarget = direction === "out" || direction === "both";
  return {
    kind: edge.Kind,
    source: includeSource ? edge.Source : undefined,
    sourceName: includeSource ? sourceNode?.Name ?? typeName({ Id: edge.Source }) : undefined,
    target: includeTarget ? edge.Target : undefined,
    targetName: includeTarget ? targetNode?.Name ?? typeName({ Id: edge.Target }) : undefined,
    weight: edge.Weight ?? 1,
    example
  };
}

function methodCallPayload(item, direction) {
  const example = normalizeExample(item.edge.Examples?.[0]);
  if (direction === "both") {
    return {
      sourceType: item.source?.TypeId || "",
      source: item.source?.Signature || "",
      targetType: item.target?.TypeId || "",
      target: item.target?.Signature || "",
      weight: item.edge.Weight ?? 1,
      example
    };
  }

  if (direction === "out") {
    return {
      source: item.source?.Signature || "",
      targetType: item.target?.TypeId || "",
      target: item.target?.Signature || "",
      weight: item.edge.Weight ?? 1,
      example
    };
  }

  return {
    sourceType: item.source?.TypeId || "",
    source: item.source?.Signature || "",
    target: item.target?.Signature || "",
    weight: item.edge.Weight ?? 1,
    example
  };
}

function nodeEvidence(node, outgoing, incoming, outgoingCalls, incomingCalls) {
  const rows = [];
  for (const call of incomingCalls.slice(0, 3)) {
    rows.push({
      title: "Called by",
      detail: `${shortTypeId(call.sourceType)}.${call.source} -> ${node.Name}`,
      example: call.example
    });
  }
  for (const call of outgoingCalls.slice(0, 3)) {
    rows.push({
      title: "Calls",
      detail: `${node.Name}.${call.source} -> ${shortTypeId(call.targetType)}.${call.target}`,
      example: call.example
    });
  }
  for (const edge of [...incoming, ...outgoing].slice(0, 4)) {
    rows.push({
      title: formatKind(edge.kind),
      detail: relationEvidenceDetail(edge),
      example: edge.example
    });
  }
  return rows.slice(0, 8);
}

function systemEvidence(cluster, internalRelationships, externalRelationships, internalCalls, report) {
  const rows = [];
  for (const flow of (report.flows ?? []).slice(0, 2)) {
    rows.push({
      title: "Likely flow",
      detail: `${flow.entry} -> ${flow.steps.slice(1, 4).join(" -> ") || "terminal"}`
    });
  }
  for (const call of internalCalls.slice(0, 3)) {
    rows.push({
      title: "Internal call",
      detail: `${shortTypeId(call.sourceType)}.${call.source} -> ${shortTypeId(call.targetType)}.${call.target}`,
      example: call.example
    });
  }
  for (const edge of externalRelationships.slice(0, 3)) {
    rows.push({
      title: `${edge.direction === "incoming" ? "Incoming" : "Outgoing"} ${formatKind(edge.kind)}`,
      detail: relationEvidenceDetail(edge),
      example: edge.example
    });
  }
  for (const edge of internalRelationships.slice(0, Math.max(0, 8 - rows.length))) {
    rows.push({
      title: `Internal ${formatKind(edge.kind)}`,
      detail: relationEvidenceDetail(edge),
      example: edge.example
    });
  }
  if (!rows.length) {
    rows.push({
      title: "System cluster",
      detail: `${cluster.Name} contains ${cluster.NodeCount ?? (cluster.NodeIds ?? []).length} related types.`
    });
  }
  return rows.slice(0, 8);
}

function relationEvidenceDetail(edge) {
  const left = edge.sourceName || shortTypeId(edge.source);
  const right = edge.targetName || shortTypeId(edge.target);
  if (left && right) return `${left} -> ${right} / ${edge.weight ?? 1} refs`;
  return `${left || right || "related type"} / ${edge.weight ?? 1} refs`;
}

function shortTypeId(typeId) {
  if (!typeId) return "";
  return String(typeId).split(".").pop().split("+").pop();
}

function exampleLabel(example) {
  if (!example) return "";
  const location = [shortFile(example.file), example.line || ""].filter(Boolean).join(":");
  return [location, example.text || ""].filter(Boolean).join(" / ");
}

function normalizeExample(example) {
  if (!example) return null;
  return {
    file: example.File || "",
    line: example.Line || 0,
    text: String(example.Text || "").slice(0, 240)
  };
}

function edgePayloadSort(a, b) {
  return (b.Weight ?? 1) - (a.Weight ?? 1)
    || relationKindRank(a.Kind) - relationKindRank(b.Kind)
    || a.Source.localeCompare(b.Source)
    || a.Target.localeCompare(b.Target);
}

function aiTargetKey(kind, id) {
  return `${kind}:${id}`;
}

function aiSummaryCacheKey(kind, id) {
  const graphKey = state.graphFingerprint || state.storageKey || state.graphLabel || "sample";
  const providerKey = `${state.aiStatus.provider || state.aiConfig.provider}:${state.aiStatus.model || state.aiConfig.model}`;
  return `UnityCodeGraph:ai:v${AI_CACHE_VERSION}:${graphKey}:${providerKey}:${kind}:${id}:${state.language}`;
}

function readAiSummaryCache(kind, id) {
  try {
    const value = localStorage.getItem(aiSummaryCacheKey(kind, id));
    return value ? JSON.parse(value) : null;
  } catch {
    return null;
  }
}

function writeAiSummaryCache(kind, id, value) {
  try {
    localStorage.setItem(aiSummaryCacheKey(kind, id), JSON.stringify(value));
  } catch {
    // Cache failures should not block AI summaries.
  }
}

function selectedSystem() {
  if (!state.selectedSystemId && state.selected?.type !== "system") return null;
  const id = state.selected?.type === "system" ? state.selected.id : state.selectedSystemId;
  return (state.graph?.SystemClusters ?? []).find(cluster => cluster.Id === id) ?? null;
}

function systemEntriesHtml(cluster) {
  const byId = new Map((state.graph?.Methods ?? []).map(method => [method.Id, method]));
  const entries = (cluster.EntryMethodIds ?? [])
    .map(id => byId.get(id))
    .filter(Boolean);

  if (!entries.length) {
    return "<p class=\"muted-text\">No entry candidates detected for this cluster.</p>";
  }

  return entries.map(method => `
    <div class="example">
      <span>${escapeHtml(method.File)}:${method.Line}</span>
      <code>${escapeHtml(method.TypeId.split(".").pop())}.${escapeHtml(method.Signature)}</code>
    </div>
  `).join("");
}

function systemReportHtml(cluster) {
  const report = buildSystemReport(cluster);
  return `
    <div class="system-report">
      <section>
        <h3>Role Estimate</h3>
        <p>${escapeHtml(report.role)}</p>
      </section>
      <section>
        <h3>Main Types</h3>
        ${report.mainTypes.length ? report.mainTypes.map(item => `
          <div class="report-row">
            <strong>${escapeHtml(item.name)}</strong>
            <span>${escapeHtml(item.detail)}</span>
          </div>
        `).join("") : "<p class=\"muted-text\">No major types detected.</p>"}
      </section>
      <section>
        <h3>Entry Candidates</h3>
        ${report.entries.length ? report.entries.map(entry => `
          <div class="report-row">
            <strong>${escapeHtml(entry.name)}</strong>
            <span>${escapeHtml(entry.detail)}</span>
          </div>
        `).join("") : "<p class=\"muted-text\">No entry candidates detected.</p>"}
      </section>
      <section>
        <h3>Likely Flows</h3>
        ${report.flows.length ? report.flows.map(flow => `
          <div class="report-flow">
            <strong>${escapeHtml(flow.entry)}</strong>
            <ol>${flow.steps.map(step => `<li>${escapeHtml(step)}</li>`).join("")}</ol>
          </div>
        `).join("") : "<p class=\"muted-text\">No internal method flow detected.</p>"}
      </section>
      <section>
        <h3>External Touchpoints</h3>
        ${report.external.length ? report.external.map(item => `
          <div class="report-row">
            <strong>${escapeHtml(item.name)}</strong>
            <span>${escapeHtml(item.detail)}</span>
          </div>
        `).join("") : "<p class=\"muted-text\">No external type touchpoints detected.</p>"}
      </section>
    </div>
  `;
}

function buildSystemReport(cluster) {
  const nodes = state.graph?.Nodes ?? [];
  const edges = state.graph?.Edges ?? [];
  const methods = state.graph?.Methods ?? [];
  const methodEdges = state.graph?.MethodEdges ?? [];
  const nodeById = new Map(nodes.map(node => [node.Id, node]));
  const methodById = new Map(methods.map(method => [method.Id, method]));
  const clusterIds = new Set(cluster.NodeIds ?? []);
  const clusterNodes = [...clusterIds].map(id => nodeById.get(id)).filter(Boolean);

  const degree = new Map(clusterNodes.map(node => [node.Id, { in: 0, out: 0, internal: 0, external: 0 }]));
  const external = new Map();
  for (const edge of edges) {
    const sourceIn = clusterIds.has(edge.Source);
    const targetIn = clusterIds.has(edge.Target);
    if (!sourceIn && !targetIn) continue;

    if (sourceIn) {
      const item = degree.get(edge.Source);
      if (item) {
        item.out += edge.Weight ?? 1;
        if (targetIn) item.internal += edge.Weight ?? 1;
        else item.external += edge.Weight ?? 1;
      }
    }
    if (targetIn) {
      const item = degree.get(edge.Target);
      if (item) {
        item.in += edge.Weight ?? 1;
        if (sourceIn) item.internal += edge.Weight ?? 1;
        else item.external += edge.Weight ?? 1;
      }
    }
    if (sourceIn !== targetIn) {
      const outsideId = sourceIn ? edge.Target : edge.Source;
      const outsideNode = nodeById.get(outsideId);
      if (outsideNode) {
        const current = external.get(outsideId) ?? { node: outsideNode, weight: 0, kinds: new Set() };
        current.weight += edge.Weight ?? 1;
        current.kinds.add(edge.Kind);
        external.set(outsideId, current);
      }
    }
  }

  const mainTypes = clusterNodes
    .map(node => ({ node, stat: degree.get(node.Id) ?? { in: 0, out: 0, internal: 0, external: 0 } }))
    .sort((a, b) =>
      (b.stat.internal + b.stat.external + b.stat.in + b.stat.out) -
      (a.stat.internal + a.stat.external + a.stat.in + a.stat.out) ||
      a.node.Name.localeCompare(b.node.Name)
    )
    .slice(0, 8)
    .map(item => ({
      name: item.node.Name,
      detail: `${item.node.Kind}${item.node.IsUnityType ? " / Unity" : ""} / ${item.stat.out} out / ${item.stat.in} in`
    }));

  const entries = (cluster.EntryMethodIds ?? [])
    .map(id => methodById.get(id))
    .filter(Boolean)
    .slice(0, 8)
    .map(method => ({
      name: methodLabel(method),
      detail: `${method.EntryKind || "candidate"} / ${shortFile(method.File)}:${method.Line}`
    }));

  const outgoingMethods = new Map();
  for (const edge of methodEdges) {
    const source = methodById.get(edge.Source);
    const target = methodById.get(edge.Target);
    if (!source || !target) continue;
    if (!clusterIds.has(source.TypeId) || !clusterIds.has(target.TypeId)) continue;
    if (!outgoingMethods.has(edge.Source)) outgoingMethods.set(edge.Source, []);
    outgoingMethods.get(edge.Source).push(edge);
  }

  const flows = (cluster.EntryMethodIds ?? [])
    .map(id => methodById.get(id))
    .filter(Boolean)
    .slice(0, 4)
    .map(method => summarizeFlow(method, outgoingMethods, methodById))
    .filter(flow => flow.steps.length);

  return {
    role: roleEstimate(cluster, clusterNodes),
    mainTypes,
    entries,
    flows,
    external: [...external.values()]
      .sort((a, b) => b.weight - a.weight || a.node.Name.localeCompare(b.node.Name))
      .slice(0, 8)
      .map(item => ({
        name: item.node.Name,
        detail: `${item.weight} refs / ${[...item.kinds].map(formatKind).join(", ")}`
      }))
  };
}

function roleEstimate(cluster, nodes) {
  const keywords = (cluster.Keywords ?? []).slice(0, 5);
  const unityCount = nodes.filter(node => node.IsUnityType).length;
  const plainCount = Math.max(0, nodes.length - unityCount);
  const density = cluster.InternalEdges > cluster.ExternalEdges ? "internally dense" : "externally connected";
  const unityText = unityCount ? `${unityCount} Unity-facing types` : `${plainCount} plain C# types`;
  return `${cluster.Name} appears to be an ${density} area around ${keywords.join(", ") || "shared code"}. It contains ${nodes.length} types, including ${unityText}.`;
}

function summarizeFlow(entry, outgoing, byId) {
  const steps = [];
  const seen = new Set();
  let current = entry;
  for (let depth = 0; depth < 6; depth++) {
    if (!current || seen.has(current.Id)) {
      if (current) steps.push(`${methodLabel(current)} / cycle`);
      break;
    }
    seen.add(current.Id);
    const nextEdges = (outgoing.get(current.Id) ?? [])
      .sort((a, b) => (b.Weight ?? 1) - (a.Weight ?? 1) || (byId.get(a.Target)?.Line ?? 0) - (byId.get(b.Target)?.Line ?? 0));
    const status = nextEdges.length ? "" : " / terminal";
    steps.push(`${methodLabel(current)}${status}`);
    current = nextEdges.length ? byId.get(nextEdges[0].Target) : null;
  }
  if (current && steps.length >= 6) steps.push("continues...");
  return { entry: methodLabel(entry), steps };
}

function methodLabel(method) {
  return `${method.TypeId.split(".").pop()}.${method.Signature}`;
}

function shortFile(file) {
  return String(file ?? "").replaceAll("\\", "/").split("/").slice(-2).join("/");
}

function renderFlowTrace(typeId = state.selected?.type === "node" ? state.selected.id : "") {
  const methods = state.graph?.Methods ?? [];
  const methodEdges = state.graph?.MethodEdges ?? [];
  if (!typeId || !methods.length) {
    els.entrySelect.innerHTML = "";
    els.entrySelect.disabled = true;
    els.flowDepth.disabled = true;
    els.flowList.innerHTML = "<p class=\"muted-text\">Select a class to inspect possible execution flows.</p>";
    return;
  }

  const typeMethods = methods.filter(method => method.TypeId === typeId);
  const entryMethods = typeMethods
    .filter(method => method.IsEntryPoint)
    .sort((a, b) => entryRank(a) - entryRank(b) || a.Line - b.Line);
  const options = (entryMethods.length ? entryMethods : typeMethods).slice(0, 40);

  if (!options.length) {
    els.entrySelect.innerHTML = "";
    els.entrySelect.disabled = true;
    els.flowDepth.disabled = true;
    els.flowList.innerHTML = "<p class=\"muted-text\">No methods found for this type.</p>";
    return;
  }

  els.entrySelect.disabled = false;
  els.flowDepth.disabled = false;
  if (!state.selectedEntry || !options.some(method => method.Id === state.selectedEntry)) {
    state.selectedEntry = options[0].Id;
  }

  els.entrySelect.innerHTML = options.map(method =>
    `<option value="${escapeHtml(method.Id)}" ${method.Id === state.selectedEntry ? "selected" : ""}>${escapeHtml(method.Signature)}${method.EntryKind ? " · " + escapeHtml(method.EntryKind) : ""}</option>`
  ).join("");

  const byId = new Map(methods.map(method => [method.Id, method]));
  const outgoing = new Map();
  for (const edge of methodEdges) {
    if (!outgoing.has(edge.Source)) outgoing.set(edge.Source, []);
    outgoing.get(edge.Source).push(edge);
  }

  const rows = [];
  const seen = new Set();
  walkFlow(state.selectedEntry, 0, Number(state.flowDepth), outgoing, byId, seen, rows);
  els.flowList.innerHTML = rows.length
    ? rows.map(row => flowRowHtml(row)).join("")
    : "<p class=\"muted-text\">No internal method calls detected from this entry.</p>";
}

function walkFlow(methodId, depth, maxDepth, outgoing, byId, seen, rows) {
  const method = byId.get(methodId);
  if (!method) return;
  const cycle = seen.has(methodId);
  const edges = (outgoing.get(methodId) ?? [])
    .sort((a, b) => (byId.get(a.Target)?.Line ?? 0) - (byId.get(b.Target)?.Line ?? 0))
    .slice(0, 12);
  rows.push({
    method,
    depth,
    cycle,
    terminal: !cycle && edges.length === 0,
    truncated: !cycle && edges.length > 0 && depth >= maxDepth
  });
  if (cycle || depth >= maxDepth) return;
  seen.add(methodId);
  for (const edge of edges) {
    walkFlow(edge.Target, depth + 1, maxDepth, outgoing, byId, new Set(seen), rows);
  }
}

function flowRowHtml(row) {
  const method = row.method;
  const shortType = method.TypeId.split(".").pop();
  const cycle = row.cycle ? " cycle" : "";
  const status = row.cycle ? "cycle" : row.terminal ? "terminal" : row.truncated ? "continues" : "";
  return `
    <div class="flow-row${cycle}" style="--depth:${row.depth}">
      <div class="flow-line"></div>
      <div>
        <strong>${escapeHtml(shortType)}.${escapeHtml(method.Signature)}</strong>
        ${status ? `<span class="flow-status">${escapeHtml(status)}</span>` : ""}
        <span>${escapeHtml(method.File)}:${method.Line}${row.cycle ? " · cycle" : ""}</span>
      </div>
    </div>
  `;
}

function entryRank(method) {
  if (method.EntryKind === "unity_lifecycle") return 0;
  if (method.EntryKind === "flow_candidate") return 1;
  return 2;
}

function setDetailRows(rows) {
  els.detailList.innerHTML = rows.map(([key, value]) => `
    <div>
      <dt>${escapeHtml(String(key))}</dt>
      <dd>${escapeHtml(String(value))}</dd>
    </div>
  `).join("");
}

function appendDetailRow(key, value) {
  const row = document.createElement("div");
  row.innerHTML = `
    <dt>${escapeHtml(String(key))}</dt>
    <dd>${escapeHtml(String(value))}</dd>
  `;
  els.detailList.appendChild(row);
}

function renderExamples(examples) {
  els.examples.innerHTML = examples.length
    ? examples.map(example => `
      <div class="example">
        <span>${escapeHtml(example.File)}:${example.Line}</span>
        <code>${escapeHtml(example.Text ?? "")}</code>
      </div>
    `).join("")
    : "<p>No examples recorded.</p>";
}

function codeCallSummaryHtml(node) {
  const methods = state.graph?.Methods ?? [];
  const methodEdges = state.graph?.MethodEdges ?? [];
  const edges = state.graph?.Edges ?? [];
  const methodById = new Map(methods.map(method => [method.Id, method]));
  const typeById = new Map((state.graph?.Nodes ?? []).map(item => [item.Id, item]));

  const outgoingCalls = methodEdges
    .map(edge => ({
      edge,
      source: methodById.get(edge.Source),
      target: methodById.get(edge.Target)
    }))
    .filter(item => item.source?.TypeId === node.Id && item.target?.TypeId !== node.Id)
    .sort(callSummarySort)
    .slice(0, 8);

  const incomingCalls = methodEdges
    .map(edge => ({
      edge,
      source: methodById.get(edge.Source),
      target: methodById.get(edge.Target)
    }))
    .filter(item => item.target?.TypeId === node.Id && item.source?.TypeId !== node.Id)
    .sort(callSummarySort)
    .slice(0, 6);

  const typeRelations = edges
    .filter(edge => edge.Source === node.Id && codeSummaryKinds.has(edge.Kind))
    .sort((a, b) => relationKindRank(a.Kind) - relationKindRank(b.Kind)
      || (b.Weight ?? 1) - (a.Weight ?? 1)
      || typeName(typeById.get(a.Target) ?? { Id: a.Target }).localeCompare(typeName(typeById.get(b.Target) ?? { Id: b.Target })))
    .slice(0, 6);

  const blocks = [
    callBlockHtml("Calls Out", outgoingCalls, item => callLineHtml(item.source, item.target, item.edge, "out")),
    callBlockHtml("Called By", incomingCalls, item => callLineHtml(item.source, item.target, item.edge, "in")),
    relationBlockHtml(typeRelations, typeById)
  ].filter(Boolean);

  return blocks.length
    ? `<div class="code-call-summary">${blocks.join("")}</div>`
    : "<p class=\"muted-text\">No cross-file calls detected for this type.</p>";
}

const codeSummaryKinds = new Set([
  "calls_member",
  "creates",
  "unity_get_component",
  "unity_try_get_component",
  "unity_add_component",
  "unity_find_object",
  "unity_create_scriptable_object"
]);

function callBlockHtml(title, items, row) {
  if (!items.length) return "";
  return `
    <section class="call-block">
      <h3>${escapeHtml(title)}</h3>
      ${items.map(row).join("")}
    </section>
  `;
}

function relationBlockHtml(relations, typeById) {
  if (!relations.length) return "";
  return `
    <section class="call-block">
      <h3>Type Touchpoints</h3>
      ${relations.map(edge => {
        const target = typeById.get(edge.Target);
        const example = edge.Examples?.[0];
        return `
          <div class="call-line">
            <strong>${escapeHtml(formatKind(edge.Kind))} ${escapeHtml(typeName(target ?? { Id: edge.Target }))}</strong>
            <span>${escapeHtml(example ? `${shortFile(example.File)}:${example.Line}` : `${edge.Weight ?? 1} refs`)}</span>
          </div>
        `;
      }).join("")}
    </section>
  `;
}

function callLineHtml(source, target, edge, direction) {
  const example = edge.Examples?.[0];
  const left = direction === "out" ? compactMethodLabel(source) : compactMethodLabel(target);
  const right = direction === "out" ? compactMethodLabel(target, true) : compactMethodLabel(source, true);
  const arrow = direction === "out" ? "-&gt;" : "&lt;-";
  return `
    <div class="call-line">
      <strong>${escapeHtml(left)} <span>${arrow}</span> ${escapeHtml(right)}</strong>
      <span>${escapeHtml(example ? `${shortFile(example.File)}:${example.Line}` : `${edge.Weight ?? 1} calls`)}</span>
    </div>
  `;
}

function callSummarySort(a, b) {
  return (b.edge.Weight ?? 1) - (a.edge.Weight ?? 1)
    || (a.source?.Line ?? 0) - (b.source?.Line ?? 0)
    || (a.target?.Line ?? 0) - (b.target?.Line ?? 0);
}

function relationKindRank(kind) {
  return [
    "calls_member",
    "creates",
    "unity_get_component",
    "unity_try_get_component",
    "unity_add_component",
    "unity_find_object",
    "unity_create_scriptable_object"
  ].indexOf(kind);
}

function compactMethodLabel(method, includeType = false) {
  if (!method) return "unknown";
  const signature = String(method.Signature ?? method.Name ?? "").replace(/\s+/g, " ");
  return includeType ? `${typeName({ Id: method.TypeId })}.${signature}` : signature;
}

function typeName(node) {
  return String(node?.Name || node?.Id || "")
    .split(".")
    .pop()
    .split("+")
    .pop();
}

function fitToView() {
  const items = state.viewMode === "system"
    ? filteredSystemClusters().map(cluster => ({ id: cluster.Id, width: SYSTEM_WIDTH, height: SYSTEM_HEIGHT }))
    : getVisibleNodes().map(node => ({ id: node.Id, width: NODE_WIDTH, height: NODE_HEIGHT }));
  const positions = state.viewMode === "system"
    ? layoutSystems(filteredSystemClusters())
    : state.positions;

  if (!items.length) return;
  const bounds = items.reduce((box, item) => {
    const p = positions.get(item.id) ?? { x: 0, y: 0 };
    return {
      minX: Math.min(box.minX, p.x),
      minY: Math.min(box.minY, p.y),
      maxX: Math.max(box.maxX, p.x + item.width),
      maxY: Math.max(box.maxY, p.y + item.height)
    };
  }, { minX: Infinity, minY: Infinity, maxX: -Infinity, maxY: -Infinity });

  const rect = els.svg.getBoundingClientRect();
  const padding = 80;
  const scale = Math.min(
    1.2,
    (rect.width - padding) / Math.max(1, bounds.maxX - bounds.minX),
    (rect.height - padding) / Math.max(1, bounds.maxY - bounds.minY)
  );
  state.transform.scale = Math.max(0.25, scale);
  state.transform.x = (rect.width - (bounds.minX + bounds.maxX) * state.transform.scale) / 2;
  state.transform.y = (rect.height - (bounds.minY + bounds.maxY) * state.transform.scale) / 2;
  render();
  scheduleSaveViewState();
}

function applyTransform() {
  const { x, y, scale } = state.transform;
  els.viewport.setAttribute("transform", `translate(${x}, ${y}) scale(${scale})`);
}

function onWheel(event) {
  event.preventDefault();
  const before = screenToWorld(event);
  const factor = event.deltaY < 0 ? 1.1 : 0.9;
  state.transform.scale = clamp(state.transform.scale * factor, 0.18, 2.5);
  const after = screenToWorld(event);
  state.transform.x += (after.x - before.x) * state.transform.scale;
  state.transform.y += (after.y - before.y) * state.transform.scale;
  render();
  scheduleSaveViewState();
}

function onPointerDown(event) {
  state.pan = { x: event.clientX, y: event.clientY, startX: state.transform.x, startY: state.transform.y };
  els.svg.classList.add("is-panning");
  if (!state.pinView) {
    state.selected = null;
    render();
  }
}

function onPointerMove(event) {
  if (resizeExplainDrawer(event)) return;

  if (state.drag) {
    const point = screenToWorld(event);
    state.positions.set(state.drag.id, { x: point.x - state.drag.dx, y: point.y - state.drag.dy });
    render();
    return;
  }

  if (state.pan) {
    state.transform.x = state.pan.startX + event.clientX - state.pan.x;
    state.transform.y = state.pan.startY + event.clientY - state.pan.y;
    render();
  }
}

function onPointerUp() {
  if (endExplainResize()) return;

  const shouldSave = Boolean(state.drag || state.pan);
  state.drag = null;
  state.pan = null;
  els.svg.classList.remove("is-panning");
  if (shouldSave) {
    scheduleSaveViewState();
  }
}

function selectNodeForInteraction(nodeId) {
  if (state.pinView && state.selected) return;
  state.workflowFocus = null;
  state.selected = { type: "node", id: nodeId };
  state.edgeMode = "selected";
  els.edgeMode.value = state.edgeMode;
  state.selectedEntry = "";
  state.aiError = "";
}

function focusWorkflowReference(button) {
  const kind = button.dataset.workflowFocus;
  if (kind === "method") {
    focusWorkflowNode(button.dataset.nodeId, button.dataset.methodId);
    return;
  }
  if (kind === "edge") {
    focusWorkflowEdge(button.dataset.sourceId, button.dataset.targetId, button.dataset.edgeKind || "");
    return;
  }
  focusWorkflowNode(button.dataset.nodeId, "");
}

function focusWorkflowNode(nodeId, methodId = "") {
  if (!state.graph || !nodeId) return;
  const node = state.graph.Nodes.find(item => item.Id === nodeId);
  if (!node) return;

  prepareTypeViewForWorkflowFocus([nodeId]);
  state.selected = { type: "node", id: nodeId };
  state.edgeMode = "selected";
  state.selectedEntry = methodId || "";
  state.workflowFocus = { kind: methodId ? "method" : "node", nodeId, methodId };
  els.edgeMode.value = state.edgeMode;
  renderSystems();
  render();
  centerWorkflowOnNode(nodeId);
  scheduleSaveViewState();
}

function focusWorkflowEdge(sourceId, targetId, edgeKind = "") {
  if (!state.graph || !sourceId || !targetId) return;
  const edge = (state.graph.Edges ?? []).find(item =>
    item.Source === sourceId &&
    item.Target === targetId &&
    (!edgeKind || item.Kind === edgeKind)
  );
  if (!edge) return;

  prepareTypeViewForWorkflowFocus([edge.Source, edge.Target]);
  state.enabledKinds.add(edge.Kind);
  state.selected = { type: "edge", key: edgeKey(edge) };
  state.edgeMode = "all";
  state.selectedEntry = "";
  state.workflowFocus = { kind: "edge", source: edge.Source, target: edge.Target, edgeKind: edge.Kind };
  els.edgeMode.value = state.edgeMode;
  renderSystems();
  render();
  centerWorkflowOnEdge(edge);
  scheduleSaveViewState();
}

function prepareTypeViewForWorkflowFocus(nodeIds) {
  state.viewMode = "type";
  state.search = "";
  els.viewMode.value = state.viewMode;
  els.search.value = state.search;

  const currentSystem = selectedSystem();
  if (currentSystem) {
    const systemIds = new Set(currentSystem.NodeIds ?? []);
    if (nodeIds.some(id => !systemIds.has(id))) {
      state.selectedSystemId = "";
    }
  }
}

function centerWorkflowOnNode(nodeId) {
  const point = centerOf(nodeId);
  if (!point) return;
  centerWorkflowOnPoint(point);
  render();
}

function centerWorkflowOnEdge(edge) {
  const source = centerOf(edge.Source);
  const target = centerOf(edge.Target);
  if (!source || !target) return;
  centerWorkflowOnPoint({
    x: (source.x + target.x) / 2,
    y: (source.y + target.y) / 2
  });
  render();
}

function centerWorkflowOnPoint(point) {
  const rect = els.svg.getBoundingClientRect();
  state.transform.scale = clamp(Math.max(state.transform.scale, 0.85), 0.18, 2.5);
  state.transform.x = rect.width / 2 - point.x * state.transform.scale;
  state.transform.y = rect.height / 2 - point.y * state.transform.scale;
}

function syncPinModeButton() {
  els.pinMode.classList.toggle("is-active", state.pinView);
  els.pinMode.setAttribute("aria-pressed", String(state.pinView));
  els.pinMode.textContent = state.pinView ? "Pinned" : "Pin View";
}

function screenToWorld(event) {
  const rect = els.svg.getBoundingClientRect();
  return {
    x: (event.clientX - rect.left - state.transform.x) / state.transform.scale,
    y: (event.clientY - rect.top - state.transform.y) / state.transform.scale
  };
}

function centerOf(id) {
  const p = state.positions.get(id);
  if (!p) return null;
  return { x: p.x + NODE_WIDTH / 2, y: p.y + NODE_HEIGHT / 2 };
}

function edgePath(source, target, edge) {
  const sourceRight = { x: source.x + NODE_WIDTH / 2 - 8, y: source.y };
  const targetLeft = { x: target.x - NODE_WIDTH / 2 + 8, y: target.y };
  if (Math.abs(source.x - target.x) < NODE_WIDTH) {
    const offset = edgeOffset(edge);
    return `M ${source.x} ${source.y} C ${source.x + offset} ${source.y - 70}, ${target.x + offset} ${target.y + 70}, ${target.x} ${target.y}`;
  }
  const start = source.x <= target.x ? sourceRight : { x: source.x - NODE_WIDTH / 2 + 8, y: source.y };
  const end = source.x <= target.x ? targetLeft : { x: target.x + NODE_WIDTH / 2 - 8, y: target.y };
  const dx = Math.max(92, Math.abs(end.x - start.x) * 0.42);
  const dir = start.x <= end.x ? 1 : -1;
  return `M ${start.x} ${start.y} C ${start.x + dx * dir} ${start.y}, ${end.x - dx * dir} ${end.y}, ${end.x} ${end.y}`;
}

function edgeOffset(edge) {
  let hash = 0;
  const key = edgeKey(edge);
  for (let i = 0; i < key.length; i++) hash = (hash * 31 + key.charCodeAt(i)) | 0;
  return 70 + Math.abs(hash % 90);
}

function edgeKey(edge) {
  return `${edge.Source}|${edge.Target}|${edge.Kind}`;
}

function isSelected(edge) {
  return state.selected?.type === "edge" && state.selected.key === edgeKey(edge);
}

function isWorkflowFocusedEdge(edge) {
  const focus = state.workflowFocus;
  return focus?.kind === "edge" &&
    focus.source === edge.Source &&
    focus.target === edge.Target &&
    (!focus.edgeKind || focus.edgeKind === edge.Kind);
}

function isWorkflowFocusedNode(nodeId) {
  const focus = state.workflowFocus;
  if (!focus) return false;
  if (focus.kind === "node" || focus.kind === "method") return focus.nodeId === nodeId;
  if (focus.kind === "edge") return focus.source === nodeId || focus.target === nodeId;
  return false;
}

function accentClass(node) {
  if (node.IsUnityType) return "node-accent-unity";
  return `node-accent-${node.Kind ?? "class"}`;
}

function chipWidth(node) {
  return Math.max(58, (node.IsUnityType ? 5 : String(node.Kind ?? "type").length) * 8 + 24);
}

function shortNamespace(namespace) {
  if (!namespace) return "global";
  const parts = namespace.split(".");
  return parts.length > 2 ? `${parts[0]}.${parts.at(-1)}` : namespace;
}

function formatKind(kind) {
  return String(kind).replaceAll("_", " ");
}

function countBy(items, keyFn) {
  const counts = new Map();
  for (const item of items) {
    const key = keyFn(item);
    counts.set(key, (counts.get(key) ?? 0) + 1);
  }
  return counts;
}

function clamp(value, min, max) {
  return Math.min(max, Math.max(min, value));
}

function validOption(select, value, fallback) {
  return [...select.options].some(option => option.value === value) ? value : fallback;
}

function isFiniteNumber(value) {
  return typeof value === "number" && Number.isFinite(value);
}

function containsWorkflowTerm(text, term) {
  if (!term) return false;
  const escaped = escapeRegExp(String(term).trim());
  if (!escaped) return false;
  return new RegExp(`(^|[^A-Za-z0-9_])${escaped}([^A-Za-z0-9_]|$)`, "i").test(text);
}

function containsWorkflowEdge(text, sourceName, targetName) {
  const source = escapeRegExp(sourceName);
  const target = escapeRegExp(targetName);
  if (!source || !target) return false;
  const arrowPattern = "(?:->|→|=>|calls|uses|references|invokes|호출|사용|참조|연결)";
  const forward = new RegExp(`${source}.{0,80}${arrowPattern}.{0,80}${target}`, "i");
  const backward = new RegExp(`${target}.{0,80}(?:<-|←).{0,80}${source}`, "i");
  return forward.test(text) || backward.test(text);
}

function escapeRegExp(value) {
  return String(value ?? "").replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function hashString(value) {
  let hash = 2166136261;
  for (let i = 0; i < value.length; i++) {
    hash ^= value.charCodeAt(i);
    hash = Math.imul(hash, 16777619);
  }
  return (hash >>> 0).toString(36);
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}
