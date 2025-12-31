
using System;
using UnityEngine;
using Core.App.Interfaces;
using Core.App.Types;
using Core.GamePad.Types;
using Core.Utils;

namespace Core.Battle {
    public class StrikerModel : IStrikerModel {
        public PlayerId PlayerId { get; private set; }
        public HitPoint MaxHitPoint { get; private set; }
        public SpecialPoint MaxSpecialPoint { get; private set; }


        // Observable properties
        private HitPoint hitPoint;
        private SpecialPoint specialPoint = new(0);
        private int missCount = 0;
        private int goodCount = 0;
        private int excellentCount = 0;
        private int score = 0;
        private int comboCount = 0;

        public Vector2 InputDirection { get; private set; }
        public bool IsInputEnabled { get; set; } = false;

        public void SetInputEnabled(bool isEnabled) {
            IsInputEnabled = isEnabled;
        }

        public HitPoint HitPoint => hitPoint;
        public SpecialPoint SpecialPoint => specialPoint;
        public int MissCount => missCount;
        public int GoodCount => goodCount;
        public int ExcellentCount => excellentCount;
        public int Score => score;
        public int ComboCount => comboCount;

        // Events for observing state changes
        private readonly Subject<HitPoint> onHitPointChanged = new();
        private readonly Subject<SpecialPoint> onSpecialPointChanged = new();
        private readonly Subject<BeatResult> onBeatResult = new();
        private readonly Subject onDied = new();
        private readonly Subject onReset = new();

        // Action events
        private readonly Subject onAttack = new();
        private readonly Subject onDash = new();
        private readonly Subject onCharge = new();
        private readonly Subject onGuard = new();
        private readonly Subject onMiss = new();
        private readonly Subject onSpecial = new();

        private ScoreRule rule;
        private IRythmTrackModel rythmTrackModel;

        public StrikerModel(PlayerId playerId, HitPoint hitPoint, SpecialPoint maxSpecialPoint, ScoreRule rule, IRythmTrackModel rythmTrackModel) {
            this.PlayerId = playerId;
            this.MaxHitPoint = hitPoint;
            this.hitPoint = hitPoint;
            this.MaxSpecialPoint = maxSpecialPoint;
            this.rule = rule;
            this.rythmTrackModel = rythmTrackModel;
        }

        // Subscription methods
        public IDisposable SubscribeHitPoint(Action<HitPoint> listener) => onHitPointChanged.Subscribe(listener);
        public IDisposable SubscribeSpecialPoint(Action<SpecialPoint> listener) => onSpecialPointChanged.Subscribe(listener);
        public IDisposable SubscribeBeatResult(Action<BeatResult> listener) => onBeatResult.Subscribe(listener);
        public IDisposable SubscribeDied(Action listener) => onDied.Subscribe(listener);
        public IDisposable SubscribeReset(Action listener) => onReset.Subscribe(listener);

        public IDisposable SubscribeAttack(Action listener) => onAttack.Subscribe(listener);
        public IDisposable SubscribeDash(Action listener) => onDash.Subscribe(listener);
        public IDisposable SubscribeCharge(Action listener) => onCharge.Subscribe(listener);
        public IDisposable SubscribeGuard(Action listener) => onGuard.Subscribe(listener);
        public IDisposable SubscribeMiss(Action listener) => onMiss.Subscribe(listener);
        public IDisposable SubscribeSpecial(Action listener) => onSpecial.Subscribe(listener);

        public void HandleInput(GamePadInput input) {
            if (IsInputEnabled == false || IsDead()) {
                Debug.Log($"[StrikerModel] Input ignored. Enabled: {IsInputEnabled}, Dead: {IsDead()}");
                return;
            }

            if (input.action == GamePadAction.Down) {
                Debug.Log($"[StrikerModel] Button Down: {input.button}");
                if (input.button == GamePadButton.South) {
                    if (Beat()) {
                        Debug.Log("[StrikerModel] Dash Fired");
                        onDash.Fire();
                    }
                    else {
                        Debug.Log("[StrikerModel] Dash Missed");
                    }
                }
                else if (input.button == GamePadButton.East) { if (Beat()) onAttack.Fire(); }
                else if (input.button == GamePadButton.West) { if (Beat()) onCharge.Fire(); }
                else if (input.button == GamePadButton.North) {
                    if (Beat()) {
                        PerformSpecial();
                    }
                }
                else if (input.button == GamePadButton.LeftTrigger) { if (Beat()) onGuard.Fire(); }
            }

            if (input.action == GamePadAction.Up && input.button == GamePadButton.Direction) {
                // Direction cancel logic was in hub, replicating here
                InputDirection = Vector2.zero;
            }
        }

        public void HandleDirection(Vector2 direction) {
            if (IsInputEnabled == false || IsDead()) return;
            InputDirection = direction;
        }

        private bool Beat() {
            var res = rythmTrackModel.Beat(PlayerId);
            AddBeatResult(res);
            Debug.Log($"[StrikerModel] Beat Result: {res.status}");
            if (res.status != BeatStatus.Miss) {
                GainSpecial();
                return true;
            }
            else {
                onMiss.Fire();
                return false;
            }
        }

        private void PerformSpecial() {
            if (specialPoint.value < MaxSpecialPoint.value) {
                onMiss.Fire();
                return;
            }
            GainSpecial(new SpecialPoint(-MaxSpecialPoint.value));
            onSpecial.Fire();
        }

        public void TakeDamage(HitPoint damage) {
            var nextHp = hitPoint.value - damage.value;
            HitPoint newHp = new(nextHp < 0 ? 0 : nextHp > MaxHitPoint.value ? MaxHitPoint.value : nextHp);
            if (hitPoint.value != newHp.value) {
                hitPoint = newHp;
                onHitPointChanged.Fire(hitPoint);
                if (IsDead()) {
                    onDied.Fire();
                }
            }
        }

        public void Heal(HitPoint heal) {
            var nextHp = hitPoint.value + heal.value;
            HitPoint newHp = new(nextHp < 0 ? 0 : nextHp > MaxHitPoint.value ? MaxHitPoint.value : nextHp);
            if (hitPoint.value != newHp.value) {
                hitPoint = newHp;
                onHitPointChanged.Fire(hitPoint);
            }
        }

        public void GainSpecial(SpecialPoint gain) {
            var nextSp = specialPoint.value + gain.value;
            SpecialPoint newSp = new(nextSp < 0 ? 0 : nextSp > MaxSpecialPoint.value ? MaxSpecialPoint.value : nextSp);
            if (specialPoint.value != newSp.value) {
                specialPoint = newSp;
                onSpecialPointChanged.Fire(specialPoint);
            }
        }

        public void GainSpecial() {
            GainSpecial(new SpecialPoint(rule.GetSpecialGain()));
        }

        public void AddBeatResult(BeatResult result) {
            if (result.status == BeatStatus.Miss) {
                missCount++;
                comboCount = 0;
                score += rule.GetScoreForJudge(BeatStatus.Miss);
            }
            else if (result.status == BeatStatus.Good) {
                goodCount++;
                score += rule.GetScoreForJudge(BeatStatus.Good);
                comboCount++;
            }
            else if (result.status == BeatStatus.Excellent) {
                excellentCount++;
                score += rule.GetScoreForJudge(BeatStatus.Excellent);
                comboCount++;
            }
            onBeatResult.Fire(result);
        }

        public bool IsDead() {
            return hitPoint.value <= 0;
        }

        public void Reset() {
            hitPoint = MaxHitPoint;
            specialPoint = new(0);
            missCount = 0;
            goodCount = 0;
            excellentCount = 0;
            score = 0;
            comboCount = 0;
            InputDirection = Vector2.zero;
            onReset.Fire();
        }
    }
}