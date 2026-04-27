(function () {
  const S = window.BeatEditorState;
  const { state, clamp, duration } = S;

  function ensureAudioContext() {
    if (!state.audioContext) {
      state.audioContext = new (window.AudioContext || window.webkitAudioContext)();
    }
  }

  function getCurrentPlaybackSeconds() {
    if (!state.isPlaying || !state.audioContext) return state.playbackOffsetSeconds;
    const elapsed = state.audioContext.currentTime - state.playbackStartAudioTime;
    return clamp(state.playbackOffsetSeconds + elapsed, 0, duration());
  }

  function getAudioTimeFromKeyboardEvent(event, offsetMs) {
    const raw = typeof event.timeStamp === "number" ? event.timeStamp : performance.now();
    const perfNowMs = raw > 1e12 ? raw - performance.timeOrigin : raw;
    const elapsedSeconds = (perfNowMs - state.playbackStartPerfMs) / 1000;
    const correctionSeconds = (Number.parseFloat(offsetMs) || 0) / 1000;
    return clamp(state.playbackOffsetSeconds + elapsedSeconds + correctionSeconds, 0, duration());
  }

  function stopPlayback() {
    const current = state.isPlaying ? getCurrentPlaybackSeconds() : state.playbackOffsetSeconds;
    if (state.sourceNode) {
      try {
        state.sourceNode.stop();
      } catch (_e) {
        // no-op
      }
      state.sourceNode.disconnect();
      state.sourceNode = null;
    }
    if (state.rafId) cancelAnimationFrame(state.rafId);
    state.rafId = 0;
    state.playbackOffsetSeconds = current;
    state.isPlaying = false;
  }

  async function startPlayback(onTick, onEnded) {
    if (!state.audioBuffer) return;
    ensureAudioContext();
    await state.audioContext.resume();
    stopPlayback();

    state.sourceNode = state.audioContext.createBufferSource();
    state.sourceNode.buffer = state.audioBuffer;
    state.sourceNode.connect(state.audioContext.destination);

    state.playbackStartAudioTime = state.audioContext.currentTime;
    state.playbackStartPerfMs = performance.now();
    state.isPlaying = true;
    state.sourceNode.start(0, state.playbackOffsetSeconds);

    state.sourceNode.onended = () => {
      if (!state.isPlaying) return;
      state.playbackOffsetSeconds = 0;
      stopPlayback();
      if (onEnded) onEnded();
    };

    const tick = () => {
      if (!state.isPlaying) return;
      if (onTick) onTick(getCurrentPlaybackSeconds());
      state.rafId = requestAnimationFrame(tick);
    };
    tick();
  }

  window.BeatEditorAudio = {
    ensureAudioContext,
    startPlayback,
    stopPlayback,
    getCurrentPlaybackSeconds,
    getAudioTimeFromKeyboardEvent,
  };
})();
