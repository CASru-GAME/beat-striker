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
            gamePad = gameObject.AddComponent<GamePad>();
            mockPresenter = new MockGamePadPresenter();
            gamePad.Construct(mockPresenter, playerInput);
        }

        [TearDown]
        public void TearDown() {
            Object.DestroyImmediate(gameObject);
        }

        [UnityTest]
        public IEnumerator OnEnable_PresenterOnEnableが呼ばれる() {
            // Act
            gamePad.enabled = true;
            yield return null;

            // Assert
            Assert.IsTrue(mockPresenter.OnEnableCalled);
        }

        [UnityTest]
        public IEnumerator OnDisable_PresenterOnDisableが呼ばれる() {
            // Arrange
            gamePad.enabled = true;
            yield return null;

            // Act
            gamePad.enabled = false;
            yield return null;

            // Assert
            Assert.IsTrue(mockPresenter.OnDisableCalled);
        }


        private class MockGamePadPresenter : IGamePadPresenter {
            public bool OnEnableCalled { get; private set; }
            public bool OnDisableCalled { get; private set; }

            public void OnEnable() {
                OnEnableCalled = true;
            }

            public void OnDisable() {
                OnDisableCalled = true;
            }

            public void OnDirection(Vector2 v) { }

            public void OnButton(GamePadButton button, GamePadAction action) { }
        }
    }
}
