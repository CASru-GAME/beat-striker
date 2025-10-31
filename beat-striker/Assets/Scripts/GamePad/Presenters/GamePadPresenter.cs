
using UnityEngine;
using Core.Utils;
using Core.GamePad.Models;
using Core.GamePad.Types;
using UnityEngine.InputSystem;

namespace Core.GamePad.Presenters {
    public sealed class GamePadPresenter : IGamePadPresenter {
        private readonly IBus bus;
        private readonly IGamePadModel model;

        public GamePadPresenter(IBus bus, IGamePadModel model, ILife life) {
            this.bus = bus;
            this.model = model;
            life.Link(OnEnable, OnDisable);
        }

         void OnEnable() {
            Debug.Log($"GamePadPresenter OnEnable: {model.Id}");
            bus.Publish(new GamePadMessages.Joined(model.Id));
        }
         void OnDisable() {
            Debug.Log($"GamePadPresenter OnDisable: {model.Id}");
            bus.Publish(new GamePadMessages.Left(model.Id));
        }


        public void OnDirection(Vector2 v) {
            var result = model.ApplyDirection(v);
            bus.Publish(new GamePadMessages.DirectionChanged(model.Id, model.GetDirection()));
            if (result.downStateChanged) {
                bus.Publish(new GamePadMessages.Inputed(
                    model.Id, GamePadButton.Direction,
                    result.downState ? GamePadAction.Down : GamePadAction.Up));
            }
        }

        public void OnButton(GamePadButton button, GamePadAction action) {
            bus.Publish(new GamePadMessages.Inputed(model.Id, button, action));
        }
    }
}
