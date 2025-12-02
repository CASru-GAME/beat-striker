using Core.App.Installers;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.Utils;
using UnityEngine;

public class SelectScene : MonoBehaviour
{
    public Transform selectorsParent;
    public Selecter selecterPrefab;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        this.GetBus().Subscribe<AppMessages.PlayerJoined>(OnPlayerJoined);
        this.GetBus().Subscribe<AppMessages.PlayerLeft>(OnPlayerLeft);
        var registry = GameObject.Find("App").GetComponent<AppFlowScope>().playerRegistry;
        foreach (var player in registry.GetAllPlayerIds()) {
            var selecter = Instantiate(selecterPrefab, selectorsParent);
            selecter.playerId = player;
        }
    }

    void OnPlayerJoined(AppMessages.PlayerJoined msg) {
        var selecter = Instantiate(selecterPrefab, selectorsParent);
        selecter.playerId = msg.playerId;
    }
    void OnPlayerLeft(AppMessages.PlayerLeft msg) {
        foreach (Transform child in selectorsParent) {
            var selecter = child.GetComponent<Selecter>();
            if (selecter != null && selecter.playerId.Equals(msg.playerId)) {
                Destroy(child.gameObject);
                break;
            }
        }
    }
    void OnDestroy() {
        this.GetBus().Unsubscribe<AppMessages.PlayerJoined>(OnPlayerJoined);
        this.GetBus().Unsubscribe<AppMessages.PlayerLeft>(OnPlayerLeft);
    }
}
