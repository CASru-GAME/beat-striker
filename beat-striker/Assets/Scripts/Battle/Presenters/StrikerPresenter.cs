
using Core.App.Types;
using Core.GamePad.Types;
using Core.Utils;

namespace Core.Battle {
    public class StrikerPresenter : IStrikerPresenter, IStrikerHit {
        readonly IBus bus;
        readonly IStrikerModel model;
        readonly IPlayerRegistry playerRegistry;
        readonly IStrikerView view;
        readonly IRythmTrackModel rythmTrackModel;
        bool isEnabled = false;

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
            bus.Subscribe<BattleMessages.OnRoundStart>(OnRoundStart);
            bus.Subscribe<BattleMessages.OnRoundFinished>(OnRoundEnd);
        }

        public void OnDisable() {
            bus.Unsubscribe<GamePadMessages.Inputed>(OnGamePadInputed);
            bus.Unsubscribe<GamePadMessages.DirectionChanged>(OnGamePadDirectionChanged);
            bus.Unsubscribe<BattleMessages.RequireIntroPose>(OnIntro);
            bus.Unsubscribe<BattleMessages.RequireVictoryPose>(OnVictory);
            bus.Unsubscribe<BattleMessages.OnRoundStart>(OnRoundStart);
            bus.Unsubscribe<BattleMessages.OnRoundFinished>(OnRoundEnd);
        }

        private void OnGamePadInputed(GamePadMessages.Inputed msg) {
            var player = playerRegistry.ToPlayerId(msg.gamePadId);
            if (isEnabled == false || player == null || model.PlayerId != player) return;

            if (msg.action == GamePadAction.Down) {
                if (msg.button == GamePadButton.South) {
                    if(Beat()) view.Dash();
                }
                else if (msg.button == GamePadButton.East) {
                    if(Beat()) view.Attack();
                }
                else if (msg.button == GamePadButton.West) {
                    if(Beat()) view.Charge();
                }
                else if (msg.button == GamePadButton.North) {
                    if(Beat()) view.Special();
                }
                else if (msg.button == GamePadButton.LeftTrigger) {
                    if(Beat()) view.Guard();
                }
            }
            else if (msg.action == GamePadAction.Up) {
                if (msg.button == GamePadButton.Direction) view.CancelDirection();
                else if (msg.button == GamePadButton.West) {
                    if(Beat()) view.ChargeEnd();
                }
            }
        }

        private bool Beat() {
            var res = rythmTrackModel.Beat(model.PlayerId);
            model.AddBeatResult(res);
            if (res.status != BeatStatus.Miss) return true;
            view.OnMiss();
            return false;
        }

        private void OnGamePadDirectionChanged(GamePadMessages.DirectionChanged msg) {
            var player = playerRegistry.ToPlayerId(msg.gamePadId);
            if (isEnabled == false || player == null || model.PlayerId != player) return;

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

        private void OnRoundStart(BattleMessages.OnRoundStart msg) {
            isEnabled = true;
        }

        private void OnRoundEnd(BattleMessages.OnRoundFinished msg) {
            isEnabled = false;
        }
    }
}