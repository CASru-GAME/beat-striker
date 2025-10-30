using System;
using Core.Battle;
using TMPro;
using UnityEngine;

public class PlayerUI : MonoBehaviour {
    [SerializeField] int playerId;
    [SerializeField] Transform HpBar;
    [SerializeField] Transform SpecialBar;
    [SerializeField] TextMeshProUGUI Combo;
    [SerializeField] BattleInstaller BattleInstaller;
    IStrikerModelGetter strikerModel;
    IRythmTrackModelGetter rythmTrackModel;

    void Awake() {
        strikerModel = BattleInstaller.strikerModels[playerId];
        rythmTrackModel = BattleInstaller.rythmTrackModel;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
    }

    // Update is called once per frame
    void Update() {
        HpBar.localScale = new Vector3(
            strikerModel.HitPoint.value / strikerModel.MaxHitPoint.value,
            HpBar.localScale.y,
            HpBar.localScale.z
        );

        SpecialBar.localScale = new Vector3(
            strikerModel.SpecialPoint.value / strikerModel.MaxSpecialPoint.value,
            SpecialBar.localScale.y,
            SpecialBar.localScale.z
        );

        Combo.text = strikerModel.ComboCount.ToString();

    }
}
