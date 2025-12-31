using System;
using Core.App.Types;
using Core.Battle;
using TMPro;
using UnityEngine;

public class PlayerUI : MonoBehaviour {
    [SerializeField] public int playerId;
    [SerializeField] BattleInstaller BattleInstaller;
    [SerializeField] Transform playerPosition;
    [SerializeField] HpBarUI hpBarUI;
    [SerializeField] SpecialBarUI specialBarUI;
    [SerializeField] ComboUI comboUI;
    [SerializeField] RingUI ringUI;

    IStrikerModelGetter strikerModel;
    IRythmTrackModelGetter rythmTrackModel;

    public void Construct(IStrikerModelGetter strikerModel, IRythmTrackModelGetter rythmTrackModel) {
        this.strikerModel = strikerModel;
        this.rythmTrackModel = rythmTrackModel;

        hpBarUI.Construct(strikerModel);
        specialBarUI.Construct(strikerModel);
        comboUI.Construct(strikerModel);
        ringUI.Construct(rythmTrackModel, playerId, playerPosition);
    }

}
