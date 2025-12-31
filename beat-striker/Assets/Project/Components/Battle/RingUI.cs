using System;
using Core.App.Types;
using Core.Battle;
using UnityEngine;
using UnityEngine.UI;

public class RingUI : MonoBehaviour {
    IRythmTrackModelGetter rythmTrackModel;
    int playerId;
    Transform playerPosition;
    [SerializeField] Image[] centerRing;
    [SerializeField] Image[] rings;
    [SerializeField] float ringRadiusPerSecond = 1f;
    private float ringFirstAlpha;
    private float centerRingFirstAlpha;
    [SerializeField] float windowScale = 3f;

    private IBattleModel battleModel;
    private IDisposable battleStartedSubscription;
    private IDisposable beatSubscription;

    public void Construct(IRythmTrackModelGetter rythmTrackModel, int playerId, Transform playerPosition) {
        this.rythmTrackModel = rythmTrackModel;
        this.playerId = playerId;
        this.playerPosition = playerPosition;
    }

    void Awake() {
        centerRing[0].gameObject.SetActive(false);
        rings.ForEach(r => r.gameObject.SetActive(false));
    }

    void Start() {
        ringFirstAlpha = rings[0].color.a;
        centerRingFirstAlpha = centerRing[0].color.a;

        // BattleゲームオブジェクトからBattleInstallerを取得
        var battleObject = GameObject.Find("Battle");
        if (battleObject != null) {
            var battleInstaller = battleObject.GetComponent<BattleInstaller>();
            if (battleInstaller != null) {
                battleModel = battleInstaller.battleModel;
                battleStartedSubscription = battleModel.SubscribeBattleStarted(OnBattleStarted);
                beatSubscription = battleModel.SubscribeBeat(OnBeat);
            }
        }
    }

    void OnDestroy() {
        battleStartedSubscription?.Dispose();
        beatSubscription?.Dispose();
    }

    void OnBattleStarted(IBattlemodelGetter battlemodel) {
        centerRing[0].gameObject.SetActive(true);
        rings.ForEach(r => r.gameObject.SetActive(true));
    }

    void OnBeat(BeatInfo beat) {
        if (beat.PlayerId.value != playerId) return;

        Color color = centerRing[0].color;
        color.a = 1f;
        centerRing[0].color = color;

        LeanTween.alpha(centerRing[0].rectTransform, centerRingFirstAlpha, 0.3f);
    }

    void Update() {
        if (centerRing[0].gameObject.activeSelf == false) return;
        if (playerPosition == null || rythmTrackModel == null) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(playerPosition.position);
        this.transform.position = screenPos;

        for (int i = 0; i < rings.Length; i++) {
            var nextBeatTime = rythmTrackModel.GetNextBeatTime(new PlayerId(playerId), i);

            if (float.IsNaN(nextBeatTime)) {
                if (!rings[i].gameObject.activeSelf) rings[i].gameObject.SetActive(false);
                continue;
            }

            if (!rings[i].gameObject.activeSelf) rings[i].gameObject.SetActive(true);
            var timeSpan = nextBeatTime - rythmTrackModel.GetTime();
            if (timeSpan < 0) timeSpan = 0;

            float scale = ringRadiusPerSecond * timeSpan + 1;
            rings[i].transform.localScale = scale * Vector3.one;

            float alpha = ringFirstAlpha * Mathf.Clamp01(windowScale - scale);
            Color color = rings[i].color;
            color.a = alpha;
            rings[i].color = color;
        }
    }
}
