using NUnit.Framework;
using UnityEngine;

using Core.GamePad.Models;
using Core.GamePad.Presenters;
using Core.GamePad.Types;
using Tests.Utils;

namespace Tests.EditMode {


    public sealed class GamePadModelTests {

        private GamePadModel model = null!;

        [SetUp]
        public void SetUp() {
            model = new GamePadModel(new GamePadConfig {
                id = new GamePadId(0),
                onThreshold = 0.5f,
                offThreshold = 0.4f,
            });
        }

        [Test]
        public void InitialState() {
            // 初期状態は方向ゼロ 押下ではない
            Assert.That(model.GetDirection(), Is.EqualTo(Vector2.zero));
            var res = model.ApplyDirection(Vector2.zero);
            Assert.False(res.downStateChanged);
            Assert.False(res.downState);
            Assert.That(model.GetDirection(), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void Hysteresis() {
            // オンしきい値未満の入力ベクトルでApplyDirectionを呼び出し、状態が変わらないことを確認
            var smallVec = model.ApplyDirection(new Vector2(0.49f, 0f));
            Assert.False(smallVec.downStateChanged);
            Assert.False(smallVec.downState);
            Assert.That(model.GetDirection(), Is.EqualTo(Vector2.zero));

            // オンしきい値を超える入力ベクトルでApplyDirectionを呼び出し、状態がオンに変わることを確認
            var bigVec = new Vector2(0.8f, 0f);
            var res = model.ApplyDirection(bigVec);
            Assert.True(res.downStateChanged);
            Assert.True(res.downState);

            // 方向ベクトルが正規化されていることを確認
            var bigVecNorm = bigVec.normalized;
            Assert.That(model.GetDirection().x, Is.EqualTo(bigVecNorm.x).Within(1e-5));
            Assert.That(model.GetDirection().y, Is.EqualTo(bigVecNorm.y).Within(1e-5));

            // オフしきい値以上の入力でオン状態を維持することを確認
            var stayOnResult = model.ApplyDirection(new Vector2(0.45f, 0f));
            Assert.False(stayOnResult.downStateChanged);
            Assert.True(stayOnResult.downState);

            // オフしきい値を下回る入力で状態がオフに変わることを確認
            var offThresholdResult = model.ApplyDirection(new Vector2(0.39f, 0f));
            Assert.True(offThresholdResult.downStateChanged);
            Assert.False(offThresholdResult.downState);
            Assert.That(model.GetDirection(), Is.EqualTo(Vector2.zero));
        }

        // 方向は押下時のみ正規化_非押下時はゼロ
        [Test]
        public void DirectionNormalization() {
            model.ApplyDirection(new Vector2(0.2f, 0.2f));
            Assert.That(model.GetDirection(), Is.EqualTo(Vector2.zero));

            var v = new Vector2(0.6f, 0.8f);
            model.ApplyDirection(v);
            Assert.That(model.GetDirection().magnitude, Is.EqualTo(1f).Within(1e-5));
        }
    }

    public sealed class GamePadPresenterTests {

        private FakeBus bus;
        private IGamePadModel model;
        private GamePadPresenter presenter;
        private GamePadId id;

        [SetUp]
        public void SetUp() {
            bus = new FakeBus();
            id = new GamePadId(10);
            model = new GamePadModel(new GamePadConfig {
                id = id,
                onThreshold = 0.5f,
                offThreshold = 0.4f,
            });
            var life = new FakeLife();
            presenter = new GamePadPresenter(bus, model, life);
            life.Enable();
        }


        [Test]
        public void EnableDisable() {
            // 有効化時に参加メッセージを送る
            var m0 = bus.GetMessage<GamePadMessages.Joined>();
            Assert.That(m0.gamePadId.value, Is.EqualTo(id.value));

            // 無効化時に離脱メッセージを送る
            bus.ClearMessages();
            var life2 = new FakeLife();
            _ = new GamePadPresenter(bus, model, life2);
            life2.Disable();
            var m1 = bus.GetMessage<GamePadMessages.Left>();
            Assert.That(m1.gamePadId.value, Is.EqualTo(id.value));
        }

        [Test]
        public void OnDirection() {
            // ゼロのメッセージを送る
            bus.ClearMessages();
            presenter.OnDirection(Vector2.zero);
            var m0 = bus.GetMessage<GamePadMessages.DirectionChanged>();
            Assert.That(m0.direction, Is.EqualTo(Vector2.zero));
            Assert.That(m0.gamePadId.value, Is.EqualTo(id.value));
            

            // 正規化された方向のメッセージを送る
            bus.ClearMessages();
            presenter.OnDirection(new Vector2(0.8f, 0f));

            var m1 = bus.GetMessage<GamePadMessages.DirectionChanged>();
            Assert.That(m1.direction.magnitude, Is.EqualTo(1f).Within(1e-5));
            Assert.That(m1.gamePadId.value, Is.EqualTo(id.value));

            var m2 = bus.GetMessage<GamePadMessages.Inputed>();
            Assert.That(m2.button, Is.EqualTo(GamePadButton.Direction));
            Assert.That(m2.action, Is.EqualTo(GamePadAction.Down));
            Assert.That(m2.gamePadId.value, Is.EqualTo(id.value));
            Assert.That(m2.gamePadId.value, Is.EqualTo(id.value));

            // オフしきい値を超える入力
            bus.ClearMessages();
            presenter.OnDirection(new Vector2(0.39f, 0f));

            var m4 = bus.GetMessage<GamePadMessages.DirectionChanged>();
            Assert.That(m4.direction, Is.EqualTo(Vector2.zero));
            Assert.That(m4.gamePadId.value, Is.EqualTo(id.value));

            var m5 = bus.GetMessage<GamePadMessages.Inputed>();
            Assert.That(m5.button, Is.EqualTo(GamePadButton.Direction));
            Assert.That(m5.action, Is.EqualTo(GamePadAction.Up));
            Assert.That(m5.gamePadId.value, Is.EqualTo(id.value));
        }

        [Test]
        public void OnButton() {
            // 押下
            bus.ClearMessages();
            presenter.OnButton(GamePadButton.North, GamePadAction.Down);
            var m0 = bus.GetMessage<GamePadMessages.Inputed>();
            Assert.That(m0.button, Is.EqualTo(GamePadButton.North));
            Assert.That(m0.action, Is.EqualTo(GamePadAction.Down));
            Assert.That(m0.gamePadId.value, Is.EqualTo(id.value));

            // 押下解除
            bus.ClearMessages();
            presenter.OnButton(GamePadButton.North, GamePadAction.Up);
            var m1 = bus.GetMessage<GamePadMessages.Inputed>();
            Assert.That(m1.button, Is.EqualTo(GamePadButton.North));
            Assert.That(m1.action, Is.EqualTo(GamePadAction.Up));
            Assert.That(m1.gamePadId.value, Is.EqualTo(id.value));
        }
    }
}
