using System;
using Core.GamePad.Types;
using UnityEngine;

namespace Core.GamePad.Models {
    /// <summary>
    /// Definitions for the shared GamePad Input Model that aggregates events from all controllers.
    /// Replaces GamePadEvents.
    /// </summary>
    public interface IGamePadInputModel {
        // --- Subscriptions ---
        IDisposable SubscribeInputed(Action<GamePadInput> listener);
        IDisposable SubscribeDirectionChanged(Action<DirectionChange> listener);
        IDisposable SubscribeJoined(Action<GamePadId> listener);
        IDisposable SubscribeLeft(Action<GamePadId> listener);
        
        // --- Fire Events ---
        void FireInputed(GamePadId gamePadId, GamePadButton button, GamePadAction action);
        void FireDirectionChanged(GamePadId gamePadId, Vector2 direction);
        void FireJoined(GamePadId gamePadId);
        void FireLeft(GamePadId gamePadId);
    }
}
