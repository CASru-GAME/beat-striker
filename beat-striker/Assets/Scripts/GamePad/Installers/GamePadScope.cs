using Core.GamePad.Models;
using Core.GamePad.Types;
using Core.GamePad.Views;
using Core.Utils;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Core.GamePad.Installers {
    [RequireComponent(typeof(GamePadView))]
    [RequireComponent(typeof(PlayerInput))]
    [RequireComponent(typeof(Life))]
    public sealed class GamePadScope : MonoBehaviour {
        public static int nextId = 0;
        private static IGamePadInputModel sharedInputModel;

        [SerializeField] float onThreshold = 0.5f;
        [SerializeField] float offThreshold = 0.4f;

        public static IGamePadInputModel GetSharedInputModel() {
            if (sharedInputModel == null) {
                sharedInputModel = new GamePadInputModel();
            }
            return sharedInputModel;
        }

        void Awake() {
            Debug.Log("GamePadScope Configure");

            var life = GetComponent<Life>();

            var sharedModel = GetSharedInputModel();

            var view = GetComponent<GamePadView>();
            var config = new GamePadConfig {
                id = new GamePadId(nextId++),
                onThreshold = onThreshold,
                offThreshold = offThreshold,
            };

            var model = new GamePadModel(config);
            model.Initialize(sharedModel);

            // Presenter removed. View talks to Model directly.
            // Model linked to Life? Presenter used to do it.
            // GamePadModel needs lifecycle hooks?
            // GamePadModel.OnEnable calls sharedModel.FireJoined.
            // Who calls GamePadModel.OnEnable? 
            // We should link model lifecycle to Life here.

            life.Link(model.OnEnable, model.OnDisable);

            view.Construct(model, life);
        }
    }

}