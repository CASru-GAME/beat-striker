using System;
using Core.App;
using Core.App.Installers;
using Core.App.Interfaces;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.Utils;
using UnityEngine;

public class SelectScene : MonoBehaviour {
    public Transform selectorsParent;
    public Selecter selecterPrefab;
    private IAppModel appModel;
    private IDisposable playerJoinedSub;
    private IDisposable playerLeftSub;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        appModel = AppFlowScope.GetInstance().GetAppModel();
        playerJoinedSub = appModel.SubscribePlayerJoined(OnPlayerJoined);
        playerLeftSub = appModel.SubscribePlayerLeft(OnPlayerLeft);
        var registry = GameObject.Find("App").GetComponent<AppFlowScope>().playerRegistry;
        foreach (var player in registry.GetAllPlayerIds()) {
            var selecter = Instantiate(selecterPrefab, selectorsParent);
            selecter.playerId = player;
        }
    }

    void OnPlayerJoined(PlayerId playerId) {
        var selecter = Instantiate(selecterPrefab, selectorsParent);
        selecter.playerId = playerId;
    }
    void OnPlayerLeft(PlayerId playerId) {
        foreach (Transform child in selectorsParent) {
            var selecter = child.GetComponent<Selecter>();
            if (selecter != null && selecter.playerId.Equals(playerId)) {
                Destroy(child.gameObject);
                break;
            }
        }
    }
    void OnDestroy() {
        playerJoinedSub?.Dispose();
        playerLeftSub?.Dispose();
    }
}
