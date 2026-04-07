using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.UI;

public class AudioSpectrum : MonoBehaviour {
    private const uint BAKED_MAGIC = 0x32534241; // ASB2

    // FFT 設定 (GetAudioData から吸収)
    public enum FFT_Resolution {
        _8192 = 8192, _4096 = 4096, _2048 = 2048, _1024 = 1024, _512 = 512, _256 = 256, _128 = 128, _64 = 64
    }

    [Tooltip("64-8192の間の2の累乗の数字である必要がある")]
    public FFT_Resolution fftRes = FFT_Resolution._512;
    [SerializeField] private int dataOffset = 0;
    [Tooltip("高速フーリエ変換の窓関数指定")]
    [SerializeField] private FFTWindow fftWf = FFTWindow.Triangle;
    [HideInInspector] public float[] spectrumData;
    private float[] data;

    // UI / 設定
    [SerializeField] Image[] bars;           // InspectorでバーImageをセット
    [SerializeField] float heightMultiplier = 300f;
    [SerializeField] float spectrumLengthOffset = 0f;
    public Gradient colorGradient; // Inspectorで色グラデーションをセット
    [SerializeField] float convergenceReferenceValue = 100f;
    [SerializeField, Range(0f, 1f)] float convergenceRatioAtReference = 0.4f;

    [SerializeField] AudioSource source;
    [SerializeField] TextAsset bakedSpectrumText;

    private bool wasPlaying = false;
    private float[] editorBarHeights;
    private BakedSpectrumCache bakedSpectrumCache;

    void Awake() {
        data = new float[0];
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
            // 再生開始: 初期化
            if (source.clip != null) {
                var clip = source.clip;
                data = new float[clip.channels * clip.samples];
                clip.GetData(data, dataOffset);
            }
            else {
                data = new float[0];
            }
            spectrumData = new float[(int)fftRes];
        }

        if (!isPlaying && wasPlaying) {
            // 再生停止: データとbarをリセット
            data = new float[0];
            spectrumData = new float[(int)fftRes];
            ResetBars();
        }

        wasPlaying = isPlaying;

        if (!isPlaying) return;

        if (bakedSpectrumCache != null && bakedSpectrumCache.fftSize == (int)fftRes) {
            CopyBakedSpectrum(source.time, bakedSpectrumCache, spectrumData);
        }
        else {
            // フォールバック: 音楽が再生中ならスペクトラムを更新
            bool cond = source.timeSamples < data.Length;
            if (cond) {
                source.GetSpectrumData(spectrumData, 0, fftWf);
            }
            else {
                for (int i = 0; i < spectrumData.Length; i++) {
                    spectrumData[i] = 0f;
                }
            }
        }

        int len = Mathf.Min(bars.Length, spectrumData.Length);
        for (int i = 0; i < len; i++) {
            float value = spectrumData[i] * heightMultiplier;
            value = Mathf.Clamp(value + spectrumLengthOffset, 0f, heightMultiplier);

            // 入力値が基準値のときに指定割合で収束値へ近づく
            float normalized = Mathf.Clamp01(value / convergenceReferenceValue);
            float convergenceRatio = normalized * convergenceRatioAtReference;
            float convergenceValue = editorBarHeights[i];
            value = Mathf.Lerp(value, convergenceValue, convergenceRatio);

            // バーの高さ変更
            bars[i].rectTransform.sizeDelta = new Vector2(bars[i].rectTransform.sizeDelta.x, value);

            // 色変更（低音→高音でグラデーション）
            bars[i].color = colorGradient.Evaluate((float)i / len);
        }

        for (int i = len; i < bars.Length; i++) {
            bars[i].rectTransform.sizeDelta = new Vector2(bars[i].rectTransform.sizeDelta.x, 0f);
        }
    }

    private void ResetBars() {
        // barの高さをEditorで設定した初期値に戻す
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
        if (bytes == null || bytes.Length < 30) {
            return null;
        }

        using (MemoryStream stream = new MemoryStream(bytes)) {
            using (BinaryReader reader = new BinaryReader(stream)) {
                uint magic = reader.ReadUInt32();
                if (magic != BAKED_MAGIC) {
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
                if (compressedLength < 0 || bytes.Length < 33 + compressedLength) {
                    return null;
                }

                byte[] compressedPayload = reader.ReadBytes(compressedLength);
                byte[] packed = Inflate(compressedPayload, packedLength);
                if (packed == null || packed.Length != packedLength) {
                    return null;
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
        using (MemoryStream input = new MemoryStream(compressed)) {
            using (DeflateStream decompressor = new DeflateStream(input, CompressionMode.Decompress)) {
                using (MemoryStream output = new MemoryStream(expectedLength)) {
                    decompressor.CopyTo(output);
                    byte[] result = output.ToArray();
                    if (result.Length != expectedLength) {
                        return null;
                    }
                    return result;
                }
            }
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
