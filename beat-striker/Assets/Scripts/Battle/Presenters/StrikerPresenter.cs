
using Core.App.Types;
using Core.GamePad.Types;
using Core.Utils;
using NUnit.Framework;

namespace Core.Battle {
    public class StrikerPresenter : IStrikerPresenter, IStrikerHit {
        readonly IBus bus;
        readonly IStrikerModel model;
        readonly IPlayerRegistry playerRegistry;
        readonly IStrikerView view;
        readonly IRythmTrackModel rythmTrackModel;

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
        }

        public void OnDisable() {
            bus.Unsubscribe<GamePadMessages.Inputed>(OnGamePadInputed);
            bus.Unsubscribe<GamePadMessages.DirectionChanged>(OnGamePadDirectionChanged);
            bus.Unsubscribe<BattleMessages.RequireIntroPose>(OnIntro);
            bus.Unsubscribe<BattleMessages.RequireVictoryPose>(OnVictory);
        }

        private void OnGamePadInputed(GamePadMessages.Inputed msg) {
            var player = playerRegistry.ToPlayerId(msg.gamePadId);
            if (player == null || model.PlayerId != player) return;

            if (msg.action == GamePadAction.Down) {
                if (msg.button == GamePadButton.South) {
                    view.Dash();
                    Beat();
                }
                else if (msg.button == GamePadButton.East) {
                    view.Attack();
                    Beat();
                }
                else if (msg.button == GamePadButton.West) {
                    view.Charge();
                    Beat();
                }
                else if (msg.button == GamePadButton.North) {
                    view.Special();
                    Beat();
                }
                else if (msg.button == GamePadButton.LeftTrigger) {
                    view.Guard();
                    Beat();
                }
            }
            else if (msg.action == GamePadAction.Up) {
                if (msg.button == GamePadButton.Direction) view.CancelDirection();
                else if (msg.button == GamePadButton.West) {
                    view.ChargeEnd();
                    Beat();
                }
            }
        }
        
        private void Beat() {
            var res = rythmTrackModel.Beat(model.PlayerId);
            model.AddBeatResult(res);
            if(res.status == BeatStatus.Miss) {
                view.OnMiss();
            }
        }

        private void OnGamePadDirectionChanged(GamePadMessages.DirectionChanged msg) {
            var player = playerRegistry.ToPlayerId(msg.gamePadId);
            if (player == null || model.PlayerId != player) return;

            view.ChangeDirection(msg.direction);
        }

        public void TakeDamage(HitStatus status) {
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
    }
}