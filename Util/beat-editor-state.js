(function () {
  const state = {
    audioContext: null,
    audioBuffer: null,
    sourceNode: null,
    beats: [],
    manualBeats: [],
    beatsFileName: "",
    selectedBeatIndex: -1,
    selectedBeatIndices: [],
    isInsertModeEnabled: false,
    warningThresholdSeconds: 0.1,
    manualWarningThresholdSeconds: 0.1,
    assignmentMinRelative: -0.5,
    assignmentMaxRelative: 0.3,
    isSnapEnabled: true,
    snapSeconds: 0.01,
    beatPickRadiusPx: 8,
    isPlaying: false,
    playbackStartAudioTime: 0,
    playbackStartPerfMs: 0,
    playbackOffsetSeconds: 0,
    rafId: 0,
    isDraggingBeat: false,
    dragBeatIndex: -1,
    dragSelectionIndices: [],
    dragAnchorTime: 0,
    viewSpanSeconds: 5,
    viewStartSeconds: 0,
    followPlayhead: true,
  };

  function clamp(v, min, max) {
    return Math.max(min, Math.min(max, v));
  }

  function duration() {
    return state.audioBuffer ? state.audioBuffer.duration : 1;
  }

  function minViewStart() {
    return -state.viewSpanSeconds / 2;
  }

  function maxViewStart() {
    return duration() - state.viewSpanSeconds / 2;
  }

  function normalizeTime(sec) {
    let value = clamp(sec, 0, duration());
    if (state.isSnapEnabled) {
      value = Math.round(value / state.snapSeconds) * state.snapSeconds;
    }
    return clamp(value, 0, duration());
  }

  function parseBeatsText(text) {
    const values = [];
    for (const rawLine of text.split(/\r?\n/)) {
      const line = rawLine.trim();
      if (!line || line.startsWith("#")) continue;
      const num = Number.parseFloat(line);
      if (Number.isFinite(num) && num >= 0) values.push(num);
    }
    return values.sort((a, b) => a - b);
  }

  function serializeBeats(values) {
    return values.map((v) => v.toFixed(6)).join("\n") + "\n";
  }

  function sortAndMergeNearby(threshold = 0.02) {
    if (!state.beats.length) return;
    state.beats.sort((a, b) => a - b);
    const merged = [state.beats[0]];
    for (let i = 1; i < state.beats.length; i++) {
      const last = merged[merged.length - 1];
      const now = state.beats[i];
      if (Math.abs(now - last) < threshold) {
        merged[merged.length - 1] = (last + now) / 2;
      } else {
        merged.push(now);
      }
    }
    state.beats = merged;
    state.selectedBeatIndex = -1;
  }

  function buildManualNearestAssignments(maxDelta = 0.2) {
    const assignments = [];
    if (!state.beats.length || !state.manualBeats.length) return assignments;
    const minRel = state.assignmentMinRelative ?? -0.5;
    const maxRel = state.assignmentMaxRelative ?? 0.3;

    for (let manualIndex = 0; manualIndex < state.manualBeats.length; manualIndex++) {
      const manual = state.manualBeats[manualIndex];
      let best = null;

      for (let i = 0; i < state.beats.length; i++) {
        const beat = state.beats[i];
        const prevBeat = i > 0 ? state.beats[i - 1] : null;
        const nextBeat = i < state.beats.length - 1 ? state.beats[i + 1] : null;
        const diff = Math.abs(manual - beat);
        if (diff > maxDelta) continue;

        let rel;
        if (manual < beat) {
          if (prevBeat == null) continue;
          const span = beat - prevBeat;
          if (span <= 0) continue;
          rel = (manual - beat) / span; // prev=-1, beat=0
        } else {
          if (nextBeat == null) continue;
          const span = nextBeat - beat;
          if (span <= 0) continue;
          rel = (manual - beat) / span; // beat=0, next=1
        }

        if (rel < minRel || rel >= maxRel) continue;

        // 1つの手打ち点に対して、最も近い1ビートだけを採用する
        if (!best || diff < best.diff) {
          best = { manual, manualIndex, beatIndex: i, diff };
        }
      }

      if (best) assignments.push(best);
    }
    return assignments;
  }

  window.BeatEditorState = {
    state,
    clamp,
    duration,
    minViewStart,
    maxViewStart,
    normalizeTime,
    parseBeatsText,
    serializeBeats,
    sortAndMergeNearby,
    buildManualNearestAssignments,
  };
})();
