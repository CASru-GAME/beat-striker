using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Core.Battle;
using Core.App.Types;
using Core.GamePad.Types;
using Core.Utils;


namespace Tests.EditMode {
    sealed class FakeRythmTrackModel : IRythmTrackModel {
        public readonly Queue<BeatResult> results = new();
        public readonly List<PlayerId> beatCalledWith = new();
        public float addedTimeSum = 0f;

        public BeatResult Beat(PlayerId playerId) {
            beatCalledWith.Add(playerId);
            if (results.Count > 0) {
                return results.Dequeue();
            }
            // 何も入ってなければデフォルトMiss
            return new BeatResult(BeatStatus.Miss);
        }

        public void AddTime(float time) {
            addedTimeSum += time;
        }

        public float GetBeatTime(PlayerId playerId, int index) {
            throw new System.NotImplementedException();
        }

        public float GetTime() {
            throw new System.NotImplementedException();
        }

        public float GetNextBeatTime(PlayerId playerId, int offset) {
            throw new System.NotImplementedException();
        }

        public void Reset() {
        }
    }

    sealed class FakeStrikerView : IStrikerView {
        public Vector2? lastDirection = null;
        public bool cancelDirectionCalled = false;

        public bool dashCalled = false;
        public bool attackCalled = false;
        public bool chargeCalled = false;
        public bool chargeEndCalled = false;
        public bool specialCalled = false;
        public bool guardCalled = false;

        public bool onMissCalled = false;
        public bool onHitCalled = false;
        public bool onDeadCalled = false;
        public bool onIntroCalled = false;
        public bool onVictoryCalled = false;

        public HitPoint lastCalcHitInput;
        public float lastCalcHitReturned;

        public Vector2 GetForwardDirection() {
            return new Vector2(1f, 0f);
        }

        public void ChangeDirection(Vector2 direction) {
            lastDirection = direction;
        }

        public void CancelDirection() {
            cancelDirectionCalled = true;
        }

        public void Dash() { dashCalled = true; }
        public void Attack() { attackCalled = true; }
        public void Charge() { chargeCalled = true; }
        public void ChargeEnd() { chargeEndCalled = true; }
        public void Special() { specialCalled = true; }
        public void Guard() { guardCalled = true; }

        public void OnMiss() { onMissCalled = true; }
        public void OnHit() { onHitCalled = true; }
        public void OnDead() { onDeadCalled = true; }
        public void OnIntro() { onIntroCalled = true; }
        public void OnVictory() { onVictoryCalled = true; }

        public HitPoint CalcHit(HitStatus status) {
            lastCalcHitInput = status.damage;
            lastCalcHitReturned = status.damage.value;
            return status.damage;
        }

        public void ResetPosition() {
        }

        public void ResetFlags() {
            lastDirection = null;
            cancelDirectionCalled = false;
            dashCalled = false;
            attackCalled = false;
            chargeCalled = false;
            chargeEndCalled = false;
            specialCalled = false;
            guardCalled = false;
            onMissCalled = false;
            onHitCalled = false;
            onDeadCalled = false;
            onIntroCalled = false;
            onVictoryCalled = false;
            lastCalcHitReturned = 0f;
        }

        public void SavePosition() {
            throw new System.NotImplementedException();
        }
    }

    sealed class FakeBattleResetter : IBattleResetter {
        public int resetCallCount = 0;

        public void ResetBattle() {
            resetCallCount++;
        }
    }

    sealed class FakeBattleModel : IBattleModel {
        public int currentRound = 0;
        public readonly Dictionary<int, PlayerId> roundWinners = new();
        public readonly Dictionary<PlayerId, int> winCounts = new();
        public bool finished = false;
        public PlayerId finalWinner = new PlayerId(-999);

        public PlayerId? lastLoser = null;
        public int nextRoundCallCount = 0;

        public PlayerId GetWinner(int round) {
            if (roundWinners.TryGetValue(round, out var p)) return p;
            return new PlayerId(-1);
        }

        public int GetWinCount(PlayerId playerId) {
            if (winCounts.TryGetValue(playerId, out var count)) return count;
            return 0;
        }

        public int GetCurrentRound() {
            return currentRound;
        }

        public void NextRound() {
            currentRound++;
            nextRoundCallCount++;
        }

        public void AddLoser(PlayerId playerId) {
            lastLoser = playerId;
        }

        public bool IsFinished() {
            return finished;
        }

        public PlayerId GetFinalWinner() {
            return finalWinner;
        }
    }



    public sealed class ScoreRuleTests {
        [Test]
        public void ScoreRule_判定ごとのスコアが正しく返る() {
            var rule = new ScoreRule(1000, 500, 200);

            Assert.That(rule.GetScoreForJudge(BeatStatus.Excellent), Is.EqualTo(1000));
            Assert.That(rule.GetScoreForJudge(BeatStatus.Good), Is.EqualTo(500));
            Assert.That(rule.GetScoreForJudge(BeatStatus.Miss), Is.EqualTo(0));
        }
    }


    public sealed class StrikerModelTests {
        [Test]
        public void StrikerModel_HPやSPのクランプ_BeatResult集計_スコア加算_コンボリセット() {
            var pid = new PlayerId(0);
            var rule = new ScoreRule(excellentScore: 1000, goodScore: 500, specialGain: 200);
            var model = new StrikerModel(pid, new HitPoint(100f), new SpecialPoint(100f), rule);

            // ダメージ適用・下限0
            model.TakeDamage(new HitPoint(30f));
            Assert.That(model.HitPoint.value, Is.EqualTo(70f).Within(1e-5));
            model.TakeDamage(new HitPoint(1000f));
            Assert.That(model.HitPoint.value, Is.EqualTo(0f).Within(1e-5));
            Assert.True(model.IsDead());

            // ヒール・上限MaxHitPointまで
            model.Heal(new HitPoint(10f));
            Assert.That(model.HitPoint.value, Is.EqualTo(10f).Within(1e-5));
            model.Heal(new HitPoint(999f));
            Assert.That(model.HitPoint.value, Is.EqualTo(model.MaxHitPoint.value).Within(1e-5));

            // SP増加・0未満にはならない
            model.GainSpecial(new SpecialPoint(5f));
            Assert.That(model.SpecialPoint.value, Is.EqualTo(5f).Within(1e-5));
            model.GainSpecial(new SpecialPoint(-999f));
            Assert.That(model.SpecialPoint.value, Is.EqualTo(0f).Within(1e-5));

            // BeatResultを流してスコア/コンボを検証
            model.AddBeatResult(new BeatResult(BeatStatus.Excellent));
            model.AddBeatResult(new BeatResult(BeatStatus.Good));
            Assert.That(model.ComboCount, Is.EqualTo(2)); // ミスしなければコンボが増加

            model.AddBeatResult(new BeatResult(BeatStatus.Miss));      // +0 & combo reset

            Assert.That(model.ExcellentCount, Is.EqualTo(1));
            Assert.That(model.GoodCount, Is.EqualTo(1));
            Assert.That(model.MissCount, Is.EqualTo(1));
            Assert.That(model.Score, Is.EqualTo(1500));
            Assert.That(model.ComboCount, Is.EqualTo(0)); // Missで0に戻る

            // ミスなしでコンボが継続するケース
            model.AddBeatResult(new BeatResult(BeatStatus.Excellent));
            model.AddBeatResult(new BeatResult(BeatStatus.Good));
            model.AddBeatResult(new BeatResult(BeatStatus.Excellent));
            Assert.That(model.ComboCount, Is.EqualTo(3)); // ミスしなければコンボが継続
        }
    }


    public sealed class RythmTrackModelTests {

        [Test]
        public void RythmTrackModel_Beat_Excellentとインデックス進行() {
            var model = new RythmTrackModel(
                new float[] { 0f, 1f, 2f },
                perfectWindow: 0.1f,
                goodWindow: 0.5f,
                timeOffset: 0f
            );
            var pid0 = new PlayerId(0);

            // 現在時刻0f → 最初の拍(0f)はExcellent扱い
            var r0 = model.Beat(pid0);
            Assert.That(r0.status, Is.EqualTo(BeatStatus.Excellent));

            // 時間を1.2fまで進める → 次の拍(1f)との差は0.2 => goodWindow(0.5)内
            model.AddTime(1.2f);
            var r1 = model.Beat(pid0);
            // 0.2はperfectWindow(0.1)より大きいのでGood判定になるはず
            Assert.That(r1.status, Is.EqualTo(BeatStatus.Good));

            // さらに時間を大きく進めて6.2まで進める
            model.AddTime(5f);
            var r2 = model.Beat(pid0);
            Assert.That(r2.status, Is.EqualTo(BeatStatus.Miss));
        }

        [Test]
        public void RythmTrackModel_AddTime_goodウィンドウ外を飛ばしてGood判定になる() {
            var model = new RythmTrackModel(
                new float[] { 0f, 1f, 2f },
                perfectWindow: 0.05f,
                goodWindow: 0.2f,
                timeOffset: 0f
            );
            var pid0 = new PlayerId(0);

            // 2.1秒まで進める → 次の拍(2f)との差は0.1
            // goodWindow(0.2)内なのでGood判定になるはず
            model.AddTime(2.1f);

            var r = model.Beat(pid0);
            Assert.That(r.status, Is.EqualTo(BeatStatus.Good));
        }
    }


    public sealed class BattleModelTests {

        [Test]
        public void BattleModel_勝者判定とラウンド遷移_Bo3ロジック_IsFinishedとGetFinalWinner() {
            // プレイヤー0と1で2本先取バトル
            var p0 = new PlayerId(0);
            var p1 = new PlayerId(1);

            var model = new BattleModel(2);

            // Round0: p1が死んだ → p0が勝者
            Assert.That(model.GetCurrentRound(), Is.EqualTo(0));
            model.AddLoser(p1);
            Assert.That(model.GetWinner(0).value, Is.EqualTo(p0.value));
            Assert.False(model.IsFinished());

            // Round1 に進む
            model.NextRound();
            Assert.That(model.GetCurrentRound(), Is.EqualTo(1));
            Assert.False(model.IsFinished());

            // Round1: またp1が死んだ → p0が2本目も勝者
            model.AddLoser(p1);
            Assert.That(model.GetWinner(1).value, Is.EqualTo(p0.value));

            // 2本先取なのでIsFinished==true、FinalWinner==p0になる
            Assert.True(model.IsFinished());
            Assert.That(model.GetFinalWinner().value, Is.EqualTo(p0.value));
        }

        [Test]
        public void BattleModel_AddLoserは同ラウンドで重複登録しない() {
            var p0 = new PlayerId(0);
            var p1 = new PlayerId(1);
            var model = new BattleModel(2);

            model.AddLoser(p1);
            model.AddLoser(p1); // 2回呼んでもOK

            // 勝者はp0のまま
            Assert.That(model.GetWinner(0).value, Is.EqualTo(p0.value));
        }
    }



    public sealed class StrikerPresenterTests {

        [Test]
        public void StrikerPresenter_入力に応じてViewコール_Beat結果でスコア加算やMiss演出_方向入力とキャンセル() {

            var bus = new Tests.Utils.FakeBus();
            var life = new FakeLife();

            var playerId = new PlayerId(0);
            var gid = new GamePadId(10);

            var registry = new FakePlayerRegistryMutable();
            registry.Map(gid, playerId);

            var view = new FakeStrikerView();
            var rule = new ScoreRule(1000, 500, 200);
            var model = new StrikerModel(playerId, new HitPoint(100f), new SpecialPoint(100f), rule);

            var rtm = new FakeRythmTrackModel();
            // Dash(South), Attack(East), Charge(West Down), ChargeEnd(West Up), Special(North), Guard(LeftTrigger)
            // Beat結果を順番に返す
            rtm.results.Enqueue(new BeatResult(BeatStatus.Excellent)); // Dash
            rtm.results.Enqueue(new BeatResult(BeatStatus.Good));      // Attack
            rtm.results.Enqueue(new BeatResult(BeatStatus.Excellent)); // Charge
            rtm.results.Enqueue(new BeatResult(BeatStatus.Excellent)); // ChargeEnd
            rtm.results.Enqueue(new BeatResult(BeatStatus.Miss));      // Special -> Miss演出
            rtm.results.Enqueue(new BeatResult(BeatStatus.Excellent)); // Guard

            var presenter = new StrikerPresenter(
                model,
                view,
                bus,
                life,
                registry,
                rtm
            );

            // 有効化でSubscribe開始
            life.Enable();

            // 方向入力 → ChangeDirection()
            bus.Publish(new GamePadMessages.DirectionChanged(gid, new Vector2(1f, 0f)));
            Assert.That(view.lastDirection, Is.EqualTo(new Vector2(1f, 0f)));

            // South(Down) => Dash() (Excellent)
            bus.Publish(new GamePadMessages.Inputed(gid, GamePadButton.South, GamePadAction.Down));
            Assert.True(view.dashCalled);

            // East(Down) => Attack() (Good)
            bus.Publish(new GamePadMessages.Inputed(gid, GamePadButton.East, GamePadAction.Down));
            Assert.True(view.attackCalled);

            // West(Down) => Charge() (Excellent)
            bus.Publish(new GamePadMessages.Inputed(gid, GamePadButton.West, GamePadAction.Down));
            Assert.True(view.chargeCalled);

            // Direction(Up) => CancelDirection() (Beat判定なし)
            bus.Publish(new GamePadMessages.Inputed(gid, GamePadButton.Direction, GamePadAction.Up));
            Assert.True(view.cancelDirectionCalled);

            // West(Up) => ChargeEnd() (Excellent)
            bus.Publish(new GamePadMessages.Inputed(gid, GamePadButton.West, GamePadAction.Up));
            Assert.True(view.chargeEndCalled);

            // North(Down) => Special()だがBeatがMissなのでSpecialせずOnMiss()だけ呼ばれる
            bus.Publish(new GamePadMessages.Inputed(gid, GamePadButton.North, GamePadAction.Down));
            Assert.True(view.onMissCalled);
            // specialCalledはMissなのでfalseのままであるはず
            Assert.False(view.specialCalled);

            // LeftTrigger(Down) => Guard() (Excellent)
            bus.Publish(new GamePadMessages.Inputed(gid, GamePadButton.LeftTrigger, GamePadAction.Down));
            Assert.True(view.guardCalled);

            // Model側のBeatResult集計/スコア/コンボを検証
            Assert.That(model.ExcellentCount, Is.EqualTo(4)); // Excellent x4
            Assert.That(model.GoodCount, Is.EqualTo(1));      // Good x1
            Assert.That(model.MissCount, Is.EqualTo(1));      // Miss x1
            Assert.That(model.Score, Is.EqualTo(
                1000 +
                500 +
                1000 +
                1000 +
                0 +
                1000
            ));
            Assert.That(model.ComboCount, Is.EqualTo(1)); // Missで0→最後のExcellentで1

            // life.Disable() 後はイベントを購読解除して反応しなくなる
            life.Disable();

            view.ResetFlags();
            bus.Publish(new GamePadMessages.DirectionChanged(gid, new Vector2(0f, 1f)));
            Assert.IsNull(view.lastDirection);
        }

        [Test]
        public void StrikerPresenter_Damage処理_死亡でNotifyPlayerDead_IntroとVictoryポーズ要求に反応() {
            var bus = new Tests.Utils.FakeBus();
            var life = new FakeLife();

            var playerId = new PlayerId(3);
            var gid = new GamePadId(33);
            var registry = new FakePlayerRegistryMutable();
            registry.Map(gid, playerId);

            var view = new FakeStrikerView();
            var model = new StrikerModel(playerId, new HitPoint(100f), new SpecialPoint(100f), new ScoreRule(1000, 500, 200));
            var rtm = new FakeRythmTrackModel();

            var presenter = new StrikerPresenter(
                model,
                view,
                bus,
                life,
                registry,
                rtm
            );

            life.Enable();

            // ダメージ10 → HP90, Deadではない
            presenter.TakeDamage(new HitStatus(new HitPoint(10f)));
            Assert.True(view.onHitCalled);
            Assert.False(view.onDeadCalled);
            Assert.That(model.HitPoint.value, Is.EqualTo(90f).Within(1e-5));

            // 大ダメージで即死 → OnDead() & NotifyPlayerDead がPublishされる
            view.ResetFlags();
            presenter.TakeDamage(new HitStatus(new HitPoint(1000f)));
            Assert.True(view.onHitCalled);
            Assert.True(view.onDeadCalled);
            Assert.True(model.IsDead());

            var deadMsg = bus.GetMessage<BattleMessages.NotifyPlayerDead>();
            Assert.That(deadMsg.playerId.value, Is.EqualTo(playerId.value));

            // Intro要求（対象プレイヤー一致時のみ）
            view.ResetFlags();
            bus.Publish(new BattleMessages.RequireIntroPose(playerId));
            Assert.True(view.onIntroCalled);
            view.ResetFlags();
            bus.Publish(new BattleMessages.RequireIntroPose(new PlayerId(999)));
            Assert.False(view.onIntroCalled);

            // Victory要求（対象プレイヤー一致時のみ）
            view.ResetFlags();
            bus.Publish(new BattleMessages.RequireVictoryPose(playerId));
            Assert.True(view.onVictoryCalled);
            view.ResetFlags();
            bus.Publish(new BattleMessages.RequireVictoryPose(new PlayerId(999)));
            Assert.False(view.onVictoryCalled);

            // Disable後は入力系イベントに反応しなくなる
            life.Disable();
            view.ResetFlags();
            bus.Publish(new GamePadMessages.DirectionChanged(gid, new Vector2(1f, 1f)));
            Assert.IsNull(view.lastDirection);
        }
    }



    public sealed class BattleFlowPresenterTests {

        [Test]
        public void BattleFlowPresenter_基本フロー_ラウンド開始とOnUpdateによるAddTime() {
            var bus = new Tests.Utils.FakeBus();
            var life = new FakeLife();

            var fakeBattle = new FakeBattleModel();
            fakeBattle.roundWinners[0] = new PlayerId(10);

            var fakeTrack = new FakeRythmTrackModel();
            var fakeResetter = new FakeBattleResetter();

            var presenter = new BattleFlowPresenter(
                bus,
                life,
                fakeBattle,
                fakeTrack,
                fakeResetter
            );

            life.Enable();

            // Intro終了イベント→RoundStartStateへ
            bus.Publish(new BattleMessages.NotifyIntroAnimationFinished());

            // RoundStartStateでNotifyRoundStartAnimationFinishedを待つはずなので発行→RoundStateへ
            bus.Publish(new BattleMessages.NotifyRoundStartAnimationFinished());

            // RoundState.Enter() で OnRoundStart(round=0) がPublishされているはず
            var roundStartMsg = bus.GetMessage<BattleMessages.OnBattleStarted>();
            Assert.That(roundStartMsg.battlemodel.GetCurrentRound(), Is.EqualTo(0));

            // RoundState中にOnUpdate()を呼ぶと、RythmTrackModel.AddTime()が叩かれる
            presenter.OnUpdate(0.5f);
            Assert.That(fakeTrack.addedTimeSum, Is.EqualTo(0.5f).Within(1e-5));
        }


        // RoundState中でプレイヤー死亡:
        // まだ試合が終わっていなければ次ラウンド開始(RoundStartState)に戻り、NextRound()が呼ばれる
        [Test]
        public void BattleFlowPresenter_プレイヤー死亡_未決着なら次ラウンドへ遷移しNextRoundが呼ばれる() {
            var bus = new Tests.Utils.FakeBus();
            var life = new FakeLife();

            var fakeBattle = new FakeBattleModel();
            fakeBattle.roundWinners[0] = new PlayerId(0);
            fakeBattle.roundWinners[1] = new PlayerId(1);

            var fakeTrack = new FakeRythmTrackModel();
            var fakeResetter = new FakeBattleResetter();

            var presenter = new BattleFlowPresenter(
                bus,
                life,
                fakeBattle,
                fakeTrack,
                fakeResetter
            );

            life.Enable();

            // RoundStateまで進める
            bus.Publish(new BattleMessages.NotifyIntroAnimationFinished());
            bus.Publish(new BattleMessages.NotifyRoundStartAnimationFinished());

            bus.ClearMessages();
            var deadPid = new PlayerId(99);
            bus.Publish(new BattleMessages.NotifyPlayerDead(deadPid));

            // RoundState.Exit()の中でOnRoundFinished(winnerOfRound0) がPublishされる
            var finishedMsg = bus.GetMessage<BattleMessages.OnOutroStarted>();
            Assert.That(finishedMsg.battlemodel.GetWinner(0).value, Is.EqualTo(fakeBattle.roundWinners[0].value));

            // battleModel.AddLoser()が呼ばれている
            Assert.That(fakeBattle.lastLoser!.Value.value, Is.EqualTo(deadPid.value));

            // IsFinished()==falseなのでRoundStartStateへ戻り、NextRound()が呼ばれている
            Assert.That(fakeBattle.currentRound, Is.EqualTo(1));
            Assert.That(fakeBattle.nextRoundCallCount, Is.EqualTo(1));

            // もう一度 NotifyRoundStartAnimationFinished() を発行すると round=1 のRoundStateに遷移して OnRoundStart(1) が Publishされる
            bus.ClearMessages();
            bus.Publish(new BattleMessages.NotifyRoundStartAnimationFinished());
            var roundStartMsg = bus.GetMessage<BattleMessages.OnBattleStarted>();
            Assert.That(roundStartMsg.battlemodel.GetCurrentRound(), Is.EqualTo(1));

            // RoundStateに戻ったのでOnUpdateでAddTime()がまた進む
            presenter.OnUpdate(1.0f);
            Assert.That(fakeTrack.addedTimeSum, Is.EqualTo(1.0f).Within(1e-5));
        }


        // RoundState中でプレイヤー死亡:
        // すでに決着済み(IsFinished()==true)ならOutroStateへ遷移し、以降のOnUpdate()ではAddTime()されない
        [Test]
        public void BattleFlowPresenter_プレイヤー死亡_決着済みならOutroStateに遷移しOnUpdateではAddTimeされない() {
            var bus = new Tests.Utils.FakeBus();
            var life = new FakeLife();

            var fakeBattle = new FakeBattleModel();
            fakeBattle.roundWinners[0] = new PlayerId(42);
            fakeBattle.finished = true; // すでに決着している想定

            var fakeTrack = new FakeRythmTrackModel();
            var fakeResetter = new FakeBattleResetter();

            var presenter = new BattleFlowPresenter(
                bus,
                life,
                fakeBattle,
                fakeTrack,
                fakeResetter
            );

            life.Enable();

            // RoundStateまで進める
            bus.Publish(new BattleMessages.NotifyIntroAnimationFinished());
            bus.Publish(new BattleMessages.NotifyRoundStartAnimationFinished());

            bus.ClearMessages();
            // この状態でDeadを投げるとOutroStateへ
            bus.Publish(new BattleMessages.NotifyPlayerDead(new PlayerId(7)));

            // RoundState.Exit()でOnRoundFinishedがPublishされているはず
            var finishedMsg = bus.GetMessage<BattleMessages.OnOutroStarted>();
            Assert.That(finishedMsg.battlemodel.GetWinner(0).value, Is.EqualTo(fakeBattle.roundWinners[0].value));

            // OutroStateはOnUpdate()でAddTimeしない(=FakeRythmTrackModel.addedTimeSumが増えない)
            fakeTrack.addedTimeSum = 0f;
            presenter.OnUpdate(2.0f);
            Assert.That(fakeTrack.addedTimeSum, Is.EqualTo(0f).Within(1e-5));
        }
    }
}
