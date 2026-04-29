(function () {
  const S = window.BeatEditorState;
  const { state, clamp, duration } = S;

  function ensureAudioContext() {
    if (!state.audioContext) {
      state.audioContext = new (window.AudioContext || window.webkitAudioContext)();
    }
  }

  async function ensureBeatSeLoaded() {
    if (state.beatSeBuffer || state.beatSeFallbackAudio) return;
    ensureAudioContext();
    try {
      const response = await fetch("./hand.mp3");
      if (!response.ok) throw new Error(`Failed to load hand.mp3: ${response.status}`);
      const arrayBuffer = await response.arrayBuffer();
      state.beatSeBuffer = await state.audioContext.decodeAudioData(arrayBuffer);
    } catch (_error) {
      // Some environments (e.g. file://) may block fetch. Fall back to HTMLAudio.
      state.beatSeFallbackAudio = new Audio("./hand.mp3");
      state.beatSeFallbackAudio.preload = "auto";
      state.beatSeFallbackAudio.load();
    }
  }

  function playBeatSe() {
    if (state.audioContext && state.beatSeBuffer) {
      const source = state.audioContext.createBufferSource();
      const gainNode = state.audioContext.createGain();
      gainNode.gain.value = 0.85;
      source.buffer = state.beatSeBuffer;
      source.connect(gainNode);
      gainNode.connect(state.audioContext.destination);
      source.start();
      return;
    }
    if (state.beatSeFallbackAudio) {
      const oneShot = state.beatSeFallbackAudio.cloneNode();
      oneShot.volume = 0.85;
      oneShot.play().catch(() => {});
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
    ensureBeatSeLoaded,
    playBeatSe,
    startPlayback,
    stopPlayback,
    getCurrentPlaybackSeconds,
    getAudioTimeFromKeyboardEvent,
  };
})();
