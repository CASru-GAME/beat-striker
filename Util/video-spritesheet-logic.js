/**
 * Video Spritesheet Generator - Logic
 * (No exports, global scope for file:// protocol compatibility)
 */

// --- Global variables for UI elements (assigned in initializeUI) ---
let dropZone, dropIcon, dropText, dropFilename, videoInput, intervalInput;
let targetFrameSizeInput, trimStartInput, trimEndInput;
let whiteThresholdRange, whiteThresholdVal, greenThresholdRange, greenThresholdVal;
let greenScreenMode, forceWhiteMode, zoomRange, zoomVal, processBtn, downloadBtn;
let progressContainer, progressFill, progressText, statsSection;
let previewPlaceholder, spritesheetCanvas, spritesheetCtx, previewInfo;
let hiddenVideo, frameCanvas, frameCtx;

let videoFile = null;
let isProcessing = false;
let detectedVideoFps = 30; // 検出された動画のベースFPSを保持

/**
 * Initialize the tool logic and event listeners
 */
function initializeSpritesheetTool() {
  // Map elements
  dropZone = document.getElementById('dropZone');
  dropIcon = document.getElementById('dropIcon');
  dropText = document.getElementById('dropText');
  dropFilename = document.getElementById('dropFilename');
  videoInput = document.getElementById('videoInput');
  intervalInput = document.getElementById('intervalInput');
  targetFrameSizeInput = document.getElementById('targetFrameSize');
  trimStartInput = document.getElementById('trimStart');
  trimEndInput = document.getElementById('trimEnd');
  whiteThresholdRange = document.getElementById('whiteThresholdRange');
  whiteThresholdVal = document.getElementById('whiteThresholdVal');
  greenThresholdRange = document.getElementById('greenThresholdRange');
  greenThresholdVal = document.getElementById('greenThresholdVal');
  greenScreenMode = document.getElementById('greenScreenMode');
  forceWhiteMode = document.getElementById('forceWhiteMode');
  zoomRange = document.getElementById('zoomRange');
  zoomVal = document.getElementById('zoomVal');
  processBtn = document.getElementById('processBtn');
  downloadBtn = document.getElementById('downloadBtn');
  progressContainer = document.getElementById('progressContainer');
  progressFill = document.getElementById('progressFill');
  progressText = document.getElementById('progressText');
  statsSection = document.getElementById('statsSection');
  previewPlaceholder = document.getElementById('previewPlaceholder');
  spritesheetCanvas = document.getElementById('spritesheetCanvas');
  spritesheetCtx = spritesheetCanvas.getContext('2d');
  previewInfo = document.getElementById('previewInfo');
  hiddenVideo = document.getElementById('hiddenVideo');
  frameCanvas = document.getElementById('frameCanvas');
  frameCtx = frameCanvas.getContext('2d');

  // --- Drop Zone ---
  dropZone.addEventListener('click', () => videoInput.click());

  ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(evt => {
    dropZone.addEventListener(evt, e => { e.preventDefault(); e.stopPropagation(); });
  });

  ['dragenter', 'dragover'].forEach(evt => {
    dropZone.addEventListener(evt, () => dropZone.classList.add('drag-over'));
  });

  ['dragleave', 'drop'].forEach(evt => {
    dropZone.addEventListener(evt, () => dropZone.classList.remove('drag-over'));
  });

  dropZone.addEventListener('drop', e => {
    const files = e.dataTransfer.files;
    if (files.length > 0) setVideoFile(files[0]);
  });

  videoInput.addEventListener('change', e => {
    if (e.target.files.length > 0) setVideoFile(e.target.files[0]);
  });

  // --- UI Listeners ---
  zoomRange.addEventListener('input', e => {
    zoomVal.textContent = e.target.value;
  });

  whiteThresholdRange.addEventListener('input', e => {
    whiteThresholdVal.textContent = e.target.value;
  });

  greenThresholdRange.addEventListener('input', e => {
    greenThresholdVal.textContent = e.target.value;
  });

  processBtn.addEventListener('click', () => {
    if (!videoFile || isProcessing) return;
    startProcessing();
  });

  downloadBtn.addEventListener('click', () => {
    if (spritesheetCanvas.width === 0) return;
    const link = document.createElement('a');
    link.download = `spritesheet_${Date.now()}.png`;
    link.href = spritesheetCanvas.toDataURL('image/png');
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  });
}

function setVideoFile(file) {
  const isVideo = file.type.startsWith('video/');
  const isMov = file.name.toLowerCase().endsWith('.mov');
  
  if (!isVideo && !isMov) {
    alert('動画ファイルを選択してください (.mp4, .mov, .webm 等)');
    return;
  }
  videoFile = file;
  dropZone.classList.add('has-file');
  dropIcon.textContent = '✅';
  dropText.textContent = '動画ファイル選択済み';
  dropFilename.textContent = file.name;
  dropFilename.style.display = 'block';
  processBtn.disabled = false;

  detectAndSetFPS(file);
}

async function detectAndSetFPS(file) {
  const tempVideo = document.createElement('video');
  tempVideo.muted = true;
  tempVideo.playsInline = true;
  const videoUrl = URL.createObjectURL(file);
  tempVideo.src = videoUrl;

  intervalInput.disabled = true;
  const originalVal = intervalInput.value;
  intervalInput.value = '';
  intervalInput.placeholder = '検出中...';

  try {
    await new Promise((resolve, reject) => {
      tempVideo.onloadedmetadata = resolve;
      tempVideo.onerror = reject;
      setTimeout(() => reject(new Error('Timeout')), 5000);
    });

    const fps = await estimateFPS(tempVideo);
    if (fps > 0) {
      const commonFPS = [23.976, 24, 25, 29.97, 30, 50, 59.94, 60, 120];
      let closest = fps;
      let minDiff = 1.0;
      for (const c of commonFPS) {
        const diff = Math.abs(fps - c);
        if (diff < minDiff && diff < 1.0) {
          minDiff = diff;
          closest = c;
        }
      }
      detectedVideoFps = Math.round(closest * 100) / 100;
    } else {
      detectedVideoFps = 30;
    }
    intervalInput.value = originalVal || 1;
  } catch (e) {
    console.warn('FPS 検出に失敗しました:', e);
    detectedVideoFps = 30;
    intervalInput.value = originalVal || 1;
  } finally {
    intervalInput.disabled = false;
    intervalInput.placeholder = '';
    URL.revokeObjectURL(videoUrl);
    tempVideo.remove();
  }
}

function estimateFPS(video) {
  return new Promise((resolve) => {
    if (!video.requestVideoFrameCallback) {
      resolve(30);
      return;
    }

    let startMediaTime = null;
    let startPresentedFrames = null;
    const requiredFrames = 15;

    function checkFrame(now, metadata) {
      if (startMediaTime === null) {
        startMediaTime = metadata.mediaTime;
        startPresentedFrames = metadata.presentedFrames;
        video.requestVideoFrameCallback(checkFrame);
      } else {
        const framesDiff = metadata.presentedFrames - startPresentedFrames;
        const timeDiff = metadata.mediaTime - startMediaTime;

        if (framesDiff >= requiredFrames) {
          const estimatedFps = framesDiff / timeDiff;
          video.pause();
          resolve(estimatedFps);
        } else {
          video.requestVideoFrameCallback(checkFrame);
        }
      }
    }

    video.play().then(() => {
      video.requestVideoFrameCallback(checkFrame);
    }).catch(() => resolve(30));
  });
}

async function startProcessing() {
  isProcessing = true;
  processBtn.disabled = true;
  downloadBtn.disabled = true;
  document.body.classList.add('processing');

  progressContainer.classList.add('visible');
  progressFill.style.width = '0%';
  progressText.textContent = '動画を読み込み中...';

  const interval = parseInt(intervalInput.value) || 1;
  const targetFrameSize = parseInt(targetFrameSizeInput.value) || 256;
  const trimStart = parseInt(trimStartInput.value) || 0;
  const trimEnd = parseInt(trimEndInput.value) || 0;
  const whiteThresholdAdj = parseInt(whiteThresholdRange.value) || 0;
  const greenThresholdAdj = parseInt(greenThresholdRange.value) || 0;
  const zoomRate = (parseInt(zoomRange.value) || 100) / 100;
  const isGreenScreen = greenScreenMode.checked;
  const isForceWhite = forceWhiteMode.checked;

  try {
    const videoUrl = URL.createObjectURL(videoFile);
    while (hiddenVideo.firstChild) hiddenVideo.removeChild(hiddenVideo.firstChild);
    
    const source = document.createElement('source');
    source.src = videoUrl;
    source.type = videoFile.type || (videoFile.name.toLowerCase().endsWith('.mov') ? 'video/quicktime' : 'video/mp4');
    hiddenVideo.appendChild(source);
    hiddenVideo.load();

    await new Promise((resolve, reject) => {
      const timeout = setTimeout(() => {
        reject(new Error('動画の読み込みがタイムアウトしました。'));
      }, 10000);

      hiddenVideo.onloadedmetadata = () => {
        clearTimeout(timeout);
        resolve();
      };
      hiddenVideo.onerror = (e) => {
        clearTimeout(timeout);
        reject(new Error('動画の読み込みに失敗しました'));
      };
    });

    const duration = hiddenVideo.duration;
    const baseFps = detectedVideoFps || 30;
    
    // 総フレーム数をベースFPSから算出
    const totalFramesInVideo = Math.floor(duration * baseFps);
    // 実際に抽出するフレーム数
    const rawSamplingFrames = Math.floor(totalFramesInVideo / interval);
    const totalFrames = Math.max(0, rawSamplingFrames - trimStart - trimEnd);

    if (totalFrames <= 0) throw new Error('トリミング後のフレームが0以下です。');

    // --- 自動レイアウト計算 ---
    const findBestPo2Layout = (framesCount, targetSize) => {
      let sheetSize = 256;
      let cols = Math.ceil(Math.sqrt(framesCount));
      let minRequired = cols * targetSize;
      while (sheetSize < minRequired && sheetSize < 8192) sheetSize *= 2;

      const fw = Math.floor(sheetSize / cols);
      const fh = fw;
      return { sheetSize, cols, fw, fh };
    };

    const { sheetSize, cols, fw, fh } = findBestPo2Layout(totalFrames, targetFrameSize);

    // Stats
    document.getElementById('statDuration').textContent = duration.toFixed(1) + 's';
    document.getElementById('statFrames').textContent = totalFrames;
    document.getElementById('statFrameSize').textContent = `${fw}×${fh}px`;
    document.getElementById('statGrid').textContent = `${cols}×${Math.ceil(totalFrames / cols)}`;
    document.getElementById('statSize').textContent = `${sheetSize}×${sheetSize}`;

    progressText.textContent = `${totalFrames}フレームを抽出中...`;

    frameCanvas.width = fw;
    frameCanvas.height = fh;
    frameCtx.imageSmoothingEnabled = true;
    frameCtx.imageSmoothingQuality = 'high';

    const vw = hiddenVideo.videoWidth;
    const vh = hiddenVideo.videoHeight;
    const baseScale = Math.min(fw / vw, fh / vh);
    const finalScale = baseScale * zoomRate;
    const drawW = vw * finalScale;
    const drawH = vh * finalScale;
    const drawX = (fw - drawW) / 2;
    const drawY = (fh - drawH) / 2;

    const frames = [];
    for (let i = 0; i < totalFrames; i++) {
      const samplingIdxInProcess = i + trimStart;
      const originalFrameIdx = samplingIdxInProcess * interval;
      
      // シーク位置を正確な秒数で指定
      let time = originalFrameIdx / baseFps;
      
      // 極端な端数によるオーバーラン防止（最終フレームガード）
      if (time >= duration) time = duration - (1 / baseFps / 2); 

      await seekToTime(hiddenVideo, time);

      frameCtx.clearRect(0, 0, fw, fh);
      frameCtx.drawImage(hiddenVideo, drawX, drawY, drawW, drawH);

      const imageData = frameCtx.getImageData(0, 0, fw, fh);
      applyTransparencyEffect(imageData, whiteThresholdAdj, greenThresholdAdj, isGreenScreen, isForceWhite);

      frameCtx.putImageData(imageData, 0, 0);
      const blob = await new Promise(resolve => frameCanvas.toBlob(resolve, 'image/png'));
      const bitmap = await createImageBitmap(blob);
      frames.push(bitmap);

      const pct = ((i + 1) / totalFrames * 100).toFixed(0);
      progressFill.style.width = pct + '%';
      progressText.textContent = `フレーム抽出中... ${i + 1}/${totalFrames}`;

      if (i % 5 === 0) await yieldToUI();
    }

    URL.revokeObjectURL(videoUrl);

    progressText.textContent = 'スプライトシートを合成中...';
    await yieldToUI();

    spritesheetCanvas.width = sheetSize;
    spritesheetCanvas.height = sheetSize;
    spritesheetCtx.imageSmoothingEnabled = true;
    spritesheetCtx.imageSmoothingQuality = 'high';
    spritesheetCtx.clearRect(0, 0, sheetSize, sheetSize);

    for (let idx = 0; idx < frames.length; idx++) {
      const col = idx % cols;
      const row = Math.floor(idx / cols);
      spritesheetCtx.drawImage(frames[idx], col * fw, row * fh, fw, fh);
    }

    statsSection.style.display = 'block';
    statsSection.classList.add('fade-in');
    previewPlaceholder.style.display = 'none';
    spritesheetCanvas.style.display = 'block';
    previewInfo.textContent = `${sheetSize} × ${sheetSize}px | ${frames.length}フレーム | ${cols}列`;

    downloadBtn.disabled = false;
    progressFill.style.width = '100%';
    progressText.textContent = '✅ 完了！';

    frames.forEach(b => b.close());
  } catch (err) {
    alert('エラー: ' + err.message);
    progressText.textContent = '❌ エラーが発生しました';
  } finally {
    isProcessing = false;
    processBtn.disabled = !videoFile;
    document.body.classList.remove('processing');
  }
}

function seekToTime(video, time) {
  return new Promise((resolve) => {
    const timeout = setTimeout(() => {
      video.removeEventListener('seeked', onSeeked);
      video.removeEventListener('error', onError);
      resolve();
    }, 1000);

    video.currentTime = Math.max(0, time);

    function onSeeked() {
      clearTimeout(timeout);
      video.removeEventListener('seeked', onSeeked);
      video.removeEventListener('error', onError);
      setTimeout(resolve, 50);
    }

    function onError() {
      clearTimeout(timeout);
      video.removeEventListener('seeked', onSeeked);
      video.removeEventListener('error', onError);
      resolve(); 
    }

    video.addEventListener('seeked', onSeeked);
    video.addEventListener('error', onError);
  });
}

function yieldToUI() {
  return new Promise(resolve => setTimeout(resolve, 0));
}

/**
 * Transparency & Effect Logic
 */
function applyTransparencyEffect(imageData, whiteThreshold = 0, greenThreshold = 0, isGreenScreen = false, isForceWhite = true) {
  if (isGreenScreen) {
    const data = imageData.data;
    const threshold = 50 + greenThreshold; 
    
    for (let i = 0; i < data.length; i += 4) {
      const r = data[i];
      const g = data[i + 1];
      const b = data[i + 2];
      const a = data[i + 3];
      const greenness = g - Math.max(r, b);
      
      let alpha = a;
      if (greenness > threshold) {
        alpha = 0;
      } else if (greenness > threshold - 20) {
        const diff = threshold - greenness;
        alpha = (diff / 20) * 255;
      }

      if (isForceWhite) {
        data[i] = 255; data[i + 1] = 255; data[i + 2] = 255;
      }
      data[i + 3] = alpha;
    }
  } else {
    // Call common module for white transparency
    applyWhiteTransparency(imageData, whiteThreshold, isForceWhite);
  }
}

// Initial call
document.addEventListener('DOMContentLoaded', initializeSpritesheetTool);
