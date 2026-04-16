/**
 * Audio Processing Utilities
 */

/**
 * WSOLA (Waveform Similarity Overlap-Add) Time Stretch
 */
function timeStretchBuffer(inputBuffer, getSpeedAtInputTime, inputDuration) {
  const sampleRate = inputBuffer.sampleRate;
  const numChannels = inputBuffer.numberOfChannels;
  const windowSize = 2048;
  const analysisHop = windowSize / 4;

  const hann = new Float32Array(windowSize);
  for (let i = 0; i < windowSize; i++) {
    hann[i] = 0.5 * (1 - Math.cos(2 * Math.PI * i / windowSize));
  }

  let inputSample = 0, outputSample = 0;
  const inputLength = inputBuffer.getChannelData(0).length;
  while (inputSample < inputLength - windowSize) {
    const t = inputSample / sampleRate;
    const speed = Math.max(0.1, getSpeedAtInputTime(t, inputDuration));
    const synthHop = Math.max(1, Math.round(analysisHop / speed));
    inputSample += analysisHop;
    outputSample += synthHop;
  }

  const outputLength = outputSample + windowSize;
  const outputChannels = [];
  const windowSums = new Float32Array(outputLength);

  for (let ch = 0; ch < numChannels; ch++) {
    outputChannels.push(new Float32Array(outputLength));
  }

  for (let ch = 0; ch < numChannels; ch++) {
    const inputData = inputBuffer.getChannelData(ch);
    const output = outputChannels[ch];
    inputSample = 0;
    outputSample = 0;
    while (inputSample < inputLength - windowSize) {
      const t = inputSample / sampleRate;
      const speed = Math.max(0.1, getSpeedAtInputTime(t, inputDuration));
      const synthHop = Math.max(1, Math.round(analysisHop / speed));
      for (let i = 0; i < windowSize; i++) {
        const inIdx = Math.floor(inputSample) + i;
        const outIdx = outputSample + i;
        if (inIdx < inputLength && outIdx < outputLength) {
          output[outIdx] += inputData[inIdx] * hann[i];
          if (ch === 0) windowSums[outIdx] += hann[i];
        }
      }
      inputSample += analysisHop;
      outputSample += synthHop;
    }
  }

  for (let ch = 0; ch < numChannels; ch++) {
    const output = outputChannels[ch];
    for (let i = 0; i < outputLength; i++) {
      if (windowSums[i] > 0.001) output[i] /= windowSums[i];
    }
  }

  let lastNonZero = outputLength;
  for (let i = outputLength - 1; i >= 0; i--) {
    if (Math.abs(outputChannels[0][i]) > 0.0001) { lastNonZero = i + 1; break; }
  }
  const trimmedLength = Math.min(lastNonZero + 100, outputLength);

  const outputBuffer = new AudioBuffer({
    numberOfChannels: numChannels,
    length: trimmedLength,
    sampleRate: sampleRate
  });

  for (let ch = 0; ch < numChannels; ch++) {
    const dst = outputBuffer.getChannelData(ch);
    for (let i = 0; i < trimmedLength; i++) dst[i] = outputChannels[ch][i];
  }

  return outputBuffer;
}

/**
 * EQ Filter Chain Creation
 */
function createEQFilterChain(ctx, startNode, eqCurve) {
  const frequencies = [31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];
  let lastNode = startNode;

  for (const freq of frequencies) {
    const gain = eqCurve.getValueAtFreq(freq);
    if (Math.abs(gain) < 0.3) continue;

    const filter = ctx.createBiquadFilter();
    filter.type = 'peaking';
    filter.frequency.value = freq;
    filter.gain.value = gain;
    filter.Q.value = 1.2;

    lastNode.connect(filter);
    lastNode = filter;
  }
  return lastNode;
}

/**
 * Generate synthetic Impulse Response for Reverb
 */
function generateImpulseResponse(ctx, decayTime, sampleRate) {
  const length = Math.ceil(sampleRate * decayTime);
  const buffer = ctx.createBuffer(2, length, sampleRate);
  for (let ch = 0; ch < 2; ch++) {
    const data = buffer.getChannelData(ch);
    for (let i = 0; i < length; i++) {
      const t = i / sampleRate;
      data[i] = (Math.random() * 2 - 1) * Math.exp(-3 * t / decayTime);
    }
  }
  return buffer;
}

/**
 * Extract AudioBuffer segment
 */
function extractClippedBuffer(buffer, startNorm, endNorm) {
  const startSample = Math.floor(startNorm * buffer.length);
  const endSample = Math.ceil(endNorm * buffer.length);
  const length = endSample - startSample;
  const clipped = new AudioBuffer({
    numberOfChannels: buffer.numberOfChannels,
    length: length,
    sampleRate: buffer.sampleRate
  });

  // 5ms fade-in/out to prevent click noise at clip edges
  const fadeSamples = Math.min(Math.floor(buffer.sampleRate * 0.005), Math.floor(length / 2));

  for (let ch = 0; ch < buffer.numberOfChannels; ch++) {
    const src = buffer.getChannelData(ch);
    const dst = clipped.getChannelData(ch);
    for (let i = 0; i < length; i++) dst[i] = src[startSample + i];

    // Apply fade-in
    if (startNorm > 0.001) {
      for (let i = 0; i < fadeSamples; i++) {
        dst[i] *= i / fadeSamples;
      }
    }
    // Apply fade-out
    if (endNorm < 0.999) {
      for (let i = 0; i < fadeSamples; i++) {
        dst[length - 1 - i] *= i / fadeSamples;
      }
    }
  }
  return clipped;
}

/**
 * MP3 Encoder
 */
function bufferToMp3(buffer) {
  if (!window.lamejs) throw new Error('lamejsライブラリがロードされていません。');
  const channels = buffer.numberOfChannels || 1;
  const sampleRate = buffer.sampleRate || 44100;
  const mp3encoder = new lamejs.Mp3Encoder(channels, sampleRate, 128);
  const mp3Data = [];
  const sampleBlockSize = 1152;
  const leftChannel = buffer.getChannelData(0);
  const rightChannel = channels > 1 ? buffer.getChannelData(1) : undefined;
  const length = leftChannel.length;

  for (let i = 0; i < length; i += sampleBlockSize) {
    const end = Math.min(i + sampleBlockSize, length);
    const subLen = end - i;
    const leftChunk = new Int16Array(subLen);
    const rightChunk = channels > 1 ? new Int16Array(subLen) : undefined;
    for (let j = 0; j < subLen; j++) {
      let s = Math.max(-1, Math.min(1, leftChannel[i + j]));
      leftChunk[j] = s < 0 ? s * 0x8000 : s * 0x7FFF;
      if (rightChunk) {
        let s2 = Math.max(-1, Math.min(1, rightChannel[i + j]));
        rightChunk[j] = s2 < 0 ? s2 * 0x8000 : s2 * 0x7FFF;
      }
    }
    const mp3buf = channels === 1
      ? mp3encoder.encodeBuffer(leftChunk)
      : mp3encoder.encodeBuffer(leftChunk, rightChunk);
    if (mp3buf.length > 0) mp3Data.push(mp3buf);
  }
  const mp3buf = mp3encoder.flush();
  if (mp3buf.length > 0) mp3Data.push(mp3buf);
  return new Blob(mp3Data, { type: 'audio/mp3' });
}

/**
 * WAV Encoder
 */
function bufferToWave(abuffer, offset, len) {
  const numOfChan = abuffer.numberOfChannels;
  const length = len * numOfChan * 2 + 44;
  const buffer = new ArrayBuffer(length);
  const view = new DataView(buffer);
  const channels = [];
  let sample, pos = 0;

  function writeString(view, offset, string) {
    for (let i = 0; i < string.length; i++) view.setUint8(offset + i, string.charCodeAt(i));
  }

  writeString(view, 0, 'RIFF');
  view.setUint32(4, 36 + len * numOfChan * 2, true);
  writeString(view, 8, 'WAVE');
  writeString(view, 12, 'fmt ');
  view.setUint32(16, 16, true);
  view.setUint16(20, 1, true);
  view.setUint16(22, numOfChan, true);
  view.setUint32(24, abuffer.sampleRate, true);
  view.setUint32(28, abuffer.sampleRate * 2 * numOfChan, true);
  view.setUint16(32, numOfChan * 2, true);
  view.setUint16(34, 16, true);
  writeString(view, 36, 'data');
  view.setUint32(40, len * numOfChan * 2, true);

  for (let i = 0; i < numOfChan; i++) channels.push(abuffer.getChannelData(i));

  pos = 44;
  let offsetP = 0;
  while (pos < length) {
    for (let i = 0; i < numOfChan; i++) {
      sample = Math.max(-1, Math.min(1, channels[i][offsetP]));
      sample = (sample < 0 ? sample * 0x8000 : sample * 0x7FFF) | 0;
      view.setInt16(pos, sample, true);
      pos += 2;
    }
    offsetP++;
  }

  return new Blob([buffer], { type: 'audio/wav' });
}

/**
 * Generate template waveform buffer
 * @param {string} type - Wave type identifier
 * @param {number} duration - Duration in seconds
 * @param {number} frequency - Base frequency in Hz (ignored for noise)
 * @param {number} sampleRate - Sample rate
 * @param {number[]|null} harmonics - Optional array of harmonic amplitudes (0-100) for additive synthesis
 * @returns {AudioBuffer}
 */
function generateWaveformBuffer(type, duration, frequency, sampleRate = 44100, harmonics = null) {
  const length = Math.ceil(sampleRate * duration);
  const buffer = new AudioBuffer({
    numberOfChannels: 1,
    length: length,
    sampleRate: sampleRate
  });
  const data = buffer.getChannelData(0);
  const twoPi = 2 * Math.PI;

  // 5ms fade-in/out to prevent clicks
  const fadeSamples = Math.min(Math.floor(sampleRate * 0.005), Math.floor(length / 2));

  // For tonal wave types, use additive synthesis when harmonics are provided
  const tonalTypes = ['sine', 'square', 'sawtooth', 'triangle', 'pulse'];
  const useAdditive = harmonics && harmonics.some(h => h > 0) && tonalTypes.includes(type);

  for (let i = 0; i < length; i++) {
    const t = i / sampleRate;

    if (useAdditive) {
      let sample = 0;
      for (let h = 0; h < harmonics.length; h++) {
        if (harmonics[h] > 0) {
          sample += (harmonics[h] / 100) * Math.sin(twoPi * frequency * (h + 1) * t);
        }
      }
      data[i] = sample;
    } else {
      const phase = (t * frequency) % 1;

      switch (type) {
        case 'sine':
          data[i] = Math.sin(twoPi * frequency * t);
          break;
        case 'square':
          data[i] = phase < 0.5 ? 0.8 : -0.8;
          break;
        case 'sawtooth':
          data[i] = 2 * phase - 1;
          break;
        case 'triangle':
          data[i] = 1 - 4 * Math.abs(phase - 0.5);
          break;
        case 'noise':
          data[i] = Math.random() * 2 - 1;
          break;
        case 'pulse': {
          data[i] = phase < 0.25 ? 0.8 : -0.8;
          break;
        }
        case 'sine_sweep': {
          const startFreq = frequency;
          const endFreq = frequency * 10;
          const k = (endFreq - startFreq) / duration;
          data[i] = Math.sin(twoPi * (startFreq * t + 0.5 * k * t * t));
          break;
        }
        case 'metallic': {
          const partials = [
            { ratio: 1.0,   amp: 1.0,  decay: 1.0 },
            { ratio: 2.76,  amp: 0.6,  decay: 1.5 },
            { ratio: 5.40,  amp: 0.4,  decay: 2.0 },
            { ratio: 8.93,  amp: 0.25, decay: 3.0 },
            { ratio: 13.34, amp: 0.15, decay: 4.0 },
            { ratio: 18.64, amp: 0.08, decay: 5.0 },
          ];
          let sample = 0;
          for (const p of partials) {
            sample += p.amp * Math.sin(twoPi * frequency * p.ratio * t) * Math.exp(-p.decay * t / (duration * 0.4));
          }
          data[i] = sample * 0.5;
          break;
        }
        case 'metallic_bend': {
          // Rising-pitch bright metallic "キーン✨"
          // Pitch sweeps UP from ~0.7x to 1.0x for an uplifting feel
          const bendRate = 60;
          const pitchDrop = 0.3;
          const sparklePartials = [
            { ratio: 1,  amp: 0.7,  decay: 0.8 },
            { ratio: 2,  amp: 1.0,  decay: 0.6 },
            { ratio: 3,  amp: 0.6,  decay: 0.5 },
            { ratio: 4,  amp: 0.8,  decay: 0.45 },
            { ratio: 5,  amp: 0.5,  decay: 0.4 },
            { ratio: 6,  amp: 0.3,  decay: 0.35 },
            { ratio: 8,  amp: 0.2,  decay: 0.3 },
          ];
          let sparkleSample = 0;
          for (const p of sparklePartials) {
            // Phase integration for smooth upward pitch sweep
            const ph = frequency * p.ratio * (t + (pitchDrop / bendRate) * (1 - Math.exp(-bendRate * t)));
            const env = Math.exp(-p.decay * t / (duration * 0.25));
            sparkleSample += p.amp * Math.sin(twoPi * ph) * env;
          }
          // Shimmer with slight detune
          const shimPhase1 = frequency * 4.01 * (t + (pitchDrop / bendRate) * (1 - Math.exp(-bendRate * t)));
          const shimPhase2 = frequency * 6.02 * (t + (pitchDrop / bendRate) * (1 - Math.exp(-bendRate * t)));
          const shimmer = 0.15 * Math.sin(twoPi * shimPhase1) * Math.exp(-t * 4)
                        + 0.1  * Math.sin(twoPi * shimPhase2) * Math.exp(-t * 5);
          data[i] = (sparkleSample * 0.3 + shimmer) * Math.min(1.0, t * 5000);
          break;
        }
        default:
          data[i] = Math.sin(twoPi * frequency * t);
      }
    }
  }

  // Normalize additive synthesis output
  if (useAdditive) {
    let maxAbs = 0;
    for (let i = 0; i < length; i++) {
      if (Math.abs(data[i]) > maxAbs) maxAbs = Math.abs(data[i]);
    }
    if (maxAbs > 0.001) {
      const scale = 0.95 / maxAbs;
      for (let i = 0; i < length; i++) data[i] *= scale;
    }
  }

  // Apply fade-in
  for (let i = 0; i < fadeSamples; i++) {
    data[i] *= i / fadeSamples;
  }
  // Apply fade-out
  for (let i = 0; i < fadeSamples; i++) {
    data[length - 1 - i] *= i / fadeSamples;
  }

  return buffer;
}

// グローバルにアクセスできるように公開
window.AudioProcessor = {
  timeStretchBuffer,
  createEQFilterChain,
  generateImpulseResponse,
  extractClippedBuffer,
  bufferToMp3,
  bufferToWave,
  generateWaveformBuffer
};
