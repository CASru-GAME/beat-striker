using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.UI;

public class AudioSpectrum : MonoBehaviour {
    private const uint BAKED_MAGIC_ASB2 = 0x32534241; // ASB2 (deflate payload)
    private const uint BAKED_MAGIC_ASB3 = 0x33425341; // ASB3 (raw packed payload)

    public enum FFT_Resolution {
        _8192 = 8192, _4096 = 4096, _2048 = 2048, _1024 = 1024, _512 = 512, _256 = 256, _128 = 128, _64 = 64
    }

    [Tooltip("64-8192の間の2の累乗。ベイクデータの FFT サイズと一致させる")]
    public FFT_Resolution fftRes = FFT_Resolution._512;
    [HideInInspector] public float[] spectrumData;

    [SerializeField] Image[] bars;
    [SerializeField] float heightMultiplier = 300f;
    [Tooltip("ベイク値の表示倍率。旧 GetSpectrum フォールバック時よりベイクは /FFT で抑えられるため、既定で少し上げる")]
    [SerializeField, Min(0.01f)] float spectrumAmplitudeScale = 1.35f;
    [SerializeField] float spectrumLengthOffset = 0f;
    public Gradient colorGradient;
    [SerializeField] float convergenceReferenceValue = 100f;
    [SerializeField, Range(0f, 1f)] float convergenceRatioAtReference = 0.4f;

    [SerializeField] AudioSource source;
    [SerializeField] TextAsset bakedSpectrumText;

    private bool wasPlaying = false;
    private float[] editorBarHeights;
    private BakedSpectrumCache bakedSpectrumCache;

    void Awake() {
        spectrumData = new float[(int)fftRes];
        bakedSpectrumCache = ParseBakedSpectrum(bakedSpectrumText);
        editorBarHeights = new float[bars.Length];
        for (int i = 0; i < bars.Length; i++) {
            editorBarHeights[i] = bars[i].rectTransform.sizeDelta.y;
        }
        ResetBars();
    }

    public void SetBakedSpectrumText(TextAsset textAsset) {
        bakedSpectrumText = textAsset;
        bakedSpectrumCache = ParseBakedSpectrum(bakedSpectrumText);
    }

    void Update() {
        bool isPlaying = source.isPlaying;

        if (isPlaying && !wasPlaying) {
            spectrumData = new float[(int)fftRes];
        }

        if (!isPlaying && wasPlaying) {
            spectrumData = new float[(int)fftRes];
            ResetBars();
        }

        wasPlaying = isPlaying;

        if (!isPlaying) return;

        if (bakedSpectrumCache != null && bakedSpectrumCache.fftSize == (int)fftRes) {
            CopyBakedSpectrum(source.time, bakedSpectrumCache, spectrumData);
        }
        else {
            for (int i = 0; i < spectrumData.Length; i++) {
                spectrumData[i] = 0f;
            }
        }

        int len = Mathf.Min(bars.Length, spectrumData.Length);
        for (int i = 0; i < len; i++) {
            float value = spectrumData[i] * spectrumAmplitudeScale * heightMultiplier;
            value = Mathf.Clamp(value + spectrumLengthOffset, 0f, heightMultiplier);

            float normalized = Mathf.Clamp01(value / convergenceReferenceValue);
            float convergenceRatio = normalized * convergenceRatioAtReference;
            float convergenceValue = editorBarHeights[i];
            value = Mathf.Lerp(value, convergenceValue, convergenceRatio);

            bars[i].rectTransform.sizeDelta = new Vector2(bars[i].rectTransform.sizeDelta.x, value);

            bars[i].color = colorGradient.Evaluate((float)i / len);
        }

        for (int i = len; i < bars.Length; i++) {
            bars[i].rectTransform.sizeDelta = new Vector2(bars[i].rectTransform.sizeDelta.x, 0f);
        }
    }

    private void ResetBars() {
        for (int i = 0; i < bars.Length; i++) {
            bars[i].rectTransform.sizeDelta = new Vector2(bars[i].rectTransform.sizeDelta.x, 0);
        }
    }

    private static void CopyBakedSpectrum(float time, BakedSpectrumCache cache, float[] destination) {
        int clampedFrameCount = Mathf.Max(cache.frameCount, 1);
        float framePosition = time * cache.frameRate;
        int frameA = Mathf.Clamp(Mathf.FloorToInt(framePosition), 0, clampedFrameCount - 1);
        int frameB = Mathf.Min(frameA + 1, clampedFrameCount - 1);
        float t = framePosition - frameA;

        int offsetA = frameA * cache.fftSize;
        int offsetB = frameB * cache.fftSize;
        int length = Mathf.Min(destination.Length, cache.fftSize);

        for (int i = 0; i < length; i++) {
            float a = cache.flattenedSpectrum[offsetA + i];
            float b = cache.flattenedSpectrum[offsetB + i];
            destination[i] = Mathf.Lerp(a, b, t) / cache.fftSize;
        }

        for (int i = length; i < destination.Length; i++) {
            destination[i] = 0f;
        }
    }

    private static BakedSpectrumCache ParseBakedSpectrum(TextAsset sourceText) {
        if (sourceText == null) {
            return null;
        }

        byte[] bytes = sourceText.bytes;
        if (bytes == null || bytes.Length < 29) {
            return null;
        }

        using (MemoryStream stream = new MemoryStream(bytes)) {
            using (BinaryReader reader = new BinaryReader(stream)) {
                uint magic = reader.ReadUInt32();
                bool asb3 = magic == BAKED_MAGIC_ASB3;
                bool asb2 = magic == BAKED_MAGIC_ASB2;
                if (!asb3 && !asb2) {
                    return null;
                }

                int fftSize = reader.ReadInt32();
                int frameRate = reader.ReadInt32();
                int frameCount = reader.ReadInt32();
                float maxMagnitude = reader.ReadSingle();
                byte quantizeBits = reader.ReadByte();
                int packedLength = reader.ReadInt32();
                int compressedLength = reader.ReadInt32();

                int expectedLength = fftSize * frameCount;
                if (quantizeBits != 4) {
                    return null;
                }
                if (packedLength != (expectedLength + 1) / 2) {
                    return null;
                }
                if (compressedLength < 0 || bytes.Length < 29 + compressedLength) {
                    return null;
                }

                byte[] storedPayload = reader.ReadBytes(compressedLength);
                byte[] packed;
                if (asb3) {
                    if (storedPayload.Length != packedLength) {
                        return null;
                    }

                    packed = storedPayload;
                }
                else {
                    packed = Inflate(storedPayload, packedLength);
                    if (packed == null || packed.Length != packedLength) {
                        return null;
                    }
                }

                byte[] quantized = Unpack4Bit(packed, expectedLength);

                float[] flattenedSpectrum = new float[expectedLength];
                for (int i = 0; i < expectedLength; i++) {
                    float compressed = quantized[i] / 15f;
                    float normalized = compressed * compressed;
                    flattenedSpectrum[i] = normalized * maxMagnitude;
                }

                return new BakedSpectrumCache(fftSize, frameRate, frameCount, flattenedSpectrum);
            }
        }
    }

    private static byte[] Inflate(byte[] compressed, int expectedLength) {
        try {
            using (MemoryStream input = new MemoryStream(compressed)) {
                using (DeflateStream decompressor = new DeflateStream(input, CompressionMode.Decompress)) {
                    byte[] buffer = new byte[expectedLength];
                    int total = 0;
                    int read;
                    while (total < expectedLength && (read = decompressor.Read(buffer, total, expectedLength - total)) > 0) {
                        total += read;
                    }

                    if (total != expectedLength) {
                        return null;
                    }

                    return buffer;
                }
            }
        }
        catch {
            return null;
        }
    }

    private static byte[] Unpack4Bit(byte[] packed, int sampleCount) {
        byte[] unpacked = new byte[sampleCount];
        for (int i = 0; i < sampleCount; i++) {
            byte p = packed[i >> 1];
            unpacked[i] = (byte)(((i & 1) == 0) ? ((p >> 4) & 0x0F) : (p & 0x0F));
        }
        return unpacked;
    }

    private sealed class BakedSpectrumCache {
        public int fftSize;
        public int frameRate;
        public int frameCount;
        public float[] flattenedSpectrum;

        public BakedSpectrumCache(int targetFftSize, int targetFrameRate, int targetFrameCount, float[] targetFlattenedSpectrum) {
            fftSize = targetFftSize;
            frameRate = targetFrameRate;
            frameCount = targetFrameCount;
            flattenedSpectrum = targetFlattenedSpectrum;
        }
    }
}
