using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class AudioSpectrum : MonoBehaviour {
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
    public Gradient colorGradient; // Inspectorで色グラデーションをセット
    [SerializeField] float convergenceReferenceValue = 100f;
    [SerializeField, Range(0f, 1f)] float convergenceRatioAtReference = 0.4f;

    [SerializeField] AudioSource source;

    private bool wasPlaying = false;
    private float[] editorBarHeights;

    void Awake() {
        data = new float[0];
        spectrumData = new float[(int)fftRes];
        editorBarHeights = new float[bars.Length];
        for (int i = 0; i < bars.Length; i++) {
            editorBarHeights[i] = bars[i].rectTransform.sizeDelta.y;
        }
        ResetBars();
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

        // 音楽が再生中ならスペクトラムを更新
        bool cond = source.timeSamples < data.Length;

        if (cond) {
            source.GetSpectrumData(spectrumData, 0, fftWf);
        }
        else {
            // 再生していないときはゼロ配列
            spectrumData = Enumerable.Repeat<float>(0, (int)fftRes).ToArray();
        }

        int len = Mathf.Min(bars.Length, spectrumData.Length);
        for (int i = 0; i < len; i++) {
            float value = spectrumData[i] * heightMultiplier;
            value = Mathf.Clamp(value, 2f, heightMultiplier);

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
    }

    private void ResetBars() {
        // barの高さをEditorで設定した初期値に戻す
        for (int i = 0; i < bars.Length; i++) {
            bars[i].rectTransform.sizeDelta = new Vector2(bars[i].rectTransform.sizeDelta.x, 0);
        }
    }
}
