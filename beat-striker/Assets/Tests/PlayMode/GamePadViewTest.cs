using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using Core.GamePad.Views;
using Core.GamePad.Presenters;
using Core.GamePad.Types;

namespace Tests.PlayMode {

    public class GamePadViewTest {

        private GameObject gameObject;
        private GamePad gamePad;
        private MockGamePadPresenter mockPresenter;
        private PlayerInput playerInput;

        [SetUp]
        public void SetUp() {
            gameObject = new GameObject("TestGamePad");
            playerInput = gameObject.AddComponent<PlayerInput>();
            mockPresenter = new MockGamePadPresenter();
            gamePad = gameObject.AddComponent<GamePad>();
            gamePad.enabled = false;
            gamePad.Construct(mockPresenter, playerInput);
        }

        [TearDown]
        public void TearDown() {
            Object.DestroyImmediate(gameObject);
        }

        [UnityTest]
        public IEnumerator OnEnableの時PresenterOnEnableが呼ばれる() {
            gamePad.enabled = true;
            yield return null;

            Assert.IsTrue(mockPresenter.onEnableCalled);
        }

        [UnityTest]
        public IEnumerator OnDisableの時PresenterOnDisableが呼ばれる() {
            gamePad.enabled = true;
            yield return null;

            gamePad.enabled = false;
            yield return null;

            Assert.IsTrue(mockPresenter.onDisableCalled);
        }


        private class MockGamePadPresenter : IGamePadPresenter {
            public bool onEnableCalled;
            public bool onDisableCalled;
            public GamePadButton? lastButtonPressed;
            public GamePadAction? lastActionType;

            public void OnEnable() {
                onEnableCalled = true;
            }

            public void OnDisable() {
                onDisableCalled = true;
            }

            public void OnDirection(Vector2 v) { }

            public void OnButton(GamePadButton button, GamePadAction action) {
                lastButtonPressed = button;
                lastActionType = action;
            }
        }
    }
}
