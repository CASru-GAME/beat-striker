// ========== Helper: wire up a standard curve tab ==========
function wireCurveControls(curve, ids, valueScale, valueUnit) {
  const pointInfo = document.getElementById(ids.pointInfo);
  const pointX = document.getElementById(ids.pointX);
  const pointY = document.getElementById(ids.pointY);
  const offsetSlider = document.getElementById(ids.offsetSlider);
  const offsetInput = document.getElementById(ids.offsetInput);
  const interpSelect = document.getElementById(ids.interp);
  const resetBtn = document.getElementById(ids.reset);

  pointY.addEventListener('change', () => {
    curve.setSelectedValue(parseFloat(pointY.value) / valueScale);
  });
  pointX.addEventListener('change', () => {
    if (curve.logX) {
      curve.setSelectedX(curve.actualToNormX(parseFloat(pointX.value)));
    } else {
      curve.setSelectedX(parseFloat(pointX.value));
    }
  });

  let offsetBase = 0;
  offsetSlider.addEventListener('input', () => {
    const sliderVal = parseInt(offsetSlider.value);
    const delta = (sliderVal - offsetBase) / 100;
    offsetBase = sliderVal;
    curve.shiftAll(delta);
    offsetInput.value = valueScale === 1
      ? curve.getAverageValue().toFixed(2)
      : (curve.getAverageValue() * valueScale).toFixed(0);
  });
  offsetSlider.addEventListener('mouseup', () => { offsetBase = 0; offsetSlider.value = 0; });
  offsetSlider.addEventListener('touchend', () => { offsetBase = 0; offsetSlider.value = 0; });

  offsetInput.addEventListener('change', () => {
    const target = parseFloat(offsetInput.value) / valueScale;
    curve.setAllToValue(target);
  });

  if (interpSelect) {
    interpSelect.addEventListener('change', (e) => {
      curve.interpolation = e.target.value;
      curve.draw();
    });
  }

  const defNorm = curve.actualToNormY(curve.defaultY);
  resetBtn.addEventListener('click', () => {
    curve.points = [{ x: 0, y: defNorm }, { x: 1, y: defNorm }];
    curve.selectedIndex = null;
    curve._notifySelection();
    curve.draw();
    offsetInput.value = valueScale === 1
      ? curve.defaultY.toFixed(2)
      : (curve.defaultY * valueScale).toFixed(0);
  });
}

// ========== Initialize Curve Editors ==========
const volumeValLabel = document.getElementById('volumeCurveVal');
const pitchValLabel = document.getElementById('pitchCurveVal');
const speedValLabel = document.getElementById('speedCurveVal');
const eqValLabel = document.getElementById('eqCurveVal');

// Volume
const volumeCurve = new CurveEditor(document.getElementById('volumeCurveCanvas'), {
  minY: 0, maxY: 3, defaultY: 1,
  color: '#22d3ee', glowColor: 'rgba(34, 211, 238, 0.25)',
  labelFormat: (v) => (v * 100).toFixed(0) + '%',
  onHover: (v) => { volumeValLabel.textContent = (v * 100).toFixed(0); },
  onSelect: (info) => {
    const pi = document.getElementById('volumePointInfo');
    if (info) {
      pi.classList.add('has-selection');
      document.getElementById('volumePointX').value = info.x.toFixed(3);
      document.getElementById('volumePointY').value = (info.value * 100).toFixed(0);
    } else { pi.classList.remove('has-selection'); }
  }
});
volumeCurve.points = [{ x: 0, y: 1/3 }, { x: 1, y: 1/3 }];
volumeCurve.draw();
wireCurveControls(volumeCurve, {
  pointInfo: 'volumePointInfo', pointX: 'volumePointX', pointY: 'volumePointY',
  offsetSlider: 'volumeOffsetSlider', offsetInput: 'volumeOffsetInput',
  interp: 'volumeInterp', reset: 'volumeResetBtn'
}, 100, '%');

// Pitch (logY for perceptually linear pitch control)
const pitchCurve = new CurveEditor(document.getElementById('pitchCurveCanvas'), {
  minY: 0.25, maxY: 4, defaultY: 1,
  logY: true,
  color: '#f472b6', glowColor: 'rgba(244, 114, 182, 0.25)',
  labelFormat: (v) => v.toFixed(2) + 'x',
  onHover: (v) => { pitchValLabel.textContent = v.toFixed(2); },
  onSelect: (info) => {
    const pi = document.getElementById('pitchPointInfo');
    if (info) {
      pi.classList.add('has-selection');
      document.getElementById('pitchPointX').value = info.x.toFixed(3);
      document.getElementById('pitchPointY').value = info.value.toFixed(2);
    } else { pi.classList.remove('has-selection'); }
  }
});
// logY: actualToNormY(1.0) = log(1/0.25)/log(4/0.25) = log(4)/log(16) = 0.5
pitchCurve.points = [{ x: 0, y: 0.5 }, { x: 1, y: 0.5 }];
pitchCurve.draw();
wireCurveControls(pitchCurve, {
  pointInfo: 'pitchPointInfo', pointX: 'pitchPointX', pointY: 'pitchPointY',
  offsetSlider: 'pitchOffsetSlider', offsetInput: 'pitchOffsetInput',
  interp: 'pitchInterp', reset: 'pitchResetBtn'
}, 1, 'x');

// Speed
const speedCurve = new CurveEditor(document.getElementById('speedCurveCanvas'), {
  minY: 0.25, maxY: 4, defaultY: 1,
  color: '#fbbf24', glowColor: 'rgba(251, 191, 36, 0.25)',
  labelFormat: (v) => v.toFixed(2) + 'x',
  onHover: (v) => { speedValLabel.textContent = v.toFixed(2); },
  onSelect: (info) => {
    const pi = document.getElementById('speedPointInfo');
    if (info) {
      pi.classList.add('has-selection');
      document.getElementById('speedPointX').value = info.x.toFixed(3);
      document.getElementById('speedPointY').value = info.value.toFixed(2);
    } else { pi.classList.remove('has-selection'); }
  }
});
const speedDefNorm = speedCurve.actualToNormY(1);
speedCurve.points = [{ x: 0, y: speedDefNorm }, { x: 1, y: speedDefNorm }];
speedCurve.draw();
wireCurveControls(speedCurve, {
  pointInfo: 'speedPointInfo', pointX: 'speedPointX', pointY: 'speedPointY',
  offsetSlider: 'speedOffsetSlider', offsetInput: 'speedOffsetInput',
  interp: 'speedInterp', reset: 'speedResetBtn'
}, 1, 'x');

// EQ
const eqCurve = new CurveEditor(document.getElementById('eqCurveCanvas'), {
  minY: -24, maxY: 24, defaultY: 0,
  logX: true, xMin: 20, xMax: 20000,
  color: '#34d399', glowColor: 'rgba(52, 211, 153, 0.25)',
  interpolation: 'smooth',
  labelFormat: (v) => v.toFixed(1) + 'dB',
  onHover: (v, freq) => {
    const fStr = freq >= 1000 ? (freq/1000).toFixed(1) + 'k' : Math.round(freq) + '';
    eqValLabel.textContent = v.toFixed(1) + 'dB @ ' + fStr + 'Hz';
  },
  onSelect: (info) => {
    const pi = document.getElementById('eqPointInfo');
    if (info) {
      pi.classList.add('has-selection');
      document.getElementById('eqPointX').value = Math.round(info.actualX);
      document.getElementById('eqPointY').value = info.value.toFixed(1);
    } else { pi.classList.remove('has-selection'); }
  }
});
const eqDefNorm = eqCurve.actualToNormY(0);
eqCurve.points = [{ x: 0, y: eqDefNorm }, { x: 1, y: eqDefNorm }];
eqCurve.draw();
wireCurveControls(eqCurve, {
  pointInfo: 'eqPointInfo', pointX: 'eqPointX', pointY: 'eqPointY',
  offsetSlider: 'eqOffsetSlider', offsetInput: 'eqOffsetInput',
  interp: null, reset: 'eqResetBtn'
}, 1, 'dB');

document.getElementById('eqPointX').addEventListener('change', () => {
  const freq = parseFloat(document.getElementById('eqPointX').value);
  const nx = eqCurve.actualToNormX(Math.max(20, Math.min(20000, freq)));
  eqCurve.setSelectedX(nx);
});

// ========== DOM Elements ==========
const dropOverlay = document.getElementById('dropOverlay');
const fileInput = document.getElementById('fileInput');
const fileInfo = document.getElementById('fileInfo');
const fileNameEl = document.getElementById('fileName');
const fileMetaEl = document.getElementById('fileMeta');
const controlsArea = document.getElementById('controlsArea');
const playBtn = document.getElementById('playBtn');
const downloadBtn = document.getElementById('downloadBtn');
const waveformCanvas = document.getElementById('waveformCanvas');
const waveformContainer = document.getElementById('waveformContainer');
const waveformEmpty = document.getElementById('waveformEmpty');
const playbackCursor = document.getElementById('playbackCursor');

const clipHandleStart = document.getElementById('clipHandleStart');
const clipHandleEnd = document.getElementById('clipHandleEnd');
const clipOutLeft = document.getElementById('clipOutLeft');
const clipOutRight = document.getElementById('clipOutRight');
const clipTimeStart = document.getElementById('clipTimeStart');
const clipTimeEnd = document.getElementById('clipTimeEnd');
const clipStartInput = document.getElementById('clipStartInput');
const clipEndInput = document.getElementById('clipEndInput');

let audioContext = null;
let originalBuffer = null;
let sourceNode = null;
let gainNode = null;
let isPlaying = false;
let currentFileName = '';
let currentFileType = '';
let playStartTime = 0;
let animFrameId = null;

let clipStartNorm = 0;
let clipEndNorm = 1;

// ========== Reverb Controls ==========
const reverbAmountSlider = document.getElementById('reverbAmount');
const reverbAmountVal = document.getElementById('reverbAmountVal');
const reverbDecaySlider = document.getElementById('reverbDecay');
const reverbDecayVal = document.getElementById('reverbDecayVal');

reverbAmountSlider.addEventListener('input', () => {
  reverbAmountVal.textContent = reverbAmountSlider.value + '%';
});
reverbDecaySlider.addEventListener('input', () => {
  reverbDecayVal.textContent = parseFloat(reverbDecaySlider.value).toFixed(1) + 's';
});

// ========== Processing Overlay ==========
function showProcessing(msg) {
  const el = document.createElement('div');
  el.className = 'processing-overlay';
  el.id = 'processingOverlay';
  el.innerHTML = `<span class="spinner" style="display:inline-block;"></span> ${msg}`;
  document.body.appendChild(el);
}

function hideProcessing() {
  const el = document.getElementById('processingOverlay');
  if (el) el.remove();
}

// ========== Waveform & Clipping ==========
function drawWaveform() {
  if (!originalBuffer) return;
  const canvas = waveformCanvas;
  const dpr = window.devicePixelRatio || 1;
  const rect = waveformContainer.getBoundingClientRect();
  canvas.width = rect.width * dpr;
  canvas.height = rect.height * dpr;
  const ctx = canvas.getContext('2d');
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  const w = rect.width, h = rect.height;
  ctx.clearRect(0, 0, w, h);

  const data = originalBuffer.getChannelData(0);
  const step = Math.ceil(data.length / w);
  const mid = h / 2;
  ctx.fillStyle = 'rgba(167, 139, 250, 0.5)';
  for (let i = 0; i < w; i++) {
    let min = 1, max = -1;
    for (let j = 0; j < step; j++) {
      const idx = Math.floor(i * step + j);
      if (idx < data.length) {
        const val = data[idx];
        if (val < min) min = val;
        if (val > max) max = val;
      }
    }
    ctx.fillRect(i, mid + min * mid, 1, (max - min) * mid || 1);
  }
}

new ResizeObserver(() => { if (originalBuffer) drawWaveform(); }).observe(waveformContainer);

function updateClipVisuals() {
  const w = waveformContainer.getBoundingClientRect().width;
  const startPx = clipStartNorm * w;
  const endPx = clipEndNorm * w;
  clipHandleStart.style.left = startPx + 'px';
  clipHandleEnd.style.left = endPx + 'px';
  clipOutLeft.style.width = startPx + 'px';
  clipOutRight.style.left = endPx + 'px';
  clipOutRight.style.width = (w - endPx) + 'px';
  if (originalBuffer) {
    const dur = originalBuffer.duration;
    clipTimeStart.textContent = (clipStartNorm * dur).toFixed(2) + 's';
    clipTimeEnd.textContent = (clipEndNorm * dur).toFixed(2) + 's';
  }
}

let clipDragging = null;
clipHandleStart.addEventListener('mousedown', (e) => { e.stopPropagation(); clipDragging = 'start'; });
clipHandleEnd.addEventListener('mousedown', (e) => { e.stopPropagation(); clipDragging = 'end'; });
clipHandleStart.addEventListener('touchstart', (e) => { e.preventDefault(); e.stopPropagation(); clipDragging = 'start'; }, { passive: false });
clipHandleEnd.addEventListener('touchstart', (e) => { e.preventDefault(); e.stopPropagation(); clipDragging = 'end'; }, { passive: false });

function handleClipDrag(clientX) {
  if (!clipDragging || !originalBuffer) return;
  const rect = waveformContainer.getBoundingClientRect();
  const x = Math.max(0, Math.min(rect.width, clientX - rect.left));
  const norm = x / rect.width;
  if (clipDragging === 'start') {
    clipStartNorm = Math.min(norm, clipEndNorm - 0.005);
    clipStartInput.value = (clipStartNorm * originalBuffer.duration).toFixed(2);
  } else {
    clipEndNorm = Math.max(norm, clipStartNorm + 0.005);
    clipEndInput.value = (clipEndNorm * originalBuffer.duration).toFixed(2);
  }
  updateClipVisuals();
}

document.addEventListener('mousemove', (e) => handleClipDrag(e.clientX));
document.addEventListener('touchmove', (e) => { if (clipDragging) { e.preventDefault(); handleClipDrag(e.touches[0].clientX); } }, { passive: false });
document.addEventListener('mouseup', () => { clipDragging = null; });
document.addEventListener('touchend', () => { clipDragging = null; });

clipStartInput.addEventListener('change', () => {
  if (!originalBuffer) return;
  const val = Math.max(0, Math.min(originalBuffer.duration, parseFloat(clipStartInput.value) || 0));
  clipStartNorm = val / originalBuffer.duration;
  if (clipStartNorm >= clipEndNorm) clipStartNorm = clipEndNorm - 0.005;
  clipStartInput.value = (clipStartNorm * originalBuffer.duration).toFixed(2);
  updateClipVisuals();
});

clipEndInput.addEventListener('change', () => {
  if (!originalBuffer) return;
  const val = Math.max(0, Math.min(originalBuffer.duration, parseFloat(clipEndInput.value) || 0));
  clipEndNorm = val / originalBuffer.duration;
  if (clipEndNorm <= clipStartNorm) clipEndNorm = clipStartNorm + 0.005;
  clipEndInput.value = (clipEndNorm * originalBuffer.duration).toFixed(2);
  updateClipVisuals();
});

// ========== Load Audio ==========
async function loadAudio(file) {
  currentFileName = file.name.replace(/\.[^/.]+$/, '');
  currentFileType = file.type;
  dropOverlay.classList.remove('show-hint', 'active');
  fileInfo.style.display = 'block';
  fileNameEl.textContent = file.name;
  fileMetaEl.textContent = '読み込み中...';
  controlsArea.classList.remove('active');

  try {
    if (!audioContext) audioContext = new (window.AudioContext || window.webkitAudioContext)();
    const arrayBuffer = await file.arrayBuffer();
    originalBuffer = await audioContext.decodeAudioData(arrayBuffer);
    fileMetaEl.textContent = `${originalBuffer.duration.toFixed(2)}s | ${originalBuffer.numberOfChannels}ch | ${originalBuffer.sampleRate}Hz | ${currentFileType || 'audio'}`;
    clipStartNorm = 0; clipEndNorm = 1;
    clipStartInput.value = '0.00';
    clipEndInput.value = originalBuffer.duration.toFixed(2);
    clipStartInput.max = originalBuffer.duration;
    clipEndInput.max = originalBuffer.duration;
    updateClipVisuals();
    drawWaveform();
    waveformEmpty.style.display = 'none';
    const ext = (currentFileType === 'audio/mpeg' || currentFileType === 'audio/mp3') ? 'MP3' : 'WAV';
    downloadBtn.innerHTML = `<span class="spinner" id="dlSpinner"></span> ${ext}をダウンロード`;
    controlsArea.classList.add('active');
  } catch (err) {
    console.error(err); alert('読み込みに失敗しました。'); resetUI();
  }
}

function resetUI() {
  dropOverlay.classList.add('show-hint'); fileInfo.style.display = 'none';
  controlsArea.classList.remove('active'); waveformEmpty.style.display = 'flex';
  originalBuffer = null; stopAudio();
}

// ========== Load Generated Buffer ==========
function loadGeneratedBuffer(buffer, name) {
  currentFileName = name;
  currentFileType = 'audio/wav';
  originalBuffer = buffer;
  dropOverlay.classList.remove('show-hint', 'active');
  fileInfo.style.display = 'block';
  fileNameEl.textContent = name;
  fileMetaEl.textContent = `${buffer.duration.toFixed(2)}s | ${buffer.numberOfChannels}ch | ${buffer.sampleRate}Hz | generated`;
  clipStartNorm = 0; clipEndNorm = 1;
  clipStartInput.value = '0.00';
  clipEndInput.value = buffer.duration.toFixed(2);
  clipStartInput.max = buffer.duration;
  clipEndInput.max = buffer.duration;
  updateClipVisuals();
  drawWaveform();
  waveformEmpty.style.display = 'none';
  downloadBtn.innerHTML = `<span class="spinner" id="dlSpinner"></span> WAVをダウンロード`;
  controlsArea.classList.add('active');
}

// ========== Events ==========
['dragenter', 'dragover', 'dragleave', 'drop'].forEach(ev => {
  document.body.addEventListener(ev, (e) => { e.preventDefault(); e.stopPropagation(); });
});
document.body.addEventListener('dragenter', () => dropOverlay.classList.add('active', 'drag-over'));
document.body.addEventListener('dragover', () => dropOverlay.classList.add('active', 'drag-over'));
document.body.addEventListener('dragleave', (e) => {
  if (!e.relatedTarget || e.relatedTarget === document.documentElement) dropOverlay.classList.remove('drag-over', 'active');
});
document.body.addEventListener('drop', (e) => {
  dropOverlay.classList.remove('drag-over', 'active');
  if (e.dataTransfer.files.length > 0) loadAudio(e.dataTransfer.files[0]);
});
dropOverlay.addEventListener('click', (e) => {
  // Don't trigger file input if template button was clicked
  if (e.target.closest('#openTemplateBtn')) return;
  fileInput.click();
});
fileInput.addEventListener('change', (e) => { if (e.target.files.length > 0) loadAudio(e.target.files[0]); });

document.querySelectorAll('.tab-btn').forEach(btn => {
  btn.addEventListener('click', () => {
    document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
    document.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));
    btn.classList.add('active');
    document.getElementById('tab-' + btn.dataset.tab).classList.add('active');
  });
});

// ========== Template Dialog ==========
const templateModal = document.getElementById('templateModal');
const openTemplateBtn = document.getElementById('openTemplateBtn');
const templateCancelBtn = document.getElementById('templateCancelBtn');
const templateGenerateBtn = document.getElementById('templateGenerateBtn');
const templatePreviewBtn = document.getElementById('templatePreviewBtn');
const freqModeNote = document.getElementById('freqModeNote');
const freqModeHz = document.getElementById('freqModeHz');
const freqNoteRow = document.getElementById('freqNoteRow');
const freqHzRow = document.getElementById('freqHzRow');
const templateNote = document.getElementById('templateNote');
const templateOctave = document.getElementById('templateOctave');
const freqDisplay = document.getElementById('freqDisplay');

// Note-to-frequency conversion (A4 = 440Hz)
const NOTE_SEMITONES = { 'C': -9, 'C#': -8, 'D': -7, 'D#': -6, 'E': -5, 'F': -4, 'F#': -3, 'G': -2, 'G#': -1, 'A': 0, 'A#': 1, 'B': 2 };

function noteToFreq(note, octave) {
  const semitone = NOTE_SEMITONES[note] + (octave - 4) * 12;
  return 440 * Math.pow(2, semitone / 12);
}

function getTemplateFreq() {
  if (freqModeNote.classList.contains('active')) {
    return noteToFreq(templateNote.value, parseInt(templateOctave.value));
  }
  return parseFloat(document.getElementById('templateFreq').value) || 440;
}

function updateFreqDisplay() {
  const freq = noteToFreq(templateNote.value, parseInt(templateOctave.value));
  freqDisplay.textContent = freq.toFixed(1) + ' Hz';
}

// Note/Hz toggle
freqModeNote.addEventListener('click', () => {
  freqModeNote.classList.add('active');
  freqModeHz.classList.remove('active');
  freqNoteRow.style.display = '';
  freqHzRow.style.display = 'none';
});

freqModeHz.addEventListener('click', () => {
  freqModeHz.classList.add('active');
  freqModeNote.classList.remove('active');
  freqNoteRow.style.display = 'none';
  freqHzRow.style.display = '';
});

templateNote.addEventListener('change', updateFreqDisplay);
templateOctave.addEventListener('change', updateFreqDisplay);
updateFreqDisplay();

// ========== Harmonics Editor ==========
const HARMONIC_COUNT = 8;
const harmonicsEditor = document.getElementById('harmonicsEditor');
const harmonicsNote = document.getElementById('harmonicsNote');
const harmonicSliders = [];
const harmonicVals = [];
for (let i = 0; i < HARMONIC_COUNT; i++) {
  harmonicSliders.push(document.getElementById('hSlider' + i));
  harmonicVals.push(document.getElementById('hVal' + i));
}

// Fourier-approximated presets per wave type
const HARMONIC_PRESETS = {
  sine:     [100, 0, 0, 0, 0, 0, 0, 0],
  square:   [100, 0, 33, 0, 20, 0, 14, 0],
  sawtooth: [100, 50, 33, 25, 20, 17, 14, 13],
  triangle: [100, 0, 11, 0, 4, 0, 2, 0],
  pulse:    [100, 71, 33, 0, 20, 14, 0, 13],
};

// Types that use their own algorithm (harmonics don't apply)
const SPECIAL_TYPES = ['noise', 'sine_sweep', 'metallic', 'metallic_bend'];

function setHarmonicsPreset(type) {
  const isSpecial = SPECIAL_TYPES.includes(type);
  if (isSpecial) {
    harmonicsEditor.classList.add('dimmed');
    harmonicsNote.textContent = 'この波形タイプには適用されません';
  } else {
    harmonicsEditor.classList.remove('dimmed');
    harmonicsNote.textContent = '波形タイプのプリセットを元に調整できます';
  }
  const preset = HARMONIC_PRESETS[type] || [100, 0, 0, 0, 0, 0, 0, 0];
  for (let i = 0; i < HARMONIC_COUNT; i++) {
    const val = preset[i] || 0;
    harmonicSliders[i].value = val;
    harmonicVals[i].textContent = val;
  }
}

function getHarmonicsArray() {
  const waveType = document.querySelector('input[name="waveType"]:checked').value;
  if (SPECIAL_TYPES.includes(waveType)) return null;
  return harmonicSliders.map(s => parseInt(s.value));
}

// Wire slider inputs
for (let i = 0; i < HARMONIC_COUNT; i++) {
  harmonicSliders[i].addEventListener('input', () => {
    harmonicVals[i].textContent = harmonicSliders[i].value;
  });
}

// Initialize with default wave type
setHarmonicsPreset('sine');

// Preview playback
let previewSource = null;
let previewGain = null;
let previewCtx = null;

function stopPreview() {
  if (previewSource) {
    try { previewSource.stop(); } catch(e) {}
    previewSource.disconnect();
    previewSource = null;
  }
  if (previewGain) { previewGain.disconnect(); previewGain = null; }
  templatePreviewBtn.classList.remove('playing');
  templatePreviewBtn.textContent = '🔊 プレビュー';
}

function playPreview() {
  stopPreview();
  if (!previewCtx) previewCtx = new (window.AudioContext || window.webkitAudioContext)();
  if (previewCtx.state === 'suspended') previewCtx.resume();

  const waveType = document.querySelector('input[name="waveType"]:checked').value;
  const freq = getTemplateFreq();
  const previewDuration = Math.min(parseFloat(document.getElementById('templateDuration').value) || 2.0, 3.0);
  const sampleRate = previewCtx.sampleRate;

  const harmonics = getHarmonicsArray();
  const buffer = AudioProcessor.generateWaveformBuffer(waveType, previewDuration, freq, sampleRate, harmonics);

  previewSource = previewCtx.createBufferSource();
  previewSource.buffer = buffer;
  previewGain = previewCtx.createGain();
  previewGain.gain.value = 0.5;
  previewSource.connect(previewGain);
  previewGain.connect(previewCtx.destination);
  previewSource.start();
  previewSource.onended = () => stopPreview();

  templatePreviewBtn.classList.add('playing');
  templatePreviewBtn.textContent = '⏹ 停止';
}

templatePreviewBtn.addEventListener('click', () => {
  if (templatePreviewBtn.classList.contains('playing')) {
    stopPreview();
  } else {
    playPreview();
  }
});

// Auto-preview on wave type change + update harmonics
document.querySelectorAll('input[name="waveType"]').forEach(radio => {
  radio.addEventListener('change', () => {
    setHarmonicsPreset(radio.value);
    if (templateModal.classList.contains('open')) {
      playPreview();
    }
  });
});

// Open/close
openTemplateBtn.addEventListener('click', (e) => {
  e.stopPropagation();
  templateModal.classList.add('open');
});

templateCancelBtn.addEventListener('click', () => {
  stopPreview();
  templateModal.classList.remove('open');
});

templateModal.addEventListener('click', (e) => {
  if (e.target === templateModal) { stopPreview(); templateModal.classList.remove('open'); }
});

// Generate
const waveNames = {
  sine: 'サイン波', square: '矩形波', sawtooth: 'ノコギリ波',
  triangle: '三角波', noise: 'ノイズ', pulse: 'パルス波',
  sine_sweep: 'スイープ', metallic: '金属音', metallic_bend: '金属キーン'
};

templateGenerateBtn.addEventListener('click', () => {
  stopPreview();
  if (!audioContext) audioContext = new (window.AudioContext || window.webkitAudioContext)();

  const waveType = document.querySelector('input[name="waveType"]:checked').value;
  const freq = getTemplateFreq();
  const duration = parseFloat(document.getElementById('templateDuration').value) || 2.0;
  const sampleRate = parseInt(document.getElementById('templateSampleRate').value) || 44100;

  const harmonics = getHarmonicsArray();
  const buffer = AudioProcessor.generateWaveformBuffer(waveType, duration, freq, sampleRate, harmonics);

  let freqLabel;
  if (freqModeNote.classList.contains('active')) {
    freqLabel = templateNote.value + templateOctave.value;
  } else {
    freqLabel = Math.round(freq) + 'Hz';
  }
  const name = `${waveNames[waveType] || waveType}_${freqLabel}_${duration}s`;
  loadGeneratedBuffer(buffer, name);
  templateModal.classList.remove('open');
});

// ========== Playback ==========
playBtn.addEventListener('click', () => { isPlaying ? stopAudio() : startAudio(); });

async function startAudio() {
  if (!originalBuffer || !audioContext) return;
  if (audioContext.state === 'suspended') audioContext.resume();
  stopAudio();

  const isClipped = clipStartNorm > 0.001 || clipEndNorm < 0.999;
  let playBuffer;
  let bufferOffset = 0;
  let bufferDuration;

  if (isClipped || !speedCurve.isFlat()) {
    if (!speedCurve.isFlat()) showProcessing('処理中...');
    if (!speedCurve.isFlat()) await new Promise(r => setTimeout(r, 30));
    const clipped = AudioProcessor.extractClippedBuffer(originalBuffer, clipStartNorm, clipEndNorm);
    if (!speedCurve.isFlat()) {
      playBuffer = AudioProcessor.timeStretchBuffer(clipped, (t, dur) => speedCurve.getValueAtTime(t, dur), clipped.duration);
      hideProcessing();
    } else {
      playBuffer = clipped;
    }
    bufferDuration = playBuffer.duration;
  } else {
    playBuffer = originalBuffer;
    bufferDuration = originalBuffer.duration;
  }

  sourceNode = audioContext.createBufferSource();
  sourceNode.buffer = playBuffer;
  const wallClockDuration = pitchCurve.scheduleOnParamAsRate(sourceNode.playbackRate, bufferDuration, audioContext.currentTime, 500);

  let lastNode = sourceNode;
  if (!eqCurve.isFlat()) lastNode = AudioProcessor.createEQFilterChain(audioContext, sourceNode, eqCurve);

  gainNode = audioContext.createGain();
  volumeCurve.scheduleOnParam(gainNode.gain, wallClockDuration, audioContext.currentTime, 300);
  lastNode.connect(gainNode);
  gainNode.connect(audioContext.destination);

  const reverbAmount = parseInt(reverbAmountSlider.value) / 100;
  if (reverbAmount > 0) {
    const ir = AudioProcessor.generateImpulseResponse(audioContext, parseFloat(reverbDecaySlider.value), audioContext.sampleRate);
    const convolver = audioContext.createConvolver(); convolver.buffer = ir;
    const wetGain = audioContext.createGain(); wetGain.gain.value = reverbAmount;
    gainNode.connect(convolver); convolver.connect(wetGain); wetGain.connect(audioContext.destination);
  }

  sourceNode.start(0, bufferOffset, bufferDuration);
  playStartTime = audioContext.currentTime;
  sourceNode.onended = () => { isPlaying = false; updatePlayBtn(); playbackCursor.style.display = 'none'; };

  isPlaying = true; updatePlayBtn(); playbackCursor.style.display = 'block';
  const animate = () => {
    if (!isPlaying) return;
    const elapsed = audioContext.currentTime - playStartTime;
    let p = elapsed / wallClockDuration; if (p > 1) p = 1;
    const norm = clipStartNorm + p * (clipEndNorm - clipStartNorm);
    playbackCursor.style.left = (norm * waveformContainer.getBoundingClientRect().width) + 'px';
    animFrameId = requestAnimationFrame(animate);
  };
  animate();
}

function stopAudio() {
  if (sourceNode) { try { sourceNode.stop(); } catch(e) {} sourceNode.disconnect(); sourceNode = null; }
  if (gainNode) { gainNode.disconnect(); gainNode = null; }
  isPlaying = false; updatePlayBtn(); playbackCursor.style.display = 'none'; cancelAnimationFrame(animFrameId);
}
function updatePlayBtn() { playBtn.textContent = isPlaying ? '⏸ 停止' : '▶ 再生'; }

// ========== Export ==========
downloadBtn.addEventListener('click', async () => {
  if (!originalBuffer) return;
  downloadBtn.disabled = true; showProcessing('エクスポート中...');
  await new Promise(r => setTimeout(r, 50));

  try {
    let workBuffer = AudioProcessor.extractClippedBuffer(originalBuffer, clipStartNorm, clipEndNorm);
    if (!speedCurve.isFlat()) workBuffer = AudioProcessor.timeStretchBuffer(workBuffer, (t, dur) => speedCurve.getValueAtTime(t, dur), workBuffer.duration);

    let totalDuration = 0;
    const steps = 500;
    for (let i = 0; i < steps; i++) {
      const t = (i / steps) * workBuffer.duration;
      totalDuration += (workBuffer.duration / steps) / Math.max(0.1, pitchCurve.getValueAtTime(t, workBuffer.duration));
    }

    const offlineCtx = new OfflineAudioContext(workBuffer.numberOfChannels, Math.ceil(totalDuration * 1.1 * workBuffer.sampleRate), workBuffer.sampleRate);
    const source = offlineCtx.createBufferSource(); source.buffer = workBuffer;
    pitchCurve.scheduleOnParamAsRate(source.playbackRate, workBuffer.duration, 0, 500);

    let lastNode = source;
    if (!eqCurve.isFlat()) lastNode = AudioProcessor.createEQFilterChain(offlineCtx, source, eqCurve);

    const gain = offlineCtx.createGain();
    volumeCurve.scheduleOnParam(gain.gain, workBuffer.duration, 0, 500);
    lastNode.connect(gain);

    const reverbAmount = parseInt(reverbAmountSlider.value) / 100;
    if (reverbAmount > 0) {
      const ir = AudioProcessor.generateImpulseResponse(offlineCtx, parseFloat(reverbDecaySlider.value), workBuffer.sampleRate);
      const convolver = offlineCtx.createConvolver(); convolver.buffer = ir;
      const wetGain = offlineCtx.createGain(); wetGain.gain.value = reverbAmount;
      gain.connect(convolver); convolver.connect(wetGain); wetGain.connect(offlineCtx.destination);
    }
    gain.connect(offlineCtx.destination);
    source.start(0);

    const rendered = await offlineCtx.startRendering();
    let last = rendered.length;
    const trim = rendered.getChannelData(0);
    for (let i = trim.length - 1; i >= 0; i--) { if (Math.abs(trim[i]) > 0.0001) { last = i + 1; break; } }

    const trimmed = new AudioBuffer({ numberOfChannels: rendered.numberOfChannels, length: Math.min(last + 100, rendered.length), sampleRate: rendered.sampleRate });
    for (let ch = 0; ch < rendered.numberOfChannels; ch++) trimmed.getChannelData(ch).set(rendered.getChannelData(ch).subarray(0, trimmed.length));

    const blob = (currentFileType === 'audio/mpeg' || currentFileType === 'audio/mp3') ? AudioProcessor.bufferToMp3(trimmed) : AudioProcessor.bufferToWave(trimmed, 0, trimmed.length);
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a'); a.href = url; a.download = `${currentFileName}_edited.${blob.type.split('/')[1]}`; a.click();
    URL.revokeObjectURL(url);
  } catch (err) { console.error(err); alert('失敗しました。'); } finally { hideProcessing(); downloadBtn.disabled = false; }
});
