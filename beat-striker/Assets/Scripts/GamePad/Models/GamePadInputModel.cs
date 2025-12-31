using System;
using Core.GamePad.Types;
using Core.Utils;
using UnityEngine;

namespace Core.GamePad.Models {
    public class GamePadInputModel : IGamePadInputModel {
        private readonly Subject<GamePadInput> onInputed = new();
        private readonly Subject<DirectionChange> onDirectionChanged = new();
        private readonly Subject<GamePadId> onJoined = new();
        private readonly Subject<GamePadId> onLeft = new();
        
        // --- Subscriptions ---
        public IDisposable SubscribeInputed(Action<GamePadInput> listener) => onInputed.Subscribe(listener);
        public IDisposable SubscribeDirectionChanged(Action<DirectionChange> listener) => onDirectionChanged.Subscribe(listener);
        public IDisposable SubscribeJoined(Action<GamePadId> listener) => onJoined.Subscribe(listener);
        public IDisposable SubscribeLeft(Action<GamePadId> listener) => onLeft.Subscribe(listener);
        
        // --- Fire Events ---
        public void FireInputed(GamePadId gamePadId, GamePadButton button, GamePadAction action) 
            => onInputed.Fire(new GamePadInput(gamePadId, button, action));
        public void FireDirectionChanged(GamePadId gamePadId, Vector2 direction) 
            => onDirectionChanged.Fire(new DirectionChange(gamePadId, direction));
        public void FireJoined(GamePadId gamePadId) => onJoined.Fire(gamePadId);
        public void FireLeft(GamePadId gamePadId) => onLeft.Fire(gamePadId);
    }
}
