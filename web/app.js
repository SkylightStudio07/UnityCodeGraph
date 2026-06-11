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
  sample: document.getElementById("sampleButton"),
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
  entrySelect: document.getElementById("entrySelect"),
  flowDepth: document.getElementById("flowDepthSelect"),
  flowList: document.getElementById("flowList"),
  empty: document.getElementById("emptyState")
};

init();

async function init() {
  bindEvents();
  await loadSampleGraph();
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
    loadGraph(graph, file.name);
  });

  els.sample.addEventListener("click", loadSampleGraph);
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
    els.viewMode.value = state.viewMode;
    els.group.value = state.groupBy;
    els.edgeMode.value = state.edgeMode;
    els.neighborhoodDepth.value = String(state.neighborhoodDepth);
    els.neighborhoodDirection.value = state.neighborhoodDirection;
    state.enabledKinds = new Set(state.edgeKinds);
    state.selected = null;
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
  });

  els.group.addEventListener("change", () => {
    state.groupBy = els.group.value;
    relayout();
    render();
    fitToView();
  });

  els.edgeMode.addEventListener("change", () => {
    state.edgeMode = els.edgeMode.value;
    render();
  });

  els.neighborhoodDepth.addEventListener("change", () => {
    state.neighborhoodDepth = Number(els.neighborhoodDepth.value);
    render();
    if (state.edgeMode === "selected" && state.selected?.type === "node") fitToView();
  });

  els.neighborhoodDirection.addEventListener("change", () => {
    state.neighborhoodDirection = els.neighborhoodDirection.value;
    render();
    if (state.edgeMode === "selected" && state.selected?.type === "node") fitToView();
  });

  els.entrySelect.addEventListener("change", () => {
    state.selectedEntry = els.entrySelect.value;
    renderFlowTrace();
  });

  els.flowDepth.addEventListener("change", () => {
    state.flowDepth = Number(els.flowDepth.value);
    renderFlowTrace();
  });

  els.svg.addEventListener("wheel", onWheel, { passive: false });
  els.svg.addEventListener("pointerdown", onPointerDown);
  window.addEventListener("pointermove", onPointerMove);
  window.addEventListener("pointerup", onPointerUp);
}

async function loadSampleGraph() {
  try {
    const response = await fetch(SAMPLE_URL);
    if (!response.ok) throw new Error("sample unavailable");
    loadGraph(await response.json(), "samples/mini-graph.json");
  } catch {
    loadGraph(sampleGraph, "embedded sample");
  }
}

function loadGraph(graph, label) {
  const normalized = {
    ...graph,
    Nodes: graph.Nodes ?? graph.nodes ?? [],
    Edges: graph.Edges ?? graph.edges ?? [],
    Methods: graph.Methods ?? graph.methods ?? [],
    MethodEdges: graph.MethodEdges ?? graph.methodEdges ?? [],
    SystemClusters: graph.SystemClusters ?? graph.systemClusters ?? []
  };
  state.graph = normalized;
  state.edgeKinds = new Set(normalized.Edges.map(edge => edge.Kind));
  state.enabledKinds = new Set(state.edgeKinds);
  const layout = layoutGraph(normalized, state.groupBy);
  state.positions = layout.positions;
  state.sections = layout.sections;
  state.selected = null;
  els.subtitle.textContent = `${label} · ${normalized.Nodes.length} types · ${normalized.Edges.length} relationships`;
  renderFilters();
  renderSystems();
  render();
  requestAnimationFrame(() => fitToView());
}

function relayout() {
  if (!state.graph) return;
  const layout = layoutGraph(state.graph, state.groupBy);
  state.positions = layout.positions;
  state.sections = layout.sections;
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
  els.empty.hidden = visibleNodes.length > 0;

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
  els.empty.hidden = clusters.length > 0;

  applyTransform();
  clearSections();
  renderSystemEdges(edges, positions);
  renderSystemNodes(clusters, positions);
  renderDetails();
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
    path.setAttribute("class", `edge ${isSelected(edge) ? "is-selected" : ""}`);
    path.setAttribute("d", edgePath(source, target, edge));
    path.dataset.edgeKey = edgeKey(edge);
    path.addEventListener("click", (event) => {
      event.stopPropagation();
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
    group.setAttribute("class", `node ${selected ? "is-selected" : ""}`);
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
      state.selected = { type: "node", id: node.Id };
      state.edgeMode = "selected";
      els.edgeMode.value = state.edgeMode;
      state.selectedEntry = "";
      render();
    });

    group.addEventListener("click", (event) => {
      event.stopPropagation();
      state.selected = { type: "node", id: node.Id };
      state.edgeMode = "selected";
      els.edgeMode.value = state.edgeMode;
      state.selectedEntry = "";
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
    els.secondaryTitle.textContent = "Examples";
    els.examples.innerHTML = "";
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
  renderFlowTrace();
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
}

function onPointerDown(event) {
  state.pan = { x: event.clientX, y: event.clientY, startX: state.transform.x, startY: state.transform.y };
  els.svg.classList.add("is-panning");
  state.selected = null;
  render();
}

function onPointerMove(event) {
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
  state.drag = null;
  state.pan = null;
  els.svg.classList.remove("is-panning");
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

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}
