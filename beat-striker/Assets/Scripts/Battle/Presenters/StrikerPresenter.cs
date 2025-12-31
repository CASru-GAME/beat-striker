
using Core.App.Types;
using Core.GamePad.Types;
using Core.Utils;
using UnityEngine;

namespace Core.Battle {
    public class StrikerPresenter : IStrikerPresenter, IStrikerHit {
        readonly IBus bus;
        readonly IStrikerModel model;
        readonly IPlayerRegistry playerRegistry;
        readonly IStrikerView view;
        readonly IRythmTrackModel rythmTrackModel;
        bool isEnabled = false;
        bool isCharged = false;

        public StrikerPresenter(IStrikerModel model, IStrikerView view, IBus bus, ILife life, IPlayerRegistry playerRegistry, IRythmTrackModel rythmTrackModel) {
            this.model = model;
            this.view = view;
            this.bus = bus;
            this.rythmTrackModel = rythmTrackModel;
            this.playerRegistry = playerRegistry;
            life.Link(OnEnable, OnDisable);
        }

        public void OnEnable() {
            bus.Subscribe<GamePadMessages.Inputed>(OnGamePadInputed);
            bus.Subscribe<GamePadMessages.DirectionChanged>(OnGamePadDirectionChanged);
            bus.Subscribe<BattleMessages.RequireIntroPose>(OnIntro);
            bus.Subscribe<BattleMessages.RequireVictoryPose>(OnVictory);
            bus.Subscribe<BattleMessages.OnBattleStarted>(OnRoundStart);
            bus.Subscribe<BattleMessages.OnBattleFinished>(OnRoundEnd);
            bus.Subscribe<BattleMessages.OnBeat>(OnBeat);
        }

        public void OnDisable() {
            bus.Unsubscribe<GamePadMessages.Inputed>(OnGamePadInputed);
            bus.Unsubscribe<GamePadMessages.DirectionChanged>(OnGamePadDirectionChanged);
            bus.Unsubscribe<BattleMessages.RequireIntroPose>(OnIntro);
            bus.Unsubscribe<BattleMessages.RequireVictoryPose>(OnVictory);
            bus.Unsubscribe<BattleMessages.OnBattleStarted>(OnRoundStart);
            bus.Unsubscribe<BattleMessages.OnBattleFinished>(OnRoundEnd);
            bus.Unsubscribe<BattleMessages.OnBeat>(OnBeat);
        }

        private void OnGamePadInputed(GamePadMessages.Inputed msg) {
            var player = playerRegistry.ToPlayerId(msg.gamePadId);
            if (isEnabled == false || player == null || model.PlayerId != player || model.IsDead()) return;

            if (msg.action == GamePadAction.Down) {
                if (msg.button == GamePadButton.South) {
                    if(Beat()) view.Dash();
                }
                else if (msg.button == GamePadButton.East) {
                    if(Beat()) view.Attack();
                }
                else if (msg.button == GamePadButton.West) {
                    if(Beat()){
                        view.Charge();
                        isCharged = true;
                    }
                }
                else if (msg.button == GamePadButton.North) {
                    if (Beat()) {
                        if (model.SpecialPoint.value < model.MaxSpecialPoint.value) {
                            view.OnMiss();
                            return;
                        }
                        model.GainSpecial(new SpecialPoint(-model.MaxSpecialPoint.value));
                        view.Special();
                    }
                }
                else if (msg.button == GamePadButton.LeftTrigger) {
                    if(Beat()) view.Guard();
                }
            }
            else if (msg.action == GamePadAction.Up) {
                if (msg.button == GamePadButton.Direction) view.CancelDirection();
                else if (msg.button == GamePadButton.West && isCharged) {
                    if (Beat()) view.ChargeEnd();
                    isCharged = false;
                }
            }
        }

        private bool Beat() {
            var res = rythmTrackModel.Beat(model.PlayerId);
            model.AddBeatResult(res);
            bus.Publish(new BattleMessages.OnBeat(model.PlayerId, res));
            if (res.status != BeatStatus.Miss){
                model.GainSpecial();
                return true;
            }
            else {
                view.OnMiss();
                return false;
            }
        }

        private void OnGamePadDirectionChanged(GamePadMessages.DirectionChanged msg) {
            var player = playerRegistry.ToPlayerId(msg.gamePadId);
            if (isEnabled == false || player == null || model.PlayerId != player || model.IsDead()) return;

            view.ChangeDirection(msg.direction);
        }

        public void GiveHit(HitStatus status) {
            if (model.IsDead()) return;
            
            view.OnHit();
            var damage = view.CalcHit(status);
            model.TakeDamage(damage);
            if (model.IsDead()) {
                OnDead();
                bus.Publish(new BattleMessages.NotifyPlayerDead(model.PlayerId));
            }
        }

        private void OnDead() {
            view.OnDead();
        }

        private void OnIntro(BattleMessages.RequireIntroPose msgq) {
            if (model.PlayerId != msgq.playerId) return;
            view.OnIntro();
        }

        private void OnVictory(BattleMessages.RequireVictoryPose msgq) {
            if (model.PlayerId != msgq.playerId) return;
            view.OnVictory();
        }

        private void OnRoundStart(BattleMessages.OnBattleStarted msg) {
            isEnabled = true;
        }

        private void OnRoundEnd(BattleMessages.OnBattleFinished msg) {
            isEnabled = false;
        }

        private void OnBeat(BattleMessages.OnBeat msg) {
            if (model.PlayerId != msg.playerId || model.IsDead()) return;
            
            if (msg.result.status == BeatStatus.Miss) {
                view.OnMiss();
            }
        }
    }
}