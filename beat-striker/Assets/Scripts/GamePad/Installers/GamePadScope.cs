using Core.GamePad.Models;
using Core.GamePad.Presenters;
using Core.GamePad.Types;
using Core.GamePad.Views;
using Core.Utils;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using VContainer.Unity;

namespace Core.GamePad.Installers {
    [RequireComponent(typeof(GamePadView))]
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(Life))]
    public sealed class GamePadScope : MonoBehaviour {
        public static int nextId = 0;
        [SerializeField] float onThreshold = 0.5f;
        [SerializeField] float offThreshold = 0.4f;

        void Awake() {
            Debug.Log("GamePadScope Configure");

            var life = GetComponent<Life>();

            var bus = this.GetBus();

            var view = GetComponent<GamePadView>();
            var config = new GamePadConfig {
                id = new GamePadId(nextId++),
                onThreshold = onThreshold,
                offThreshold = offThreshold,
            };

            var model = new GamePadModel(config);
            var presenter = new GamePadPresenter(bus, model,life);

            view.Construct(presenter, life);
        }
    }

}