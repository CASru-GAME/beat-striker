

using System.Collections.Generic;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.Utils;

public class CursorRegistry : ICursorRegistry {
    private readonly Dictionary<PlayerId, bool> cursors;
    private readonly ICursorFactory cursorFactory;
    private readonly IPlayerRegistry playerRegistry;
    private readonly IBus bus;
    private bool isActive = false;

    public CursorRegistry(ICursorFactory factory, IPlayerRegistry playerRegistry, IBus bus, ILife life) {
        cursors = new();
        cursorFactory = factory;
        this.playerRegistry = playerRegistry;
        this.bus = bus;
        life.Link(OnEnable, OnDisable);
    }

    private void OnEnable() {
        bus.Subscribe<AppMessages.PlayerJoined>(OnPlayerJoined);
        bus.Subscribe<AppMessages.PlayerLeft>(OnPlayerLeft);
    }

    private void OnDisable() {
        bus.Unsubscribe<AppMessages.PlayerJoined>(OnPlayerJoined);
        bus.Unsubscribe<AppMessages.PlayerLeft>(OnPlayerLeft);
    }

    private void OnPlayerJoined(AppMessages.PlayerJoined message) {
        UpdateCursors();
    }

    private void OnPlayerLeft(AppMessages.PlayerLeft message) {
        UpdateCursors();
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
                    bus.Publish(new AppMessages.RequireCursorDestroyed(playerId));
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
            bus.Publish(new AppMessages.RequireCursorDestroyed());
        }
    }
}

