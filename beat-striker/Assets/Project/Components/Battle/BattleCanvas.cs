using System;
using Core.Battle;
using Core.Utils;
using TMPro;
using UnityEngine;

public class BattleCanvas : MonoBehaviour {
    [SerializeField] TextMeshProUGUI BattleStartText;
    [SerializeField] TextMeshProUGUI BattleFinishText;
    private IBus bus;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake() {
        this.bus = this.GetBus();
        Debug.Log("BattleCanvas Awake");
        BattleFinishText.gameObject.SetActive(false);
        BattleStartText.gameObject.SetActive(false);
        bus.Subscribe<BattleMessages.OnOutroStarted>(OnOutroStarted);
        bus.Subscribe<BattleMessages.OnBattleFinished>(OnBattleFinished);
        bus.Subscribe<BattleMessages.OnRoundStarted>(OnRoundStarted);
    }

    void OnDestroy() {
        bus.Unsubscribe<BattleMessages.OnOutroStarted>(OnOutroStarted);
        bus.Unsubscribe<BattleMessages.OnBattleFinished>(OnBattleFinished);
        bus.Unsubscribe<BattleMessages.OnRoundStarted>(OnRoundStarted);
    }

    // Update is called once per frame
    void Update() {

    }

    void OnRoundStarted(BattleMessages.OnRoundStarted msg) {
        Debug.Log("Round Started Animation");
        BattleStartText.gameObject.SetActive(true);
        LeanTween.delayedCall(1f, () => {
            BattleStartText.text = $"Round {msg.battlemodel.GetCurrentRound()} Start!";
            BattleStartText.gameObject.SetActive(false);
            bus.Publish(new BattleMessages.NotifyRoundStartAnimationFinished());
        });
    }

    void OnBattleFinished(BattleMessages.OnBattleFinished msg) {
        Debug.Log("Battle Finished");
        BattleFinishText.gameObject.SetActive(true);
        LeanTween.delayedCall(1f, () => {
            BattleFinishText.gameObject.SetActive(false);
            bus.Publish(new BattleMessages.NotifyRoundFinishAnimationFinished());
        });
    }

    void OnOutroStarted(BattleMessages.OnOutroStarted msg) {
        Debug.Log("Battle All Finished");
        BattleFinishText.text = $"Battle All Finished!";
        BattleFinishText.gameObject.SetActive(true);
        LeanTween.delayedCall(1f, () => {
            BattleFinishText.gameObject.SetActive(false);
        });
    }

}