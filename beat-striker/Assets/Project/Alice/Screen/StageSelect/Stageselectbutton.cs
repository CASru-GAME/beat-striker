using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using R3;
using Alice;
using Core;

[RequireComponent(typeof(Botan))]
[RequireComponent(typeof(AudioSource))]
public class Stageselectbutton : MonoBehaviour
{
    Botan botan;
    public RawImage image;
    public AudioClip hoverSound;
    [Range(0f, 1f)]
    public float hoverSoundVolume = 1f; // ホバー音の音量
    AudioSource audioSource;
    public Panel panel; // Panel参照
    public Transform popParent; // Popupの親Transform
    public enum MoveType { None, Right, Left }
    public MoveType moveType = MoveType.None;
    public MusicPopup popupPrefab; // MusicPopupのPrefab
    private MusicPopup currentPopup; // インスタンス化されたMusicPopup
    private static bool isPopupShown = false;
    
    [Header("Selection")]
    public Stage selectedStage = Stage.Live;
    
    // black表示用
    public GameObject blackObject; // blackのImageオブジェクト
    private CanvasGroup blackCanvasGroup;
    public float blackFadeDuration = 0.5f;
    private bool isHovering = false;
    private bool hasCompletedMove = false;
    IReadOnlyList<MusicInfo> musics;
    readonly Subject<Stage> stageSelected = new();
    readonly Subject<MusicInfo> musicSelected = new();
    readonly CompositeDisposable popupSubscriptions = new();

    public Observable<Stage> OnStageSelected => stageSelected;
    public Observable<MusicInfo> OnMusicSelected => musicSelected;

    public void Initialize(IReadOnlyList<MusicInfo> musics) {
        this.musics = musics;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        botan = GetComponent<Botan>();
        audioSource = GetComponent<AudioSource>();

        image.color = Color.gray;
        
        // blackオブジェクトにCanvasGroupを追加/取得して初期状態で非表示に
        if (blackObject != null)
        {
            blackCanvasGroup = blackObject.GetComponent<CanvasGroup>();
            if (blackCanvasGroup == null)
            {
                blackCanvasGroup = blackObject.AddComponent<CanvasGroup>();
            }
            blackCanvasGroup.alpha = 0f;
        }
        
        // Panelの移動完了イベントを購読
        if (panel != null)
        {
            panel.OnRightMoveComplete += OnPanelMoveComplete;
        }

        botan.onHover += (e) => {
            if (isPopupShown) return;
            image.color = Color.white;
            if (hoverSound != null && audioSource != null) {
                audioSource.PlayOneShot(hoverSound, hoverSoundVolume);
            }
             if(panel != null) {
                if (moveType == MoveType.Right) {
                    panel.MoveRight();
                }
                else if (moveType == MoveType.Left) {
                    panel.MoveLeft();
                }
                
                isHovering = true;
                hasCompletedMove = false;
            }
            else {
                Debug.LogWarning($"{gameObject.name}: Panel is null!");
            }
        };
        botan.onClick += (e) => {
            if (popupPrefab != null) {
                // Popupをインスタンス化
                if (currentPopup == null)
                {
                    currentPopup = Instantiate(popupPrefab, popParent);

                    popupSubscriptions.Clear();
                    currentPopup.Initialize(selectedStage, musics);
                    currentPopup.OnMusicSelected.Subscribe(x => musicSelected.OnNext(x)).AddTo(popupSubscriptions);
                    currentPopup.OnHidden.Subscribe(_ => OnPopupHidden()).AddTo(popupSubscriptions);
                }
                currentPopup.Show();
                isPopupShown = true;
                stageSelected.OnNext(selectedStage);
            }
        };
        botan.onHoverExit += (e) => {
            if (isPopupShown) return;
            image.color = Color.gray;
            
            isHovering = false;
            hasCompletedMove = false;
            
            // ホバーが離れたときにPanelをデフォルト位置に戻す（blackもフェードアウト）
            if(panel != null) {
                panel.MoveToDefault();
            }
            
            // blackをフェードアウト
            if (blackCanvasGroup != null)
            {
                LeanTween.cancel(blackObject);
                LeanTween.alphaCanvas(blackCanvasGroup, 0f, blackFadeDuration).setEase(LeanTweenType.easeInQuad);
            }
        };
    }
    
    public void HidePopup()
    {
        // 表示されていなかったら何もしない
        if (!isPopupShown) return;
        
        if (currentPopup != null)
        {
            currentPopup.Hide();
        }
    }

    void OnPopupHidden()
    {
        isPopupShown = false;
        currentPopup = null;
    }
    
    void OnPanelMoveComplete()
    {

        // ホバー中で、まだフェードインしていない場合のみ実行
        if (isHovering && !hasCompletedMove)
        {
            hasCompletedMove = true;

            // blackオブジェクトをフェードイン
            if (blackCanvasGroup != null)
            {
                LeanTween.cancel(blackObject);
                LeanTween.alphaCanvas(blackCanvasGroup, 1f, blackFadeDuration).setEase(LeanTweenType.easeOutQuad);
            }
            else
            {
                Debug.LogWarning($"{gameObject.name}: blackCanvasGroup is null!");
            }
        }
    }
    
    void OnDestroy()
    {
        popupSubscriptions.Dispose();

        // イベント購読解除
        if (panel != null)
        {
            panel.OnRightMoveComplete -= OnPanelMoveComplete;
        }
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
