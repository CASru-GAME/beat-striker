using System;
using Core.App.Types;
using Core.Battle;
using TMPro;
using UnityEngine;

public class PlayerUI : MonoBehaviour {
    [SerializeField] int playerId;
    [SerializeField] BattleInstaller BattleInstaller;
    [SerializeField] Transform playerPosition;
    [SerializeField] HpBarUI hpBarUI;
    [SerializeField] SpecialBarUI specialBarUI;
    [SerializeField] ComboUI comboUI;
    [SerializeField] RingUI ringUI;
    
    IStrikerModelGetter strikerModel;
    IRythmTrackModelGetter rythmTrackModel;

    void Start() {
        strikerModel = BattleInstaller.strikerModels[playerId];
        rythmTrackModel = BattleInstaller.rythmTrackModel;

        hpBarUI.Construct(strikerModel);
        specialBarUI.Construct(strikerModel);
        comboUI.Construct(strikerModel);
        ringUI.Construct(rythmTrackModel, playerId, playerPosition);
    }

}
