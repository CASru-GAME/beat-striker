
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Core.App.Models;
using Core.App.Presenters.Scene;
using Core.App.Presenters.Scene.States;
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.GamePad.Types;
using Core.Utils;
using UnityEngine;

using AppMsg = Core.App.Presenters.Scene.Types.AppMessages;
using PadMsg = Core.GamePad.Types.GamePadMessages;
using Tests.Utils;
using Core.App;

namespace Tests.EditMode {

    sealed class FakeCursorView : ICursorView {
        public Vector2? lastMoveDir;
        public bool moveEndCalled;
        public bool clickCalled;
        public bool destroyCalled;

        public void OnMove(Vector2 direction) {
            lastMoveDir = direction;
        }

        public void OnMoveEnd() {
            moveEndCalled = true;
        }

        public void OnClick() {
            clickCalled = true;
        }

        public void Destroy() {
            destroyCalled = true;
        }

        public void ResetFlags() {
            lastMoveDir = null;
            moveEndCalled = false;
            clickCalled = false;
            destroyCalled = false;
        }
    }

    sealed class FakePlayerRegistryMutable : IPlayerRegistry {
        readonly Dictionary<int, PlayerId> map = new();

        public void Map(GamePadId gid, PlayerId pid) {
            map[gid.value] = pid;
        }

        public void SetPlayers(params PlayerId[] players) {
            map.Clear();
            for (int i = 0; i < players.Length; i++) {
                map[i] = players[i];
            }
        }

        public void RemoveGamePad(GamePadId gid) {
            map.Remove(gid.value);
        }

        public PlayerId? ToPlayerId(GamePadId gamePadId) {
            if (map.TryGetValue(gamePadId.value, out var pid)) {
                return pid;
            }
            return null;
        }

        public IEnumerable<PlayerId> GetAllPlayerIds() {
            return new HashSet<PlayerId>(map.Values);
        }
    }

    sealed class FakeCursorFactory : ICursorFactory {
        public readonly List<PlayerId> created = new();
        public void CreateCursor(PlayerId id) {
            created.Add(id);
        }
    }

    sealed class FakeCursorRegistry : ICursorRegistry {
        public bool? lastActive = null;
        public int updateCount = 0;

        public void SetCursorsActive(bool active) {
            lastActive = active;
            updateCount++;
        }

        public void UpdateCursors() {
            updateCount++;
        }
    }

    sealed class FakeSceneView : ISceneView {
        public readonly List<AppScene> loadedScenes = new();

        public void LoadScene(AppScene scene, Action<AppScene> OnSceneLoadCompleted) {
            loadedScenes.Add(scene);
            OnSceneLoadCompleted?.Invoke(scene);
        }
    }

    // IBattleSettingModelフェイク: 単純なsetter/getter辞書
    sealed class FakeBattleSettingModel : IBattleSettingModel {
        public StageId Stage { get; set; } = new("");
        public TrackId Track { get; set; } = new("");

        readonly Dictionary<int, StrikerId> strikers = new();

        public StrikerId GetStriker(PlayerId playerId) {
            return strikers.TryGetValue(playerId.value, out var s)
                ? s
                : new StrikerId("");
        }

        public void SetStriker(PlayerId playerId, StrikerId striker) {
            strikers[playerId.value] = striker;
        }
    }

    sealed class FakeSceneStateController : ISceneStateController {
        public ISceneState lastState;
        public void ChangeState(ISceneState newState) {
            lastState = newState;
        }
    }

    sealed class FakeSceneStateFactory : ISceneStateFactory {
        readonly Dictionary<AppScene, ISceneState> map = new();

        public void Register(AppScene scene, ISceneState state) {
            map[scene] = state;
        }

        public ISceneState CreateSceneState(AppScene scene, SceneStateContext context) {
            if (map.TryGetValue(scene, out var st)) return st;
            return new DummyState();
        }
    }

    sealed class DummyState : ISceneState {
        public bool entered;
        public bool exited;
        public void Enter() { entered = true; }
        public void Exit() { exited = true; }
    }

    sealed class LogFakeState : ISceneState {
        readonly string name;
        readonly List<string> log;
        public bool entered;
        public bool exited;

        public LogFakeState(string name, List<string> log) {
            this.name = name;
            this.log = log;
        }

        public void Enter() {
            entered = true;
            log.Add(name + ".Enter");
        }

        public void Exit() {
            exited = true;
            log.Add(name + ".Exit");
        }
    }


    public sealed class CursorPresenterTests {

        [Test]
        public void CursorPresenter_自身のPlayerだけ反応し_OnClick_OnMoveEnd_Destroyまで動く() {
            var bus = new FakeBus();
            var life = new FakeLife();
            var pid = new PlayerId(1);
            var view = new FakeCursorView();
            var registry = new FakePlayerRegistryMutable();
            var gid = new GamePadId(10);
            registry.Map(gid, pid);

            var presenter = new CursorPresenter(
                view,
                pid,
                registry,
                bus,
                life
            );

            life.Enable();

            // 自分のGamePadからの方向入力
            bus.Publish(new PadMsg.DirectionChanged(gid, new Vector2(1f, 0f)));
            Assert.That(view.lastMoveDir, Is.EqualTo(new Vector2(1f, 0f)));

            // 方向ボタンがUp => OnMoveEnd()だけ呼ばれる クリックされない
            view.ResetFlags();
            bus.Publish(new PadMsg.Inputed(gid, GamePadButton.Direction, GamePadAction.Up));
            Assert.True(view.moveEndCalled);
            Assert.False(view.clickCalled);

            // Eastボタン => OnClick()
            view.ResetFlags();
            bus.Publish(new PadMsg.Inputed(gid, GamePadButton.East, GamePadAction.Down));
            Assert.True(view.clickCalled);

            // Cursor破棄要求(対象=自分)
            view.ResetFlags();
            bus.Publish(new AppMsg.RequireCursorDestroyed(pid));
            Assert.True(view.destroyCalled);

            // Cursor破棄要求(対象=全員)
            view.ResetFlags();
            bus.Publish(new AppMsg.RequireCursorDestroyed());
            Assert.True(view.destroyCalled);

            // 別GamePad (registryに未登録) からの入力は無視される
            view.ResetFlags();
            bus.Publish(new PadMsg.DirectionChanged(new GamePadId(99), new Vector2(0f, 1f)));
            Assert.IsNull(view.lastMoveDir);

            // 破棄要求(別プレイヤー)は無視
            view.ResetFlags();
            bus.Publish(new AppMsg.RequireCursorDestroyed(new PlayerId(999)));
            Assert.False(view.destroyCalled);
        }

        [Test]
        public void CursorPresenter_Disable後はイベントに反応しない() {
            var bus = new FakeBus();
            var life = new FakeLife();
            var playerId = new PlayerId(3);
            var view = new FakeCursorView();
            var registry = new FakePlayerRegistryMutable();
            var gid = new GamePadId(123);
            registry.Map(gid, playerId);

            var presenter = new CursorPresenter(view, playerId, registry, bus, life);

            life.Enable();
            life.Disable();

            view.ResetFlags();
            bus.Publish(new PadMsg.DirectionChanged(gid, new Vector2(1f, 1f)));
            bus.Publish(new AppMsg.RequireCursorDestroyed(playerId));

            Assert.IsNull(view.lastMoveDir);
            Assert.False(view.destroyCalled);
        }
    }


    public sealed class CursorRegistryTests {

        [Test]
        public void CursorRegistry_有効化でCursor生成_離脱で個別破棄_無効化で全破棄() {
            var bus = new FakeBus();
            var life = new FakeLife();
            var factory = new FakeCursorFactory();
            var playerReg = new FakePlayerRegistryMutable();

            // P0, P1が現在参加中
            var p0 = new PlayerId(0);
            var p1 = new PlayerId(1);
            playerReg.SetPlayers(p0, p1);

            var registry = new CursorRegistry(factory, playerReg, bus, life);
            life.Enable(); // PlayerJoined/Left購読開始

            // カーソルモードアクティブ化すると、存在するプレイヤー分CreateCursorされる デストロイ要求は出ない
            registry.SetCursorsActive(true);
            Assert.That(factory.created.Select(pid => pid.value).ToHashSet(),
                Is.EquivalentTo(new[] { 0, 1 }));
            Assert.That(bus.CountPublished<AppMsg.RequireCursorDestroyed>(), Is.EqualTo(0));

            // プレイヤー0がいなくなった → UpdateCursorsでP0だけ破棄要求
            bus.ClearMessages();
            playerReg.SetPlayers(p1);
            registry.UpdateCursors();

            Assert.That(bus.CountPublished<AppMsg.RequireCursorDestroyed>(), Is.EqualTo(1));
            var dmes = bus.GetMessage<AppMsg.RequireCursorDestroyed>();
            Assert.True(dmes.IsTarget(p0));

            // 非アクティブ化すると全カーソル破棄要求
            bus.ClearMessages();
            registry.SetCursorsActive(false);
            Assert.That(bus.CountPublished<AppMsg.RequireCursorDestroyed>(), Is.EqualTo(1));
            var d_all_mes = bus.GetMessage<AppMsg.RequireCursorDestroyed>();
            // 999も対象になる
            Assert.True(d_all_mes.IsTarget(new PlayerId(999)));
        }
    }


    public sealed class PlayerRegistryTests {

        [Test]
        public void PlayerRegistry_JoinでPlayerId割当_LeaveでPlayerLeft通知_無効化で購読解除() {
            var bus = new FakeBus();
            var life = new FakeLife();
            var registry = new PlayerRegistry(bus, life);

            life.Enable(); // GamePadMessages.Joined/Left購読開始

            // GamePadId=10 が参加 → PlayerId=0が割り当てられる
            bus.ClearMessages();
            var g0 = new GamePadId(10);
            bus.Publish(new PadMsg.Joined(g0));

            var pid0 = registry.ToPlayerId(g0);
            Assert.NotNull(pid0, "PlayerId should be assigned");
            Assert.That(pid0!.Value.value, Is.EqualTo(0));

            // 参加通知も発行される
            var joinedMsg0 = bus.GetMessage<AppMsg.PlayerJoined>();
            Assert.That(joinedMsg0.playerId.value, Is.EqualTo(0));

            // GamePadId=11 が参加 → PlayerId=1
            bus.ClearMessages();
            var g1 = new GamePadId(11);
            bus.Publish(new PadMsg.Joined(g1));

            var pid1 = registry.ToPlayerId(g1);
            Assert.NotNull(pid1, "PlayerId should be assigned");
            Assert.That(pid1!.Value.value, Is.EqualTo(1));

            var joinedMsg1 = bus.GetMessage<AppMsg.PlayerJoined>();
            Assert.That(joinedMsg1.playerId.value, Is.EqualTo(1));

            // GetAllPlayerIds()は0,1両方を返す
            var allIds = registry.GetAllPlayerIds().Select(p => p.value).ToHashSet();
            Assert.That(allIds, Is.EquivalentTo(new[] { 0, 1 }));

            // g0が抜ける → PlayerLeft(0)が発行され、マップから消える
            bus.ClearMessages();
            bus.Publish(new PadMsg.Left(g0));
            var left0 = bus.GetMessage<AppMsg.PlayerLeft>();
            Assert.That(left0.playerId.value, Is.EqualTo(0));
            Assert.IsNull(registry.ToPlayerId(g0));

            // g1も抜ける → PlayerLeft(1)が発行され、マップから消える
            bus.ClearMessages();
            bus.Publish(new PadMsg.Left(g1));
            var left1 = bus.GetMessage<AppMsg.PlayerLeft>();
            Assert.That(left1.playerId.value, Is.EqualTo(1));
            Assert.IsNull(registry.ToPlayerId(g1));

            // Disable後は購読解除されるので、Joinしても反応しない
            life.Disable();
            bus.ClearMessages();
            var g2 = new GamePadId(22);
            bus.Publish(new PadMsg.Joined(g2));
            Assert.IsNull(registry.ToPlayerId(g2));
            bus.CantGetMessage<AppMsg.PlayerJoined>();
        }
    }


    public sealed class SceneStatePresenterTests {

        [Test]
        public void ChangeState_前のExit後に次のEnterが呼ばれる() {
            var presenter = new SceneStatePresenter();
            var log = new List<string>();
            var s1 = new LogFakeState("S1", log);
            var s2 = new LogFakeState("S2", log);

            // 最初のChangeStateはcurrentState==nullなのでEnterだけ
            presenter.ChangeState(s1);
            Assert.True(s1.entered);
            Assert.False(s1.exited);
            Assert.That(log, Is.EqualTo(new[] { "S1.Enter" }));

            // 2回目はS1.Exit -> S2.Enter の順
            presenter.ChangeState(s2);
            Assert.True(s1.exited);
            Assert.True(s2.entered);
            Assert.That(log, Is.EqualTo(new[] { "S1.Enter", "S1.Exit", "S2.Enter" }));
        }

        [Test]
        public void CreateSceneState_シーンに応じて正しいState型が返る() {
            var presenter = new SceneStatePresenter();

            var ctx = new SceneStateContext(
                new FakeSceneView(),
                new FakeBus(),
                new FakeBattleSettingModel(),
                presenter, // controller
                presenter, // factory
                new FakeCursorFactory(),
                new FakeCursorRegistry()
            );

            Assert.That(presenter.CreateSceneState(AppScene.Title, ctx), Is.TypeOf<TitleState>());
            Assert.That(presenter.CreateSceneState(AppScene.StageSelect, ctx), Is.TypeOf<StageSelectState>());
            Assert.That(presenter.CreateSceneState(AppScene.CharacterSelect, ctx), Is.TypeOf<CharacterSelectState>());
            Assert.That(presenter.CreateSceneState(AppScene.Battle, ctx), Is.TypeOf<BattleState>());
            // 未定義はTitleState扱い
            Assert.That(presenter.CreateSceneState(AppScene.None, ctx), Is.TypeOf<TitleState>());
        }
    }



    public sealed class SceneStatesTests {

        SceneStateContext MakeContext(out FakeBus bus,
                                      out FakeSceneStateController controller,
                                      out FakeCursorRegistry cursorReg,
                                      out FakeBattleSettingModel setting,
                                      out FakeSceneStateFactory factory,
                                      out FakeSceneView view) {

            bus = new FakeBus();
            controller = new FakeSceneStateController();
            cursorReg = new FakeCursorRegistry();
            setting = new FakeBattleSettingModel();
            factory = new FakeSceneStateFactory();
            view = new FakeSceneView();

            return new SceneStateContext(
                view,
                bus,
                setting,
                controller,
                factory,
                new FakeCursorFactory(),
                cursorReg
            );
        }

        [Test]
        public void TitleState_Enterでカーソル有効_NextでTransitionStateに遷移_Exitで購読解除() {
            var ctx = MakeContext(out var bus, out var controller, out var cursorReg, out _, out _, out _);
            var state = new TitleState(ctx);

            // Enter時にカーソルが有効化される
            state.Enter();
            Assert.That(cursorReg.lastActive, Is.True);

            // Next要求でTransitionStateにChangeStateされる
            bus.Publish(new AppMsg.RequireTransition(TransitionRequire.Next));
            Assert.That(controller.lastState, Is.TypeOf<TransitionState>());

            // Exit後はイベントに反応しない
            controller.lastState = null;
            state.Exit();
            bus.Publish(new AppMsg.RequireTransition(TransitionRequire.Next));
            Assert.IsNull(controller.lastState);
        }

        [Test]
        public void StageSelectState_Enterでカーソル有効_StageTrack選択を設定に反映_Nextで遷移() {
            var ctx = MakeContext(out var bus, out var controller, out var cursorReg, out var setting, out _, out _);
            var state = new StageSelectState(ctx);

            // Enter時にカーソルが有効化される
            state.Enter();
            Assert.That(cursorReg.lastActive, Is.True);

            // ステージ/トラック選択イベントでsettingに反映
            var stg = new StageId("stageA");
            var trk = new TrackId("trackB");
            bus.Publish(new AppMsg.SelectStage(stg));
            bus.Publish(new AppMsg.SelectTrack(trk));

            Assert.That(setting.Stage.value, Is.EqualTo("stageA"));
            Assert.That(setting.Track.value, Is.EqualTo("trackB"));

            // Next要求でTransitionStateにChangeState
            bus.Publish(new AppMsg.RequireTransition(TransitionRequire.Next));
            Assert.That(controller.lastState, Is.TypeOf<TransitionState>());

            // Exit後は無反応
            controller.lastState = null;
            state.Exit();
            bus.Publish(new AppMsg.RequireTransition(TransitionRequire.Next));
            Assert.IsNull(controller.lastState);
        }

        [Test]
        public void CharacterSelectState_Striker選択を設定に登録_Nextで遷移() {
            var ctx = MakeContext(out var bus, out var controller, out var cursorReg, out var setting, out _, out _);
            var state = new CharacterSelectState(ctx);

            // Enter時にカーソルが有効化される
            state.Enter();
            Assert.That(cursorReg.lastActive, Is.True);

            // Striker選択イベント
            var pid = new PlayerId(7);
            var striker = new StrikerId("strikerX");
            bus.Publish(new AppMsg.SelectStriker(pid, striker));
            Assert.That(setting.GetStriker(pid).value, Is.EqualTo("strikerX"));

            // Next要求でTransitionStateにChangeState
            bus.Publish(new AppMsg.RequireTransition(TransitionRequire.Next));
            Assert.That(controller.lastState, Is.TypeOf<TransitionState>());

            // Exit後は無反応
            controller.lastState = null;
            state.Exit();
            bus.Publish(new AppMsg.RequireTransition(TransitionRequire.Next));
            Assert.IsNull(controller.lastState);
        }

        [Test]
        public void BattleState_Enterでカーソル無効_LoadScene要求でTitleへのTransitionStateに遷移() {
            var ctx = MakeContext(out var bus, out var controller, out var cursorReg, out _, out _, out _);
            var state = new BattleState(ctx);

            // Enter時にカーソルが無効化される
            state.Enter();
            Assert.That(cursorReg.lastActive, Is.False);

            // Next要求でTransitionStateへの遷移が依頼される
            bus.Publish(new AppMsg.RequireTransition(TransitionRequire.Next));
            Assert.That(controller.lastState, Is.TypeOf<TransitionState>());

            // Exit後は無反応
            controller.lastState = null;
            state.Exit();
            bus.Publish(new AppMsg.RequireTransition(TransitionRequire.Next));
            Assert.IsNull(controller.lastState);
        }

        [Test]
        public void TransitionState_EnterでTransitionStartedをPublish_Next要求でシーンロード完了後に次ステートへ() {
            var ctx = MakeContext(out var bus, out var controller, out _, out _, out var factory, out var view);

            var nextScene = AppScene.CharacterSelect;
            var nextDummy = new DummyState();
            factory.Register(nextScene, nextDummy);

            var state = new TransitionState(ctx, nextScene);

            // Enter()でTransitionStartedメッセージがPublishされる
            state.Enter();
            var mes = bus.GetMessage<AppMsg.TransitionStartedMessage>();
            Assert.That(mes.scene, Is.EqualTo(nextScene));

            // LoadScene要求でシーンロード開始、完了後に次ステートへChangeState
            bus.Publish(new AppMsg.RequireTransition(TransitionRequire.LoadScene));
            Assert.AreSame(nextDummy, controller.lastState);
            // viewが指定シーンをロードしようとしていることも確認
            Assert.That(view.loadedScenes.Last(), Is.EqualTo(nextScene));

            // Exit後は無反応
            controller.lastState = null;
            state.Exit();
            bus.Publish(new AppMsg.RequireTransition(TransitionRequire.LoadScene));
            Assert.IsNull(controller.lastState);
        }
    }
}
