using System;
using System.Collections.Generic;
using Core.App;
using Core.App.Interfaces;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.Utils;
using UnityEngine;

public class CursorRegistry : ICursorRegistry {
    private readonly Dictionary<PlayerId, bool> cursors;
    private readonly ICursorFactory cursorFactory;
    private readonly IPlayerRegistry playerRegistry;
    private readonly IAppModel appModel;
    private readonly CompositeDisposable subscriptions = new();
    private bool isActive = false;

    public CursorRegistry(ICursorFactory factory, IPlayerRegistry playerRegistry, IAppModel appModel, ILife life) {
        cursors = new();
        cursorFactory = factory;
        this.playerRegistry = playerRegistry;
        this.appModel = appModel;
        life.Link(OnEnable, OnDisable);
    }

    private void OnEnable() {
        subscriptions.Add(appModel.SubscribePlayerJoined(OnPlayerJoined));
        subscriptions.Add(appModel.SubscribePlayerLeft(OnPlayerLeft));
        subscriptions.Add(appModel.SubscribeSetCursorsActive(OnSetCursorsActive));
    }

    private void OnDisable() {
        subscriptions.Dispose();
    }

    private void OnPlayerJoined(PlayerId playerId) {
        Debug.Log("CursorRegistry OnPlayerJoined: " + playerId.value);
        UpdateCursors();
    }

    private void OnPlayerLeft(PlayerId playerId) {
        UpdateCursors();
    }

    private void OnSetCursorsActive(bool active) {
        Debug.Log($"CursorRegistry OnSetCursorsActive: {active}");
        SetCursorsActive(active);
    }


    public void SetCursorsActive(bool active) {
        isActive = active;
        UpdateCursors();
    }

    public void UpdateCursors() {
        if (isActive) {
            var currentPlayerIds = new HashSet<PlayerId>(playerRegistry.GetAllPlayerIds());
            var existingPlayerIds = new HashSet<PlayerId>(cursors.Keys);

            foreach (var playerId in existingPlayerIds) {
                if (!currentPlayerIds.Contains(playerId)) {
                    cursors.Remove(playerId);
                    appModel.FireRequireCursorDestroyed(new CursorDestroyRequest(playerId));
                }
            }

            foreach (var playerId in currentPlayerIds) {
                if (!cursors.TryGetValue(playerId, out var isCreated) || !isCreated) {
                    cursorFactory.CreateCursor(playerId);
                    cursors[playerId] = true;
                }
            }
        }
        else {
            cursors.Clear();
            appModel.FireRequireCursorDestroyed(CursorDestroyRequest.All());
        }
    }
}

