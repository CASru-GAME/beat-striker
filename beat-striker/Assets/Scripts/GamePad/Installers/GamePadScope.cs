using Core.GamePad.Models;
using Core.GamePad.Presenters;
using Core.GamePad.Types;
using Core.GamePad.Views;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;

namespace Core.GamePad.Installers {
    [RequireComponent(typeof(GamePadView))]
    [RequireComponent(typeof(PlayerInput))]
    public sealed class GamePadScope : LifetimeScope {
        public static int nextId = 0;
        [SerializeField] float onThreshold = 0.5f;
        [SerializeField] float offThreshold = 0.4f;

        protected override void Configure(IContainerBuilder b) {
            var playerInput = GetComponent<PlayerInput>();
            b.RegisterInstance(playerInput).As<PlayerInput>();

            var view = GetComponent<GamePadView>();
            b.RegisterComponent(view).As<GamePadView>();

            b.RegisterInstance(new GamePadConfig {
                id = new GamePadId(nextId++),
                onThreshold = onThreshold,
                offThreshold = offThreshold,
            }).As<GamePadConfig>();

            b.Register<GamePadModel>(Lifetime.Scoped).As<IGamePadModel>();

            b.Register<GamePadPresenter>(Lifetime.Scoped).As<IGamePadPresenter>();
        }
    }

}