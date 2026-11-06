using System;
using R3;
using UnityEngine;

namespace Alice.DemonKingAi {
    [Serializable]
    public sealed record DemonKingAiTuning {
        [Min(.05f)] public float InitialBeatSeconds = .5f;
        [Min(.01f)] public float GroundClearance = .2f;
        [Min(1f)] public float ProjectileObservationRadius = 60f;
        [Min(0f)] public float PredictionMargin = .18f;
        [Min(0f)] public float WalkSpeed = 1f;
        [Min(0f)] public float MeleeReach = 1.8f;
        [Min(0f)] public float AirAttackSpeed = 5f;
        [Min(0f)] public float AirAttackDamage = 10f;
        [Min(0f)] public float AirAttackSeconds = .5f;
        [Min(0f)] public float DiveSpeed = 20f;
        [Min(0f)] public float DiveSeconds = .5f;
        [Range(.001f, 1f)] public float DiveEndRatio = .1f;
        [Min(1)] public int MaximumBankedVolleys = 3;
        [Min(0f)] public float WarriorLungeReach = 6.5f;
        [Min(0f)] public float WarriorWarpReach = 5f;
        [Min(0f)] public float DamageWeight = 1.8f;
        [Min(0f)] public float RiskWeight = 46f;
        [Min(0f)] public float PositionWeight = 2f;
        [Min(0f)] public float SpecialScore = 55f;
    }

    [Serializable]
    public sealed record DemonKingAiMatchup {
        [Min(0f)] public float PreferredDistance = 3f;
        [Min(0f)] public float OpponentReach = 5f;
        [Min(0f)] public float Pressure = 1f;
        [Min(0f)] public float ProjectileCaution = 1.2f;
    }

    [AddComponentMenu("AI/Demon King Tactical Brain")]
    public sealed class DemonKingAiBrain : AiBrain {
        [SerializeField] DemonKingAiTuning tuning = new();
        [Header("Matchups")]
        [SerializeField] DemonKingAiMatchup versusWarrior = new() { PreferredDistance = 3f, OpponentReach = 6.5f, ProjectileCaution = 1.4f };
        [SerializeField] DemonKingAiMatchup versusWizard = new() { PreferredDistance = 5f, OpponentReach = 2.5f, ProjectileCaution = 1.5f };
        [SerializeField] DemonKingAiMatchup versusHero = new() { PreferredDistance = 4f, OpponentReach = 5f, ProjectileCaution = 1.4f };
        [SerializeField] DemonKingAiMatchup versusDemonKing = new() { PreferredDistance = 3.5f, OpponentReach = 5.5f, ProjectileCaution = 1.5f };

        readonly DemonKingAiSensor sensor = new();
        readonly DemonKingAiDecision decision = new();
        readonly DemonKingAiMemory memory = new();
        readonly AiDifficulty difficulty = new();
        IObservableStriker observedSelf, observedOpponent;
        IDisposable opponentStateSubscription;
        int lastBeat = -1;
        float lastBeatTime;
        AiAction lastAction = AiAction.None;

        public string LastDecision { get; private set; } = string.Empty;
        public float LastDecisionScore { get; private set; }

        protected override AiAction OnGoodWindow(AiObservation observation) {
            if (observation.Self == null || observation.Opponent == null || observation.Self.Striker.CurrentValue != Striker.DemonKing) return AiAction.None;
            if (!ReferenceEquals(observedSelf, observation.Self) || !ReferenceEquals(observedOpponent, observation.Opponent)
                || observation.Signal.BeatIndex < lastBeat || observation.Signal.BeatTime < lastBeatTime) {
                ResetObservation();
                observedSelf = observation.Self;
                observedOpponent = observation.Opponent;
                if (observedOpponent is IStrikerCombatObservationSource source) {
                    memory.ObserveOpponentState(source.ObservedState);
                    opponentStateSubscription = source.OnCombatStateEntered.Subscribe(memory.ObserveOpponentState).AddTo(this);
                } else {
                    opponentStateSubscription = observedOpponent.CurrentStateCategory.DistinctUntilChanged()
                        .Subscribe(memory.ObserveOpponentCategory).AddTo(this);
                }
            }
            if (lastBeat == observation.Signal.BeatIndex) return new AiAction(lastAction.Direction, null);
            var beatSeconds = lastBeat < 0 ? tuning.InitialBeatSeconds
                : Mathf.Clamp((observation.Signal.BeatTime - lastBeatTime) / (observation.Signal.BeatIndex - lastBeat), .08f, 2f);
            lastBeat = observation.Signal.BeatIndex;
            lastBeatTime = observation.Signal.BeatTime;

            var frame = sensor.Observe(observation, beatSeconds, tuning);
            if (frame.Self.Demon == null) {
                LastDecision = "Demon King combat observation is missing from the character prefab";
                return lastAction = AiAction.None;
            }
            memory.Observe(frame);
            var matchup = observation.Opponent.Striker.CurrentValue switch {
                Striker.Wizard => versusWizard,
                Striker.Hero => versusHero,
                Striker.DemonKing => versusDemonKing,
                _ => versusWarrior
            };
            var result = decision.Choose(frame, tuning, matchup, memory, difficulty);
            memory.Record(result, frame);
            LastDecision = result.Reason;
            LastDecisionScore = result.Score;
            return lastAction = result.Action;
        }

        protected override void OnDifficultyChanged(int level) => difficulty.Configure(level);
        protected override void OnAiEnabled() => ResetObservation();
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

    internal sealed class DemonKingAiMemory {
        bool initialized;
        float selfHp, opponentHp;
        Vector3 opponentPosition;
        DemonKingAiMove lastMove;
        StrikerStateCategory lastOffensiveCategory = StrikerStateCategory.Unknown;
        bool attackSinceDecision, guardSinceDecision, alternationSinceDecision;

        public float ShieldHabit { get; private set; }
        public float AttackHabit { get; private set; }
        public float AbHabit { get; private set; }
        public float RecentDamage { get; private set; }
        public float WhiffPressure { get; private set; }
        public Vector3 OpponentDrift { get; private set; }
        public float RetreatHabit { get; private set; }
        public float RepositionSpeed { get; private set; } = 8f;
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

        bool meleeEntered, rushEntered, shieldLaunched;
        readonly float[,] responseLoss = new float[4, ((int)DemonKingAiMove.Recover + 1) * 9];
        int lastPattern, lastResponse;
        bool hasResponse;

        public Core.LargeSatan.IWarriorMeleeObservation MeleeOpener { get; private set; }
        public float MeleeHabit { get; private set; }
        public float RushHabit { get; private set; }
        public float ShieldLaunchHabit { get; private set; }

        public void ObserveOpponentState(IStrikerState state) {
            if (state == null) return;
            ObserveOpponentCategory(state.Category);
            if (state is Core.LargeSatan.Attack0State opener) MeleeOpener = opener;
            meleeEntered |= state is Core.LargeSatan.Attack0State or Core.LargeSatan.Attack1State or Core.LargeSatan.Attack2State;
            rushEntered |= state is Core.LargeHero.RushState;
            shieldLaunched |= state is Core.LargeWizard.GuardState { HasLaunchedShield: true };
        }

        int PatternIndex => ShieldLaunchHabit > .15f && ShieldLaunchHabit >= Mathf.Max(MeleeHabit, RushHabit) ? 2
            : RushHabit > .15f && RushHabit >= MeleeHabit ? 3 : MeleeHabit > .15f ? 1 : 0;

        static int ResponseIndex(DemonKingAiMove move, Vector2 direction, DemonKingAiFrame frame) {
            var toward = frame.Opponent.Position.x >= frame.Self.Position.x ? 1f : -1f;
            var horizontal = Mathf.RoundToInt(direction.x * toward);
            var vertical = Mathf.RoundToInt(direction.y);
            return (int)move * 9 + (vertical + 1) * 3 + horizontal + 1;
        }

        public float ResponsePenalty(DemonKingAiMove move, Vector2 direction, DemonKingAiFrame frame)
            => responseLoss[PatternIndex, ResponseIndex(move, direction, frame)];

        void AdvancePatterns() {
            // Count entries once per decision, not every frame spent inside one attack.
            MeleeHabit = Mathf.Lerp(MeleeHabit, meleeEntered ? 1f : 0f, meleeEntered ? .4f : .12f);
            RushHabit = Mathf.Lerp(RushHabit, rushEntered ? 1f : 0f, rushEntered ? .4f : .12f);
            ShieldLaunchHabit = Mathf.Lerp(ShieldLaunchHabit, shieldLaunched ? 1f : 0f, shieldLaunched ? .4f : .12f);
            meleeEntered = rushEntered = shieldLaunched = false;
            for (var pattern = 0; pattern < responseLoss.GetLength(0); pattern++)
                for (var response = 0; response < responseLoss.GetLength(1); response++) responseLoss[pattern, response] *= .85f;
        }

        void LearnResponse(float received, float dealt) {
            if (!hasResponse) return;
            responseLoss[lastPattern, lastResponse] = Mathf.Clamp(responseLoss[lastPattern, lastResponse]
                + received * 1.2f - dealt * .6f, 0f, 24f);
        }

        public void Observe(DemonKingAiFrame frame) {
            AdvancePatterns();
            var enemy = frame.Opponent;
            var away = Mathf.Sign(enemy.Position.x - frame.Self.Position.x);
            var movingAway = enemy.Velocity.x * away > 1f;
            RetreatHabit = Mathf.Lerp(RetreatHabit, movingAway ? 1f : 0f, .2f);
            // Ignore hit-stun knockback and teleports when learning a movement response.
            if (enemy.Category is StrikerStateCategory.Idle or StrikerStateCategory.Dash)
                RepositionSpeed = Mathf.Lerp(RepositionSpeed, Mathf.Clamp(Mathf.Abs(enemy.Velocity.x), 4f, 18f), .2f);
            var self = frame.Self.Striker.HitPoint.CurrentValue;
            var opponent = frame.Opponent.Striker.HitPoint.CurrentValue;
            if (initialized) {
                var dealt = Mathf.Max(0f, opponentHp - opponent);
                var received = Mathf.Max(0f, selfHp - self);
                LearnResponse(received, dealt);
                RecentDamage = RecentDamage * .6f + received;
                StaleBeats = dealt > .01f || received > .01f ? 0 : Mathf.Min(30, StaleBeats + 1);
                if (dealt > .01f) WhiffPressure = 0f;
                else if (lastMove is DemonKingAiMove.Melee or DemonKingAiMove.Uppercut or DemonKingAiMove.HeelDrop or DemonKingAiMove.Launch)
                    WhiffPressure = Mathf.Min(4f, WhiffPressure + 1f);
                var drift = (frame.Opponent.Position - opponentPosition) / Mathf.Max(.08f, frame.BeatSeconds);
                if (drift.magnitude < 30f) OpponentDrift = Vector3.Lerp(OpponentDrift, drift, .5f);
            }
            AbHabit = frame.Opponent.Striker.Striker.CurrentValue == Striker.Warrior
                ? Mathf.Lerp(AbHabit, alternationSinceDecision ? 1f : 0f, alternationSinceDecision ? .4f : .08f) : 0f;
            ShieldHabit = Mathf.Lerp(ShieldHabit, guardSinceDecision || frame.OpponentShields.Count >= 2 ? 1f : 0f, .3f);
            AttackHabit = Mathf.Lerp(AttackHabit, attackSinceDecision || frame.Opponent.Category is StrikerStateCategory.Attack or StrikerStateCategory.Dash ? 1f : 0f, .3f);
            selfHp = self;
            opponentHp = opponent;
            opponentPosition = frame.Opponent.Position;
            attackSinceDecision = guardSinceDecision = alternationSinceDecision = false;
            initialized = true;
        }

        public void Record(DemonKingAiChoice choice, DemonKingAiFrame frame) {
            lastPattern = PatternIndex;
            lastResponse = ResponseIndex(choice.Move, choice.Action.Direction, frame);
            hasResponse = choice.Move is not (DemonKingAiMove.Wait or DemonKingAiMove.Recover or DemonKingAiMove.Special);
            GuardStreak = choice.Move == DemonKingAiMove.Guard ? GuardStreak + 1 : 0;
            lastMove = choice.Move;
        }

        public void Reset() {
            MeleeOpener = null;
            MeleeHabit = RushHabit = ShieldLaunchHabit = 0f;
            meleeEntered = rushEntered = shieldLaunched = false;
            hasResponse = false;
            Array.Clear(responseLoss, 0, responseLoss.Length);
            initialized = false;
            ShieldHabit = AttackHabit = AbHabit = RecentDamage = WhiffPressure = 0f;
            StaleBeats = GuardStreak = 0;
            OpponentDrift = Vector3.zero;
            RetreatHabit = 0f;
            RepositionSpeed = 8f;
            lastMove = DemonKingAiMove.Wait;
            lastOffensiveCategory = StrikerStateCategory.Unknown;
            attackSinceDecision = guardSinceDecision = alternationSinceDecision = false;
        }
    }
}
