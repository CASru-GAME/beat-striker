using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// ボタンをクリックしたときに効果音を再生するスクリプト
/// ButtonコンポーネントまたはEventTriggerと組み合わせて使用できます
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ButtonClickSound : MonoBehaviour, IPointerClickHandler
{
    [Header("Sound Settings")]
    [Tooltip("クリック時に再生する効果音")]
    public AudioClip clickSound;
    
    [Tooltip("音量（0.0 ~ 1.0）")]
    [Range(0f, 1f)]
    public float volume = 1f;
    
    [Tooltip("音を再生するまでの遅延時間（秒）")]
    [Range(0f, 2f)]
    public float delay = 0f;
    
    [Header("Debounce Settings")]
    [Tooltip("連続クリック防止の間隔（秒）")]
    [Range(0f, 1f)]
    public float debounceTime = 0.1f;
    
    [Tooltip("デバッグログを表示")]
    public bool showDebugLog = false;
    
    [Header("Auto Detection")]
    [Tooltip("ButtonやBotanがある場合、OnPointerClickを無効化（二重再生防止）")]
    public bool autoDetectOtherComponents = true;
    
    private AudioSource audioSource;
    private float lastClickTime = -999f;
    private bool usePointerClick = true;
    
    void Awake()
    {
        // AudioSourceを取得または追加
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // AudioSourceの設定
        audioSource.playOnAwake = false;
        audioSource.volume = volume;
        
        // 他のボタンコンポーネントを検知
        if (autoDetectOtherComponents)
        {
            // Buttonコンポーネントがある場合
            if (GetComponent<Button>() != null)
            {
                usePointerClick = false;
                if (showDebugLog)
                {
                    Debug.Log($"[ButtonClickSound] Button検出 - OnPointerClickを無効化");
                }
            }
            
            // Botanコンポーネントがある場合（名前空間を考慮）
            var botanComponent = GetComponent("Botan");
            if (botanComponent != null)
            {
                usePointerClick = false;
                if (showDebugLog)
                {
                    Debug.Log($"[ButtonClickSound] Botan検出 - OnPointerClickを無効化");
                }
            }
        }
    }
    
    /// <summary>
    /// クリック時に呼ばれるメソッド（IPointerClickHandler）
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        // 他のボタンコンポーネントがある場合は何もしない
        if (!usePointerClick)
        {
            return;
        }
        
        if (showDebugLog)
        {
            Debug.Log($"[ButtonClickSound] OnPointerClick called on {gameObject.name}");
        }
        PlayClickSound();
    }
    
    /// <summary>
    /// 効果音を再生（Buttonの OnClick イベントからも呼び出し可能）
    /// </summary>
    public void PlayClickSound()
    {
        // 連続クリック防止（デバウンス）
        float currentTime = Time.unscaledTime;
        if (currentTime - lastClickTime < debounceTime)
        {
            if (showDebugLog)
            {
                Debug.Log($"[ButtonClickSound] Debounced - 間隔が短すぎます ({currentTime - lastClickTime:F3}秒)");
            }
            return;
        }
        lastClickTime = currentTime;
        
        if (showDebugLog)
        {
            Debug.Log($"[ButtonClickSound] PlayClickSound called on {gameObject.name}");
        }
        
        if (audioSource != null && clickSound != null)
        {
            if (delay > 0f)
            {
                // 遅延ありで再生
                Invoke(nameof(PlaySoundImmediate), delay);
            }
            else
            {
                // 即座に再生
                PlaySoundImmediate();
            }
        }
        else if (clickSound == null)
        {
            Debug.LogWarning($"ButtonClickSound: クリック音が設定されていません ({gameObject.name})");
        }
    }
    
    /// <summary>
    /// 即座に効果音を再生（内部用）
    /// </summary>
    private void PlaySoundImmediate()
    {
        if (audioSource != null && clickSound != null)
        {
            audioSource.PlayOneShot(clickSound, volume);
        }
    }
}
