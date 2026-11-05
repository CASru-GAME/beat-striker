using System;
using R3;
using UnityEngine;

namespace Alice.WizardAi {
    [Serializable]
    public sealed record WizardAiTuning {
        [Min(.05f)] public float InitialBeatSeconds = .5f;
        [Min(.01f)] public float GroundClearance = .2f;
        [Min(1f)] public float ProjectileObservationRadius = 55f;
        [Min(0f)] public float PredictionMargin = .18f;
        [Min(0f)] public float WalkSpeed = 3f;
        [Min(0f)] public float MeleeReach = 2.1f;
        [Min(0f)] public float FireSpeed = 30f;
        [Min(0f)] public float FireRadius = .73f;
        [Min(0f)] public float FireDamage = 10f;
        [Min(0f)] public float IceDamage = 20f;
        [Min(0f)] public float IceHalfWidth = .95f;
        [Min(0f)] public float IceHeight = 1f;
        [Min(0f)] public float IceGrowthSeconds = .09f;
        [Min(0f)] public float RockDamage = 30f;
        [Min(0f)] public float RockRadius = 1.59f;
        [Min(0f)] public float ShieldRadius = 1.16f;
        [Min(0f)] public float ShieldForwardOffset = .393f;
        [Min(0f)] public float ShieldHeight = 1.3f;
        [Min(0f)] public float ShieldGenerationSeconds = .15f;
        [Min(0f)] public float WarriorLungeReach = 6.5f;
        [Min(0f)] public float WarriorWarpReach = 5f;
        [Min(0f)] public float DamageWeight = 1.6f;
        [Min(0f)] public float RiskWeight = 45f;
        [Min(0f)] public float PositionWeight = 1.8f;
        [Min(0f)] public float SpecialScore = 28f;
    }

    [Serializable]
    public sealed record WizardAiMatchup {
        [Min(0f)] public float PreferredDistance = 9f;
        [Min(0f)] public float OpponentReach = 5f;
        [Min(0f)] public float Pressure = 1f;
        [Min(0f)] public float ProjectileCaution = 1.2f;
    }

    [AddComponentMenu("AI/Wizard Tactical Brain")]
    public sealed class WizardAiBrain : AiBrain {
        [SerializeField] WizardAiTuning tuning = new();
        [Header("Matchups")]
        [SerializeField] WizardAiMatchup versusWarrior = new() { PreferredDistance = 10f, OpponentReach = 6.5f, ProjectileCaution = 1.4f };
        [SerializeField] WizardAiMatchup versusWizard = new() { PreferredDistance = 8f, OpponentReach = 2.5f, ProjectileCaution = 1.5f };
        [SerializeField] WizardAiMatchup versusHero = new() { PreferredDistance = 9f, OpponentReach = 5f };
        [SerializeField] WizardAiMatchup versusDemonKing = new() { PreferredDistance = 10f, OpponentReach = 5.5f, ProjectileCaution = 1.5f };

        readonly WizardAiSensor sensor = new();
        readonly WizardAiDecision decision = new();
        readonly WizardAiMemory memory = new();
        readonly AiDifficulty difficulty = new();
        IObservableStriker observedSelf;
        IObservableStriker observedOpponent;
        int lastBeat = -1;
        float lastBeatTime;
        float beatSeconds;
        AiAction lastAction = AiAction.None;
        IDisposable opponentStateSubscription;

        public string LastDecision { get; private set; } = string.Empty;
        public float LastDecisionScore { get; private set; }

        protected override AiAction OnGoodWindow(AiObservation observation) {
            if (observation.Self == null || observation.Opponent == null || observation.Self.Striker.CurrentValue != Striker.Wizard) return AiAction.None;
            if (!ReferenceEquals(observedSelf, observation.Self) || !ReferenceEquals(observedOpponent, observation.Opponent)
                || observation.Signal.BeatIndex < lastBeat || observation.Signal.BeatTime < lastBeatTime) {
                ResetObservation();
                observedSelf = observation.Self;
                observedOpponent = observation.Opponent;
                opponentStateSubscription = observedOpponent.CurrentStateCategory.DistinctUntilChanged()
                    .Subscribe(memory.ObserveOpponentCategory).AddTo(this);
            }
            if (lastBeat == observation.Signal.BeatIndex) return new AiAction(lastAction.Direction, null);
            beatSeconds = lastBeat < 0 ? tuning.InitialBeatSeconds
                : Mathf.Clamp((observation.Signal.BeatTime - lastBeatTime) / (observation.Signal.BeatIndex - lastBeat), .08f, 2f);
            lastBeat = observation.Signal.BeatIndex;
            lastBeatTime = observation.Signal.BeatTime;

            var frame = sensor.Observe(observation, beatSeconds, tuning);
            if (frame.Self.Wizard == null) {
                LastDecision = "Wizard combat observation is missing from the character prefab";
                return AiAction.None;
            }
            memory.Observe(frame);
            var matchup = observation.Opponent.Striker.CurrentValue switch {
                Striker.Wizard => versusWizard,
                Striker.Hero => versusHero,
                Striker.DemonKing => versusDemonKing,
                _ => versusWarrior
            };
            var result = decision.Choose(frame, tuning, matchup, memory, difficulty);
            memory.Record(result);
            LastDecision = result.Reason;
            LastDecisionScore = result.Score;
            return lastAction = result.Action;
        }

        protected override void OnAiEnabled() => ResetObservation();
        protected override void OnDifficultyChanged(int level) => difficulty.Configure(level);
        protected override void OnAiDisabled() => ResetObservation();
        public override void EndRoundEpisode() => ResetObservation();
        public override void BeginRoundEpisode(int roundNumber, string player1StrikerName, string player2StrikerName, string aiBrainPrefabName) => ResetObservation();
        public override void CompleteRoundEpisode(int roundNumber, bool player1Win) => ResetObservation();

        void ResetObservation() {
            opponentStateSubscription?.Dispose();
            opponentStateSubscription = null;
            sensor.Reset();
            memory.Reset();
            observedSelf = observedOpponent = null;
            lastBeat = -1;
            lastBeatTime = 0f;
            lastAction = AiAction.None;
            LastDecision = string.Empty;
            LastDecisionScore = 0f;
        }
    }

    internal sealed class WizardAiMemory {
        bool initialized;
        float selfHp, opponentHp;
        Vector3 opponentPosition;
        StrikerStateCategory previousCategory;
        int previousVersion;
        WizardAiMove lastMove;
        StrikerStateCategory lastOffensiveCategory = StrikerStateCategory.Unknown;
        bool attackSinceDecision, guardSinceDecision, alternationSinceDecision;

        public float ShieldHabit { get; private set; }
        public float AttackHabit { get; private set; }
        public float AbHabit { get; private set; }
        public float RecentDamage { get; private set; }
        public float WhiffPressure { get; private set; }
        public Vector3 OpponentDrift { get; private set; }
        public int StaleBeats { get; private set; }
        public int GuardStreak { get; private set; }

        public void ObserveOpponentCategory(StrikerStateCategory category) {
            attackSinceDecision |= category is StrikerStateCategory.Attack or StrikerStateCategory.Dash;
            guardSinceDecision |= category == StrikerStateCategory.Guard;
            if (category is not (StrikerStateCategory.Attack or StrikerStateCategory.Charge)) return;
            alternationSinceDecision |= lastOffensiveCategory is StrikerStateCategory.Attack or StrikerStateCategory.Charge
                && lastOffensiveCategory != category;
            lastOffensiveCategory = category;
        }

        public void Observe(WizardAiFrame frame) {
            var self = frame.Self.Striker.HitPoint.CurrentValue;
            var opponent = frame.Opponent.Striker.HitPoint.CurrentValue;
            if (initialized) {
                var dealt = Mathf.Max(0f, opponentHp - opponent);
                var received = Mathf.Max(0f, selfHp - self);
                RecentDamage = RecentDamage * .6f + received;
                StaleBeats = dealt > .01f || received > .01f ? 0 : Mathf.Min(30, StaleBeats + 1);
                // Delayed spells can hit several beats after casting; only decay the pressure on a hit.
                if (dealt > .01f) WhiffPressure = 0f;
                else if (lastMove is WizardAiMove.Fire or WizardAiMove.Ice or WizardAiMove.Rock or WizardAiMove.Melee)
                    WhiffPressure = Mathf.Min(4f, WhiffPressure + 1f);
                var drift = (frame.Opponent.Position - opponentPosition) / Mathf.Max(.08f, frame.BeatSeconds);
                if (drift.magnitude < 30f) OpponentDrift = Vector3.Lerp(OpponentDrift, drift, .5f);
                var changed = previousVersion != frame.Opponent.StateVersion || previousCategory != frame.Opponent.Category;
                var alternates = changed && (previousCategory == StrikerStateCategory.Charge && frame.Opponent.Category == StrikerStateCategory.Attack
                    || previousCategory == StrikerStateCategory.Attack && frame.Opponent.Category == StrikerStateCategory.Charge);
                var ab = alternates || alternationSinceDecision;
                AbHabit = frame.Opponent.Striker.Striker.CurrentValue == Striker.Warrior
                    ? Mathf.Lerp(AbHabit, ab ? 1f : 0f, ab ? .4f : .08f) : 0f;
            }
            ShieldHabit = Mathf.Lerp(ShieldHabit, guardSinceDecision || frame.Opponent.Category == StrikerStateCategory.Guard || frame.OpponentShields.Count >= 2 ? 1f : 0f, .3f);
            AttackHabit = Mathf.Lerp(AttackHabit, attackSinceDecision || frame.Opponent.Category is StrikerStateCategory.Attack or StrikerStateCategory.Dash ? 1f : 0f, .3f);
            attackSinceDecision = guardSinceDecision = alternationSinceDecision = false;
            selfHp = self;
            opponentHp = opponent;
            opponentPosition = frame.Opponent.Position;
            previousCategory = frame.Opponent.Category;
            previousVersion = frame.Opponent.StateVersion;
            initialized = true;
        }

        public void Record(WizardAiChoice result) {
            GuardStreak = result.Move == WizardAiMove.Shield ? GuardStreak + 1 : 0;
            lastMove = result.Move;
        }

        public void Reset() {
            initialized = false;
            ShieldHabit = AttackHabit = AbHabit = RecentDamage = WhiffPressure = 0f;
            StaleBeats = GuardStreak = 0;
            OpponentDrift = Vector3.zero;
            previousCategory = StrikerStateCategory.Unknown;
            previousVersion = 0;
            lastMove = WizardAiMove.Wait;
            lastOffensiveCategory = StrikerStateCategory.Unknown;
            attackSinceDecision = guardSinceDecision = alternationSinceDecision = false;
        }

    }
}
