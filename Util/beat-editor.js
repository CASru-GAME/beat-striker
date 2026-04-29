(function () {
  const S = window.BeatEditorState;
  const A = window.BeatEditorAudio;
  const T = window.BeatEditorTimeline;
  const { state, clamp, duration, minViewStart, maxViewStart, normalizeTime, parseBeatsText, serializeBeats, sortAndMergeNearby, buildManualNearestAssignments } = S;

  const audioInput = document.getElementById("audioInput");
  const beatsInput = document.getElementById("beatsInput");
  const manualInput = document.getElementById("manualInput");
  const loadAudioBtn = document.getElementById("loadAudioBtn");
  const loadBeatsBtn = document.getElementById("loadBeatsBtn");
  const playBtn = document.getElementById("playBtn");
  const stopBtn = document.getElementById("stopBtn");
  const beatSeToggle = document.getElementById("beatSeToggle");
  const addAtPlayheadBtn = document.getElementById("addAtPlayheadBtn");
  const deleteSelectedBtn = document.getElementById("deleteSelectedBtn");
  const sortMergeBtn = document.getElementById("sortMergeBtn");
  const compareBtn = document.getElementById("compareBtn");
  const downloadBtn = document.getElementById("downloadBtn");
  const exportManualBtn = document.getElementById("exportManualBtn");
  const importManualBtn = document.getElementById("importManualBtn");
  const undoManualBtn = document.getElementById("undoManualBtn");
  const clearManualBtn = document.getElementById("clearManualBtn");
  const insertModeBtn = document.getElementById("insertModeBtn");
  const snapBtn = document.getElementById("snapBtn");
  const applySelectedBtn = document.getElementById("applySelectedBtn");
  const warningThresholdInput = document.getElementById("warningThresholdInput");
  const manualWarningThresholdInput = document.getElementById("manualWarningThresholdInput");
  const assignmentMinRelativeInput = document.getElementById("assignmentMinRelativeInput");
  const assignmentMaxRelativeInput = document.getElementById("assignmentMaxRelativeInput");
  const nudgeWarningsBtn = document.getElementById("nudgeWarningsBtn");
  const generateBpmInput = document.getElementById("generateBpmInput");
  const generateOffsetInput = document.getElementById("generateOffsetInput");
  const generateBeatsBtn = document.getElementById("generateBeatsBtn");
  const shiftSelectedInput = document.getElementById("shiftSelectedInput");
  const shiftSelectedBtn = document.getElementById("shiftSelectedBtn");
  const shiftAllInput = document.getElementById("shiftAllInput");
  const shiftAllBtn = document.getElementById("shiftAllBtn");
  const viewSpanInput = document.getElementById("viewSpanInput");
  const viewStartInput = document.getElementById("viewStartInput");
  const viewPositionSlider = document.getElementById("viewPositionSlider");
  const viewRangeLabel = document.getElementById("viewRangeLabel");
  const followPlayheadBtn = document.getElementById("followPlayheadBtn");
  const selectedBeatInput = document.getElementById("selectedBeatInput");
  const offsetMsInput = document.getElementById("offsetMsInput");
  const audioInfo = document.getElementById("audioInfo");
  const beatsInfo = document.getElementById("beatsInfo");
  const statusText = document.getElementById("statusText");
  const currentTimeLabel = document.getElementById("currentTimeLabel");
  const selectedBeatLabel = document.getElementById("selectedBeatLabel");
  const manualCountLabel = document.getElementById("manualCountLabel");
  const jumpNextAlertManualBtn = document.getElementById("jumpNextAlertManualBtn");
  const statsBox = document.getElementById("statsBox");
  const timeline = document.getElementById("timeline");
  const canvas = document.getElementById("timelineCanvas");

  let currentPlayheadSec = 0;
  let previousPlaybackSec = 0;
  let nextBeatSeIndex = 0;
  let isAreaSelecting = false;
  let areaSelectStartX = 0;
  let areaSelectCurrentX = 0;

  function resetBeatSeCursor(startSec) {
    nextBeatSeIndex = 0;
    while (nextBeatSeIndex < state.beats.length && state.beats[nextBeatSeIndex] < startSec - 0.0005) {
      nextBeatSeIndex++;
    }
    previousPlaybackSec = startSec;
  }

  function playBeatSeOnCrossedBeats(currentSec) {
    if (!state.isBeatSeEnabled || !state.beats.length) {
      previousPlaybackSec = currentSec;
      return;
    }
    if (currentSec < previousPlaybackSec - 0.001) {
      resetBeatSeCursor(currentSec);
      return;
    }
    while (nextBeatSeIndex < state.beats.length && state.beats[nextBeatSeIndex] <= currentSec + 0.0005) {
      if (state.beats[nextBeatSeIndex] >= previousPlaybackSec - 0.0005) {
        A.playBeatSe();
      }
      nextBeatSeIndex++;
    }
    previousPlaybackSec = currentSec;
  }

  function clearSelection() {
    state.selectedBeatIndex = -1;
    state.selectedBeatIndices = [];
  }

  function buildRangeSelection(anchorIndex, targetIndex) {
    if (anchorIndex < 0 || targetIndex < 0) return [];
    const start = Math.min(anchorIndex, targetIndex);
    const end = Math.max(anchorIndex, targetIndex);
    const values = [];
    for (let i = start; i <= end; i++) values.push(i);
    return values;
  }

  function makeSelectionFromIndices(indices) {
    const unique = [...new Set(indices)].filter((i) => i >= 0 && i < state.beats.length).sort((a, b) => a - b);
    state.selectedBeatIndices = unique;
    state.selectedBeatIndex = unique.length > 0 ? unique[0] : -1;
  }

  function resizeCanvas() {
    const dpr = window.devicePixelRatio || 1;
    const rect = timeline.getBoundingClientRect();
    canvas.width = Math.floor(rect.width * dpr);
    canvas.height = Math.floor(rect.height * dpr);
    const ctx = canvas.getContext("2d");
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    draw();
  }

  function getViewCenterSeconds() {
    return normalizeTime(state.viewStartSeconds + state.viewSpanSeconds / 2);
  }

  function syncViewPositionUI() {
    const minStart = minViewStart();
    const maxStart = maxViewStart();
    viewPositionSlider.min = minStart.toFixed(3);
    viewPositionSlider.max = maxStart.toFixed(3);
    viewPositionSlider.value = clamp(state.viewStartSeconds, minStart, maxStart).toFixed(3);
    viewPositionSlider.disabled = !state.audioBuffer;
    viewRangeLabel.textContent = `${getViewCenterSeconds().toFixed(2)}s / ${duration().toFixed(2)}s`;
  }

  function draw() {
    T.drawTimeline(canvas, null);
    if (isAreaSelecting) {
      const rect = canvas.getBoundingClientRect();
      const left = Math.min(areaSelectStartX, areaSelectCurrentX);
      const width = Math.abs(areaSelectCurrentX - areaSelectStartX);
      const ctx = canvas.getContext("2d");
      ctx.fillStyle = "rgba(100, 181, 255, 0.18)";
      ctx.strokeStyle = "rgba(100, 181, 255, 0.95)";
      ctx.lineWidth = 1.5;
      ctx.fillRect(left, 0, width, rect.height);
      ctx.strokeRect(left, 0, width, rect.height);
    }
    currentTimeLabel.textContent = getViewCenterSeconds().toFixed(3);
  }

  function updateSelectedBeatUI() {
    const selected = state.selectedBeatIndices.filter((i) => i >= 0 && i < state.beats.length).sort((a, b) => a - b);
    state.selectedBeatIndices = selected;
    state.selectedBeatIndex = selected.length > 0 ? selected[0] : -1;

    if (selected.length === 0) {
      selectedBeatLabel.textContent = "なし";
      selectedBeatInput.value = "";
      applySelectedBtn.disabled = true;
      deleteSelectedBtn.disabled = true;
      return;
    }
    if (selected.length === 1) {
      const sec = state.beats[selected[0]];
      selectedBeatLabel.textContent = `${selected[0] + 1} / ${state.beats.length} (${sec.toFixed(3)}s)`;
      selectedBeatInput.value = sec.toFixed(3);
      applySelectedBtn.disabled = false;
      deleteSelectedBtn.disabled = false;
      return;
    }
    selectedBeatLabel.textContent = `${selected.length}件選択`;
    selectedBeatInput.value = "";
    applySelectedBtn.disabled = true;
    deleteSelectedBtn.disabled = false;
  }

  function updateButtonStates() {
    const hasAudio = Boolean(state.audioBuffer);
    const hasBeats = state.beats.length > 0;
    const hasSelected = state.selectedBeatIndices.length > 0;
    const hasManual = state.manualBeats.length > 0;
    playBtn.disabled = !hasAudio;
    stopBtn.disabled = !hasAudio;
    addAtPlayheadBtn.disabled = !hasAudio;
    sortMergeBtn.disabled = !hasBeats;
    compareBtn.disabled = !hasAudio || !hasBeats || !hasManual;
    downloadBtn.disabled = !hasBeats;
    undoManualBtn.disabled = !hasManual;
    clearManualBtn.disabled = !hasManual;
    exportManualBtn.disabled = !hasManual;
    viewSpanInput.disabled = !hasAudio;
    viewStartInput.disabled = !hasAudio;
    viewPositionSlider.disabled = !hasAudio;
    followPlayheadBtn.disabled = !hasAudio;
    generateBeatsBtn.disabled = !hasAudio;
    shiftSelectedBtn.disabled = !hasSelected;
    shiftAllBtn.disabled = !hasBeats;
    nudgeWarningsBtn.disabled = collectOutlierTargets().length === 0;
    jumpNextAlertManualBtn.disabled = findNextAlertOrUnassignedManualTime() == null;
    manualCountLabel.textContent = String(state.manualBeats.length);
    viewSpanInput.value = state.viewSpanSeconds.toFixed(1);
    viewStartInput.value = state.viewStartSeconds.toFixed(2);
    syncViewPositionUI();
    followPlayheadBtn.textContent = `再生追従: ${state.followPlayhead ? "ON" : "OFF"}`;
    insertModeBtn.textContent = `挿入モード: ${state.isInsertModeEnabled ? "ON" : "OFF"}`;
    warningThresholdInput.value = state.warningThresholdSeconds.toFixed(3);
    manualWarningThresholdInput.value = state.manualWarningThresholdSeconds.toFixed(3);
    assignmentMinRelativeInput.value = state.assignmentMinRelative.toFixed(2);
    assignmentMaxRelativeInput.value = state.assignmentMaxRelative.toFixed(2);
    updateSelectedBeatUI();
    beatSeToggle.checked = state.isBeatSeEnabled;
  }

  function addBeatAt(sec) {
    state.beats.push(normalizeTime(sec));
    state.beats.sort((a, b) => a - b);
    const selected = state.beats.findIndex((v) => Math.abs(v - sec) < 0.0006);
    makeSelectionFromIndices(selected >= 0 ? [selected] : []);
    updateButtonStates();
    draw();
  }

  function generateBeatsFromBpm(bpm, offsetSeconds) {
    if (!state.audioBuffer || bpm <= 0) return;
    const beatInterval = 60 / bpm;
    const values = [];
    for (let sec = offsetSeconds; sec <= duration() + 0.0001; sec += beatInterval) {
      if (sec < 0) continue;
      values.push(normalizeTime(sec));
    }
    state.beats = [...new Set(values)].sort((a, b) => a - b);
    clearSelection();
  }

  function shiftBeats(indices, deltaSeconds) {
    if (!indices.length || !Number.isFinite(deltaSeconds)) return;
    for (const index of indices) {
      state.beats[index] = normalizeTime(state.beats[index] + deltaSeconds);
    }
    state.beats.sort((a, b) => a - b);
  }

  function finishDragSelection() {
    if (!state.isDraggingBeat) return;
    const selectedTimes = state.dragSelectionIndices.map((index) => state.beats[index]);
    state.beats.sort((a, b) => a - b);
    const newIndices = selectedTimes.map((time) => state.beats.findIndex((v) => Math.abs(v - time) < 0.0006));
    makeSelectionFromIndices(newIndices);
    state.isDraggingBeat = false;
    state.dragBeatIndex = -1;
    state.dragSelectionIndices = [];
  }

  function collectOutlierTargets() {
    const targets = [];
    const assignments = buildManualNearestAssignments();
    if (!assignments.length) return targets;

    const buckets = new Map();
    for (const item of assignments) {
      if (!buckets.has(item.beatIndex)) buckets.set(item.beatIndex, []);
      buckets.get(item.beatIndex).push(item.manual);
    }
    for (const [index, manuals] of buckets.entries()) {
      const beat = state.beats[index];
      const avg = manuals.reduce((a, b) => a + b, 0) / manuals.length;
      if (Math.abs(avg - beat) >= state.warningThresholdSeconds) {
        targets.push({ index, avg });
      }
    }
    return targets;
  }

  function updateSelectionFromArea(width) {
    const left = Math.min(areaSelectStartX, areaSelectCurrentX);
    const right = Math.max(areaSelectStartX, areaSelectCurrentX);
    const startSec = T.toSecondsFromX(left, width);
    const endSec = T.toSecondsFromX(right, width);
    const minSec = Math.min(startSec, endSec);
    const maxSec = Math.max(startSec, endSec);
    const indices = [];
    for (let i = 0; i < state.beats.length; i++) {
      const sec = state.beats[i];
      if (sec >= minSec && sec <= maxSec) indices.push(i);
    }
    makeSelectionFromIndices(indices);
  }

  function getNearestBeatDiff(sec) {
    if (!state.beats.length) return Number.POSITIVE_INFINITY;
    let min = Number.POSITIVE_INFINITY;
    for (const beat of state.beats) {
      const diff = Math.abs(sec - beat);
      if (diff < min) min = diff;
    }
    return min;
  }

  function findNextAlertOrUnassignedManualTime() {
    if (!state.manualBeats.length) return null;
    const windowEnd = state.viewStartSeconds + state.viewSpanSeconds;
    const manualWarningThreshold = state.manualWarningThresholdSeconds ?? 0.1;
    const assignments = buildManualNearestAssignments();
    const assignedIndices = new Set(assignments.map((item) => item.manualIndex));

    for (let i = 0; i < state.manualBeats.length; i++) {
      const sec = state.manualBeats[i];
      if (sec <= windowEnd) continue;
      if (!assignedIndices.has(i)) return sec;
      if (getNearestBeatDiff(sec) > manualWarningThreshold) return sec;
    }
    return null;
  }

  function setStatusFromFiles(audioFileName, beatsFileName) {
    if (audioFileName) audioInfo.textContent = `音声: ${audioFileName}`;
    if (beatsFileName) beatsInfo.textContent = `beats: ${beatsFileName} (${state.beats.length}件)`;
  }

  function downloadBeats() {
    const content = serializeBeats(state.beats);
    const blob = new Blob([content], { type: "text/plain;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = state.beatsFileName || "beats.txt";
    a.click();
    URL.revokeObjectURL(url);
  }

  function downloadManualBeats() {
    const content = serializeBeats(state.manualBeats);
    const blob = new Blob([content], { type: "text/plain;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    const audioName = audioInfo.textContent.replace(/^音声:\s*/, "").trim();
    const baseName = audioName && audioName !== "未読込" ? audioName.replace(/\.[^/.]+$/, "") : "manual";
    a.download = `${baseName}_manual.beats.txt`;
    a.click();
    URL.revokeObjectURL(url);
  }

  function computeStats() {
    if (!state.beats.length || !state.manualBeats.length) {
      statsBox.textContent = "統計: beats または 手打ちが不足しています。";
      return;
    }
    const diffs = state.manualBeats.map((m) => {
      let min = Number.POSITIVE_INFINITY;
      for (const b of state.beats) min = Math.min(min, Math.abs(m - b));
      return min;
    });
    const avg = diffs.reduce((a, b) => a + b, 0) / diffs.length;
    const max = Math.max(...diffs);
    const median = diffs.slice().sort((a, b) => a - b)[Math.floor(diffs.length / 2)];
    const under30ms = diffs.filter((d) => d <= 0.03).length;
    statsBox.textContent =
      `統計（手打ち -> 既存ビート最近傍）\n` +
      `件数: ${diffs.length}\n` +
      `平均ズレ: ${(avg * 1000).toFixed(1)} ms\n` +
      `中央値: ${(median * 1000).toFixed(1)} ms\n` +
      `最大ズレ: ${(max * 1000).toFixed(1)} ms\n` +
      `30ms以内: ${under30ms}/${diffs.length}`;
  }

  loadAudioBtn.addEventListener("click", () => audioInput.click());
  loadBeatsBtn.addEventListener("click", () => beatsInput.click());
  importManualBtn.addEventListener("click", () => manualInput.click());

  audioInput.addEventListener("change", async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    A.ensureAudioContext();
    await state.audioContext.resume();
    const arrayBuffer = await file.arrayBuffer();
    state.audioBuffer = await state.audioContext.decodeAudioData(arrayBuffer);
    state.playbackOffsetSeconds = 0;
    currentPlayheadSec = 0;
    T.setViewSpan(Math.min(5, duration()));
    T.setViewStart(0);
    setStatusFromFiles(file.name, "");
    updateButtonStates();
    draw();
    audioInput.value = "";
  });

  beatsInput.addEventListener("change", async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    const text = await file.text();
    state.beats = parseBeatsText(text);
    state.beatsFileName = file.name;
    clearSelection();
    setStatusFromFiles("", file.name);
    updateButtonStates();
    draw();
    beatsInput.value = "";
  });

  manualInput.addEventListener("change", async (e) => {
    const files = Array.from(e.target.files ?? []);
    if (!files.length) return;
    const manualBeats = [...state.manualBeats];
    for (const file of files) {
      const text = await file.text();
      manualBeats.push(...parseBeatsText(text));
    }
    state.manualBeats = manualBeats.sort((a, b) => a - b);
    updateButtonStates();
    draw();
    manualInput.value = "";
  });

  playBtn.addEventListener("click", async () => {
    if (state.isPlaying) {
      A.stopPlayback();
      statusText.textContent = "状態: 停止";
      playBtn.textContent = "▶ 再生";
      draw();
      return;
    }
    const startAt = getViewCenterSeconds();
    if (state.isBeatSeEnabled) {
      try {
        await A.ensureBeatSeLoaded();
      } catch (error) {
        console.error(error);
      }
    }
    state.playbackOffsetSeconds = startAt;
    currentPlayheadSec = startAt;
    resetBeatSeCursor(startAt);
    await A.startPlayback((sec) => {
      playBeatSeOnCrossedBeats(sec);
      currentPlayheadSec = sec;
      T.ensurePlayheadVisible(sec);
      statusText.textContent = "状態: 再生中（Spaceで記録）";
      playBtn.textContent = "⏸ 停止";
      updateButtonStates();
      draw();
    }, () => {
      currentPlayheadSec = 0;
      statusText.textContent = "状態: 停止";
      playBtn.textContent = "▶ 再生";
      updateButtonStates();
      draw();
    });
  });

  stopBtn.addEventListener("click", () => {
    state.playbackOffsetSeconds = 0;
    currentPlayheadSec = 0;
    A.stopPlayback();
    statusText.textContent = "状態: 停止";
    playBtn.textContent = "▶ 再生";
    previousPlaybackSec = 0;
    nextBeatSeIndex = 0;
    draw();
  });

  beatSeToggle.addEventListener("change", () => {
    state.isBeatSeEnabled = beatSeToggle.checked;
  });

  viewSpanInput.addEventListener("change", () => {
    if (!state.audioBuffer) return;
    const span = Number.parseFloat(viewSpanInput.value);
    if (!Number.isFinite(span)) return;
    T.setViewSpan(span);
    updateButtonStates();
    draw();
  });

  viewStartInput.addEventListener("change", () => {
    if (!state.audioBuffer) return;
    const start = Number.parseFloat(viewStartInput.value);
    if (!Number.isFinite(start)) return;
    T.setViewStart(start);
    updateButtonStates();
    draw();
  });

  viewPositionSlider.addEventListener("input", () => {
    if (!state.audioBuffer) return;
    const start = Number.parseFloat(viewPositionSlider.value);
    if (!Number.isFinite(start)) return;
    T.setViewStart(start);
    if (state.isPlaying) {
      const centered = getViewCenterSeconds();
      state.playbackOffsetSeconds = centered;
      currentPlayheadSec = centered;
    }
    updateButtonStates();
    draw();
  });

  followPlayheadBtn.addEventListener("click", () => {
    state.followPlayhead = !state.followPlayhead;
    updateButtonStates();
    draw();
  });

  addAtPlayheadBtn.addEventListener("click", () => addBeatAt(A.getCurrentPlaybackSeconds()));

  deleteSelectedBtn.addEventListener("click", () => {
    if (!state.selectedBeatIndices.length) return;
    const selectedSet = new Set(state.selectedBeatIndices);
    state.beats = state.beats.filter((_, index) => !selectedSet.has(index));
    clearSelection();
    updateButtonStates();
    draw();
  });

  sortMergeBtn.addEventListener("click", () => {
    sortAndMergeNearby(0.02);
    clearSelection();
    updateButtonStates();
    draw();
  });

  undoManualBtn.addEventListener("click", () => {
    state.manualBeats.pop();
    updateButtonStates();
    draw();
  });

  clearManualBtn.addEventListener("click", () => {
    state.manualBeats = [];
    updateButtonStates();
    draw();
  });

  compareBtn.addEventListener("click", computeStats);
  downloadBtn.addEventListener("click", downloadBeats);
  exportManualBtn.addEventListener("click", downloadManualBeats);
  generateBeatsBtn.addEventListener("click", () => {
    const bpm = Number.parseFloat(generateBpmInput.value);
    const offset = Number.parseFloat(generateOffsetInput.value);
    if (!Number.isFinite(bpm) || bpm <= 0) return;
    generateBeatsFromBpm(bpm, Number.isFinite(offset) ? offset : 0);
    state.beatsFileName = "";
    beatsInfo.textContent = `beats: BPM生成 (${state.beats.length}件)`;
    updateButtonStates();
    draw();
  });
  shiftSelectedBtn.addEventListener("click", () => {
    const delta = Number.parseFloat(shiftSelectedInput.value);
    if (!Number.isFinite(delta) || !state.selectedBeatIndices.length) return;
    const selectedTimes = state.selectedBeatIndices.map((i) => state.beats[i]);
    shiftBeats(state.selectedBeatIndices, delta);
    const newIndices = selectedTimes.map((time) => {
      const target = normalizeTime(time + delta);
      return state.beats.findIndex((v) => Math.abs(v - target) < 0.0006);
    });
    makeSelectionFromIndices(newIndices);
    updateButtonStates();
    draw();
  });
  shiftAllBtn.addEventListener("click", () => {
    const delta = Number.parseFloat(shiftAllInput.value);
    if (!Number.isFinite(delta) || !state.beats.length) return;
    const allIndices = state.beats.map((_, i) => i);
    shiftBeats(allIndices, delta);
    clearSelection();
    updateButtonStates();
    draw();
  });

  snapBtn.addEventListener("click", () => {
    state.isSnapEnabled = !state.isSnapEnabled;
    snapBtn.textContent = `スナップ: ${state.isSnapEnabled ? "ON" : "OFF"}`;
  });

  insertModeBtn.addEventListener("click", () => {
    state.isInsertModeEnabled = !state.isInsertModeEnabled;
    updateButtonStates();
  });

  warningThresholdInput.addEventListener("change", () => {
    const value = Number.parseFloat(warningThresholdInput.value);
    if (!Number.isFinite(value) || value < 0) return;
    state.warningThresholdSeconds = value;
    updateButtonStates();
    draw();
  });

  manualWarningThresholdInput.addEventListener("change", () => {
    const value = Number.parseFloat(manualWarningThresholdInput.value);
    if (!Number.isFinite(value) || value < 0) return;
    state.manualWarningThresholdSeconds = value;
    updateButtonStates();
    draw();
  });

  assignmentMinRelativeInput.addEventListener("change", () => {
    const value = Number.parseFloat(assignmentMinRelativeInput.value);
    if (!Number.isFinite(value)) return;
    state.assignmentMinRelative = value;
    updateButtonStates();
    draw();
  });

  assignmentMaxRelativeInput.addEventListener("change", () => {
    const value = Number.parseFloat(assignmentMaxRelativeInput.value);
    if (!Number.isFinite(value)) return;
    state.assignmentMaxRelative = value;
    updateButtonStates();
    draw();
  });

  nudgeWarningsBtn.addEventListener("click", () => {
    const outliers = collectOutlierTargets();
    if (!outliers.length) return;
    for (const target of outliers) {
      const current = state.beats[target.index];
      const moved = current + (target.avg - current) * 0.2;
      state.beats[target.index] = normalizeTime(moved);
    }
    state.beats.sort((a, b) => a - b);
    clearSelection();
    updateButtonStates();
    draw();
  });

  jumpNextAlertManualBtn.addEventListener("click", () => {
    const target = findNextAlertOrUnassignedManualTime();
    if (target == null) return;
    T.setViewStart(target - state.viewSpanSeconds / 2);
    updateButtonStates();
    draw();
  });

  applySelectedBtn.addEventListener("click", () => {
    if (state.selectedBeatIndices.length !== 1) return;
    const targetIndex = state.selectedBeatIndices[0];
    if (targetIndex < 0 || targetIndex >= state.beats.length) return;
    const target = Number.parseFloat(selectedBeatInput.value);
    if (!Number.isFinite(target)) return;
    state.beats[targetIndex] = normalizeTime(target);
    state.beats.sort((a, b) => a - b);
    const selected = state.beats.findIndex((v) => Math.abs(v - normalizeTime(target)) < 0.0006);
    makeSelectionFromIndices(selected >= 0 ? [selected] : []);
    updateButtonStates();
    draw();
  });

  window.addEventListener("keydown", (event) => {
    if (event.code !== "Space" || !state.isPlaying || event.repeat) return;
    event.preventDefault();
    const time = A.getAudioTimeFromKeyboardEvent(event, offsetMsInput.value);
    state.manualBeats.push(time);
    state.manualBeats.sort((a, b) => a - b);
    updateButtonStates();
    draw();
  });

  canvas.addEventListener("mousedown", (event) => {
    const rect = canvas.getBoundingClientRect();
    const x = event.clientX - rect.left;
    const picked = T.pickTopBeatIndex(x, rect.width);
    const isToggleSelect = event.ctrlKey || event.metaKey;
    const isRangeSelect = event.shiftKey;
    if (picked >= 0) {
      if (isToggleSelect) {
        const selectedSet = new Set(state.selectedBeatIndices);
        if (selectedSet.has(picked)) selectedSet.delete(picked);
        else selectedSet.add(picked);
        makeSelectionFromIndices([...selectedSet]);
      } else if (isRangeSelect && state.selectedBeatIndices.length > 0) {
        const anchor = state.selectedBeatIndices[0];
        makeSelectionFromIndices(buildRangeSelection(anchor, picked));
      } else {
        if (!state.selectedBeatIndices.includes(picked)) {
          makeSelectionFromIndices([picked]);
        }
        state.dragBeatIndex = picked;
        state.dragSelectionIndices = [...state.selectedBeatIndices];
        state.dragAnchorTime = state.beats[picked];
        state.isDraggingBeat = true;
      }
    } else {
      clearSelection();
      if (state.isInsertModeEnabled) {
        addBeatAt(T.toSecondsFromX(x, rect.width));
      } else {
        isAreaSelecting = true;
        areaSelectStartX = clamp(x, 0, rect.width);
        areaSelectCurrentX = areaSelectStartX;
      }
    }
    updateButtonStates();
    draw();
  });

  window.addEventListener("mousemove", (event) => {
    const rect = canvas.getBoundingClientRect();
    const x = clamp(event.clientX - rect.left, 0, rect.width);

    if (state.isDraggingBeat && state.dragBeatIndex >= 0) {
      const targetTime = normalizeTime(T.toSecondsFromX(x, rect.width));
      const delta = targetTime - state.dragAnchorTime;
      for (const index of state.dragSelectionIndices) {
        state.beats[index] = normalizeTime(state.beats[index] + delta);
      }
      state.dragAnchorTime = targetTime;
      updateSelectedBeatUI();
      draw();
      return;
    }

    if (isAreaSelecting) {
      areaSelectCurrentX = x;
      updateSelectionFromArea(rect.width);
      updateButtonStates();
      draw();
    }
  });

  window.addEventListener("mouseup", () => {
    if (state.isDraggingBeat) finishDragSelection();
    if (isAreaSelecting) isAreaSelecting = false;
    updateButtonStates();
    draw();
  });

  timeline.addEventListener("wheel", (event) => {
    if (!state.audioBuffer) return;
    event.preventDefault();
    if (event.ctrlKey || event.metaKey) {
      const factor = event.deltaY > 0 ? 1.1 : 0.9;
      T.setViewSpan(state.viewSpanSeconds * factor);
    } else {
      const deltaSec = (event.deltaY / 120) * (state.viewSpanSeconds * 0.1);
      T.setViewStart(state.viewStartSeconds + deltaSec);
    }
    updateButtonStates();
    draw();
  }, { passive: false });

  window.addEventListener("resize", resizeCanvas);
  resizeCanvas();
  updateButtonStates();
  draw();
})();
