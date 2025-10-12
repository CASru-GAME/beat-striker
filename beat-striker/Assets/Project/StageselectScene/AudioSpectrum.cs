using UnityEngine;
using UnityEngine.UI;

public class AudioSpectrum : MonoBehaviour
{
    public GetAudioData audioData; // FFTデータ取得用
    public Image[] bars;           // InspectorでバーImageをセット
    public float heightMultiplier = 300f;
    public Gradient colorGradient; // Inspectorで色グラデーションをセット

    void Update()
    {
        if (audioData == null || audioData.spectrumData == null) return;

        int len = Mathf.Min(bars.Length, audioData.spectrumData.Length);
        for (int i = 0; i < len; i++)
        {
            float value = audioData.spectrumData[i] * heightMultiplier;
            value = Mathf.Clamp(value, 2f, heightMultiplier);

            // バーの高さ変更
            bars[i].rectTransform.sizeDelta = new Vector2(bars[i].rectTransform.sizeDelta.x, value);

            // 色変更（低音→高音でグラデーション）
            bars[i].color = colorGradient.Evaluate((float)i / len);
        }
    }
}
