(function () {
  const S = window.BeatEditorState;
  const { state, clamp, duration, minViewStart, maxViewStart, buildManualNearestAssignments } = S;

  function toTimelineX(sec, width) {
    return ((sec - state.viewStartSeconds) / state.viewSpanSeconds) * width;
  }

  function toSecondsFromX(x, width) {
    return state.viewStartSeconds + (x / width) * state.viewSpanSeconds;
  }

  function setViewStart(sec) {
    state.viewStartSeconds = clamp(sec, minViewStart(), maxViewStart());
  }

  function setViewSpan(sec) {
    state.viewSpanSeconds = clamp(sec, 2, Math.max(2, duration()));
    setViewStart(state.viewStartSeconds);
  }

  function ensurePlayheadVisible(sec) {
    if (!state.followPlayhead) return;
    setViewStart(sec - state.viewSpanSeconds / 2);
  }

  function pickTopBeatIndex(mouseX, width) {
    let best = -1;
    let bestDist = Number.POSITIVE_INFINITY;
    for (let i = 0; i < state.beats.length; i++) {
      const sec = state.beats[i];
      if (sec < state.viewStartSeconds || sec > state.viewStartSeconds + state.viewSpanSeconds) continue;
      const x = toTimelineX(sec, width);
      const d = Math.abs(x - mouseX);
      if (d < bestDist && d <= state.beatPickRadiusPx) {
        best = i;
        bestDist = d;
      }
    }
    return best;
  }

  function buildNearestPairs(maxDelta = 0.2) {
    const pairs = [];
    const assignments = buildManualNearestAssignments(maxDelta);
    for (const item of assignments) {
      pairs.push({ file: state.beats[item.beatIndex], manual: item.manual, diff: item.diff });
    }
    return pairs;
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

  function buildOutlierBeatIndexSet(maxDelta = 0.2, threshold = state.warningThresholdSeconds ?? 0.1) {
    const outliers = new Set();
    const assignments = buildManualNearestAssignments(maxDelta);
    if (!assignments.length) return outliers;

    const buckets = new Map();
    for (const item of assignments) {
      if (!buckets.has(item.beatIndex)) buckets.set(item.beatIndex, []);
      buckets.get(item.beatIndex).push(item.manual);
    }

    for (const [index, manuals] of buckets.entries()) {
      const beat = state.beats[index];
      const avg = manuals.reduce((a, b) => a + b, 0) / manuals.length;
      if (Math.abs(avg - beat) >= threshold) outliers.add(index);
    }
    return outliers;
  }

  function buildAssignedManualIndexSet(maxDelta = 0.2) {
    const assigned = new Set();
    const assignments = buildManualNearestAssignments(maxDelta);
    for (const item of assignments) assigned.add(item.manualIndex);
    return assigned;
  }

  function drawTimeline(canvas, playheadSec) {
    const ctx = canvas.getContext("2d");
    const width = canvas.clientWidth;
    const height = canvas.clientHeight;
    ctx.clearRect(0, 0, width, height);

    const centerY = Math.floor(height * 0.55);
    const fileY1 = centerY - 30;
    const fileY2 = centerY + 30;
    const manualR = 4;

    ctx.fillStyle = "#111724";
    ctx.fillRect(0, 0, width, height);

    ctx.strokeStyle = "#2e3a53";
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(0, centerY);
    ctx.lineTo(width, centerY);
    ctx.stroke();

    const gridStep = state.viewSpanSeconds <= 10 ? 0.5 : state.viewSpanSeconds <= 30 ? 1 : 5;
    ctx.fillStyle = "#8593aa";
    ctx.font = "11px Inter, sans-serif";
    ctx.textBaseline = "top";
    ctx.strokeStyle = "#1f2a3f";
    for (let t = Math.floor(state.viewStartSeconds / gridStep) * gridStep; t <= state.viewStartSeconds + state.viewSpanSeconds; t += gridStep) {
      const x = toTimelineX(t, width);
      if (x < 0 || x > width) continue;
      ctx.beginPath();
      ctx.moveTo(x, 0);
      ctx.lineTo(x, height);
      ctx.stroke();
      ctx.fillText(`${t.toFixed(1)}s`, x + 2, 2);
    }

    // Difference overlay lines
    const pairs = buildNearestPairs(0.2);
    for (const p of pairs) {
      if (p.file < state.viewStartSeconds || p.file > state.viewStartSeconds + state.viewSpanSeconds) continue;
      if (p.manual < state.viewStartSeconds || p.manual > state.viewStartSeconds + state.viewSpanSeconds) continue;
      const xf = toTimelineX(p.file, width);
      const xm = toTimelineX(p.manual, width);
      const alpha = 1 - clamp(p.diff / 0.2, 0, 1);
      ctx.strokeStyle = `rgba(255, 166, 79, ${0.2 + alpha * 0.6})`;
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.moveTo(xf, centerY);
      ctx.lineTo(xm, centerY);
      ctx.stroke();
    }

    // File beats
    const outlierBeatIndices = buildOutlierBeatIndexSet(0.2, state.warningThresholdSeconds ?? 0.1);
    ctx.lineWidth = 2;
    for (let i = 0; i < state.beats.length; i++) {
      const sec = state.beats[i];
      if (sec < state.viewStartSeconds || sec > state.viewStartSeconds + state.viewSpanSeconds) continue;
      const x = toTimelineX(sec, width);
      const isSelected = i === state.selectedBeatIndex || state.selectedBeatIndices.includes(i);
      if (isSelected) {
        ctx.strokeStyle = "#ffffff";
      } else if (outlierBeatIndices.has(i)) {
        ctx.strokeStyle = "#ff4d5e";
      } else {
        ctx.strokeStyle = "#64b5ff";
      }
      ctx.beginPath();
      ctx.moveTo(x, fileY1);
      ctx.lineTo(x, fileY2);
      ctx.stroke();
    }

    // Manual beats (same lane, dots)
    const manualWarningThreshold = state.manualWarningThresholdSeconds ?? 0.1;
    const assignedManualIndices = buildAssignedManualIndexSet(0.2);
    for (let i = 0; i < state.manualBeats.length; i++) {
      const sec = state.manualBeats[i];
      if (sec < state.viewStartSeconds || sec > state.viewStartSeconds + state.viewSpanSeconds) continue;
      const x = toTimelineX(sec, width);
      const nearestDiff = getNearestBeatDiff(sec);
      if (!assignedManualIndices.has(i)) {
        ctx.fillStyle = "#8b1e2b";
      } else if (nearestDiff > manualWarningThreshold) {
        ctx.fillStyle = "#ff4d5e";
      } else {
        ctx.fillStyle = "#f78ec5";
      }
      ctx.beginPath();
      ctx.arc(x, centerY, manualR, 0, Math.PI * 2);
      ctx.fill();
    }

    // Fixed playhead at window center
    const centerX = width / 2;
    ctx.strokeStyle = "#ffd23f";
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.moveTo(centerX, 0);
    ctx.lineTo(centerX, height);
    ctx.stroke();
  }

  window.BeatEditorTimeline = {
    toTimelineX,
    toSecondsFromX,
    setViewStart,
    setViewSpan,
    ensurePlayheadVisible,
    pickTopBeatIndex,
    drawTimeline,
  };
})();
