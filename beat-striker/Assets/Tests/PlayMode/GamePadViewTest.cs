using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using Core.GamePad.Views;
using Core.GamePad.Presenters;
using Core.GamePad.Types;
using Tests.Utils;

namespace Tests.PlayMode {

    public class GamePadViewTest {

        private GameObject gameObject;
        private GamePadView gamePad;
        private MockGamePadPresenter mockPresenter;
        private PlayerInput playerInput;

        [SetUp]
        public void SetUp() {
            gameObject = new GameObject("TestGamePad");
            playerInput = gameObject.AddComponent<PlayerInput>();
            mockPresenter = new MockGamePadPresenter();
            gamePad = gameObject.AddComponent<GamePadView>();
            gamePad.enabled = false;
            gamePad.Construct(mockPresenter, new FakeLifeMutater());
        }

        [TearDown]
        public void TearDown() {
            Object.DestroyImmediate(gameObject);
        }


        private class MockGamePadPresenter : IGamePadPresenter {
            public GamePadButton? lastButtonPressed;
            public GamePadAction? lastActionType;

            public void OnDirection(Vector2 v) { }

            public void OnButton(GamePadButton button, GamePadAction action) {
                lastButtonPressed = button;
                lastActionType = action;
            }
        }
    }
}
