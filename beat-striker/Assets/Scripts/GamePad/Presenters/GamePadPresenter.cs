
using UnityEngine;
using Core.Utils;
using Core.GamePad.Models;
using Core.GamePad.Types;

namespace Core.GamePad.Presenters {
    public sealed class GamePadPresenter : IGamePadPresenter {
        private readonly IBus bus;
        private readonly IGamePadModel model;

        public GamePadPresenter(IBus bus, IGamePadModel model) {
            this.bus = bus; 
            this.model = model;
        }

        public void OnEnable() => bus.Publish(new GamePadJoinedMessage(model.Id));
        public void OnDisable() => bus.Publish(new GamePadLeftMessage(model.Id));

        public void OnDirection(Vector2 v) {
            var result = model.ApplyDirection(v);
            bus.Publish(new GamePadDirectionMessage(model.Id, model.GetDirection()));
            if (result.downStateChanged) {
                bus.Publish(new GamePadMessage(
                    model.Id, GamePadButton.Direction,
                    result.downState ? GamePadAction.Down : GamePadAction.Up));
            }
        }

        public void OnButton(GamePadButton button, GamePadAction action) {
            bus.Publish(new GamePadMessage(model.Id, button, action));
        }
    }
}
