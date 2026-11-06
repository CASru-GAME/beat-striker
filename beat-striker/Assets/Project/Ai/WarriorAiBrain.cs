using System;
using R3;
using UnityEngine;

namespace Alice {
    [Serializable]
    public sealed record WarriorAiTuning {
        [Min(.05f)] public float InitialBeatSeconds = .5f;
        [Min(.01f)] public float GroundClearance = .2f;
        [Min(1f)] public float ProjectileObservationRadius = 45f;
        [Min(0f)] public float PredictionMargin = .18f;
        [Min(0f)] public float AttackReach = 2.2f;
        [Min(0f)] public float AttackVerticalReach = 1.3f;
        [Min(0f)] public float AttackSpeed = 15f;
        [Min(.01f)] public float AttackDuration = .3f;
        [Min(.01f)] public float ComboDuration = .5f;
        [Min(0f)] public float FinisherSpeed = 40f;
        [Min(0f)] public float AirAttackSpeed = 5f;
        [Min(.01f)] public float AirAttackDuration = .5f;
        [Min(0f)] public float DiveAttackSpeed = 30f;
        [Min(0f)] public float DashSpeed = 20f;
        [Min(.01f)] public float DashDuration = .3f;
        [Min(0f)] public float JumpSpeed = 38f;
        [Min(0f)] public float AirJumpSpeed = 14f;
        [Min(0f)] public float AirSuperJumpSpeed = 9f;
        [Min(0f)] public float AirJumpAcceleration = 30f;
        [Min(.01f)] public float AirJumpDuration = .5f;
        [Min(.01f)] public float JumpDuration = .4f;
        [Min(0f)] public float DiveSpeed = 20f;
        [Min(0f)] public float GuardShift = .2f;
        [Min(0f)] public float ThrowSpeed = 35f;
        [Min(.01f)] public float ThrowSeconds = .8f;
        [Min(0f)] public float ThrowReleaseDelay = 0f;
        [Min(0f)] public float ThrowMinimumDistance = 5f;
        [Min(0f)] public float ThrowMaximumDistance = 10f;
        [Min(0f)] public float RecoilSpeed = 12f;
        [Min(0f)] public float RiskWeight = 32f;
        [Min(0f)] public float DamageWeight = 1.8f;
        [Min(0f)] public float PositionWeight = 2.4f;
        [Min(0f)] public float SpecialScore = 35f;
    }

    [Serializable]
    public sealed record WarriorAiMatchup {
        [Min(0f)] public float PreferredDistance = 3f;
        [Min(0f)] public float OpponentReach = 3.5f;
        [Min(0f)] public float Pressure = 1f;
        [Min(0f)] public float ProjectileCaution = 1f;
        public float JumpPreference;
    }

    [AddComponentMenu("AI/Warrior Tactical Brain")]
    public sealed class WarriorAiBrain : AiBrain {
        [SerializeField] WarriorAiTuning tuning = new();
        [Header("Matchups")]
        [SerializeField] WarriorAiMatchup versusWarrior = new() {
            PreferredDistance = 3.2f, OpponentReach = 4.2f, Pressure = 1f, ProjectileCaution = 1.15f, JumpPreference = .2f
        };
        [SerializeField] WarriorAiMatchup versusWizard = new() {
            PreferredDistance = 2.1f, OpponentReach = 2.6f, Pressure = 1.35f, ProjectileCaution = 1.4f, JumpPreference = 1.1f
        };
        [SerializeField] WarriorAiMatchup versusHero = new() {
            PreferredDistance = 3.5f, OpponentReach = 3.5f, Pressure = .95f, ProjectileCaution = 1.15f, JumpPreference = .1f
        };
        [SerializeField] WarriorAiMatchup versusDemonKing = new() {
            PreferredDistance = 3.8f, OpponentReach = 3.2f, Pressure = .95f, ProjectileCaution = 1.5f, JumpPreference = -.3f
        };

        readonly WarriorAiSensor sensor = new();
        readonly WarriorAiDecision decision = new();
        readonly WarriorAiMemory memory = new();
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
            if (observation.Self == null || observation.Opponent == null || observation.Self.Striker.CurrentValue != Striker.Warrior) {
                return AiAction.None;
            }
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
            if (lastBeat >= 0) {
                var interval = (observation.Signal.BeatTime - lastBeatTime) / (observation.Signal.BeatIndex - lastBeat);
                beatSeconds = Mathf.Clamp(interval, .08f, 2f);
            } else {
                beatSeconds = tuning.InitialBeatSeconds;
            }
            lastBeat = observation.Signal.BeatIndex;
            lastBeatTime = observation.Signal.BeatTime;

            var frame = sensor.Observe(observation, beatSeconds, tuning);
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
            lastAction = result.Action;
            return lastAction;
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
            observedSelf = null;
            observedOpponent = null;
            lastBeat = -1;
            lastBeatTime = 0f;
            lastAction = AiAction.None;
            LastDecision = string.Empty;
            LastDecisionScore = 0f;
        }
    }

    internal sealed class WarriorAiMemory {
        float selfHitPoint;
        float opponentHitPoint;
        Vector3 opponentPosition;
        bool initialized;
        WarriorAiMove lastMove;
        bool attackSinceDecision, guardSinceDecision;

        public float RecentDamage { get; private set; }
        public float RecentHits { get; private set; }
        public float GuardHabit { get; private set; }
        public float AttackHabit { get; private set; }
        public float WhiffPressure { get; private set; }
        public int GuardStreak { get; private set; }
        public int DashStreak { get; private set; }
        public int StaleBeats { get; private set; }
        public Vector3 OpponentDrift { get; private set; }
        public float RetreatHabit { get; private set; }
        public float RepositionSpeed { get; private set; } = 8f;
        public bool NextAirJumpIsSuper { get; private set; }

        bool meleeEntered, rushEntered, shieldLaunched;
        readonly float[,] responseLoss = new float[4, ((int)WarriorAiMove.Recover + 1) * 9];
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

        static int ResponseIndex(WarriorAiMove move, Vector2 direction, WarriorAiFrame frame) {
            var toward = frame.Opponent.Position.x >= frame.Self.Position.x ? 1f : -1f;
            var horizontal = Mathf.RoundToInt(direction.x * toward);
            var vertical = Mathf.RoundToInt(direction.y);
            return (int)move * 9 + (vertical + 1) * 3 + horizontal + 1;
        }

        public float ResponsePenalty(WarriorAiMove move, Vector2 direction, WarriorAiFrame frame)
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

        public void ObserveOpponentCategory(StrikerStateCategory category) {
            attackSinceDecision |= category is StrikerStateCategory.Attack or StrikerStateCategory.Dash;
            guardSinceDecision |= category == StrikerStateCategory.Guard;
        }

        public void Observe(WarriorAiFrame frame) {
            AdvancePatterns();
            var enemy = frame.Opponent;
            var away = Mathf.Sign(enemy.Position.x - frame.Self.Position.x);
            var movingAway = enemy.Velocity.x * away > 1f;
            RetreatHabit = Mathf.Lerp(RetreatHabit, movingAway ? 1f : 0f, .2f);
            // Ignore hit-stun knockback and teleports when learning a movement response.
            if (enemy.Category is StrikerStateCategory.Idle or StrikerStateCategory.Dash)
                RepositionSpeed = Mathf.Lerp(RepositionSpeed, Mathf.Clamp(Mathf.Abs(enemy.Velocity.x), 4f, 18f), .2f);
            var currentSelfHp = frame.Self.Striker.HitPoint.CurrentValue;
            var currentOpponentHp = frame.Opponent.Striker.HitPoint.CurrentValue;
            if (frame.Self.Grounded) NextAirJumpIsSuper = false;
            else if (frame.Self.State is Core.LargeSatan.AirJumpState) NextAirJumpIsSuper = true;
            else if (frame.Self.State is Core.LargeSatan.AirSuperJumpState) NextAirJumpIsSuper = false;
            if (initialized) {
                var received = Mathf.Max(0f, selfHitPoint - currentSelfHp);
                var dealt = Mathf.Max(0f, opponentHitPoint - currentOpponentHp);
                LearnResponse(received, dealt);
                RecentDamage = RecentDamage * .55f + received;
                RecentHits = RecentHits * .55f + dealt;
                if (lastMove == WarriorAiMove.Attack || lastMove == WarriorAiMove.Throw) {
                    WhiffPressure = dealt > .01f ? 0f : Mathf.Min(4f, WhiffPressure + 1f);
                } else {
                    WhiffPressure *= .6f;
                }
                StaleBeats = received > .01f || dealt > .01f ? 0 : Mathf.Min(StaleBeats + 1, 30);
                var drift = (frame.Opponent.Position - opponentPosition) / Mathf.Max(frame.BeatSeconds, .08f);
                OpponentDrift = Vector3.Lerp(OpponentDrift, Vector3.ClampMagnitude(drift, 12f), .55f);
            }
            GuardHabit = Mathf.Lerp(GuardHabit, guardSinceDecision || frame.Opponent.Category == StrikerStateCategory.Guard ? 1f : 0f, .25f);
            AttackHabit = Mathf.Lerp(AttackHabit, attackSinceDecision || frame.Opponent.Category is StrikerStateCategory.Attack or StrikerStateCategory.Dash ? 1f : 0f, .25f);
            attackSinceDecision = guardSinceDecision = false;
            selfHitPoint = currentSelfHp;
            opponentHitPoint = currentOpponentHp;
            opponentPosition = frame.Opponent.Position;
            initialized = true;
        }

        public void Record(WarriorAiChoice result, WarriorAiFrame frame) {
            lastPattern = PatternIndex;
            lastResponse = ResponseIndex(result.Move, result.Action.Direction, frame);
            hasResponse = result.Move is not (WarriorAiMove.Wait or WarriorAiMove.Recover or WarriorAiMove.Special);
            GuardStreak = result.Move == WarriorAiMove.Guard ? GuardStreak + 1 : 0;
            DashStreak = result.Move == WarriorAiMove.Dash ? DashStreak + 1 : 0;
            lastMove = result.Move;
        }

        public void Reset() {
            MeleeOpener = null;
            MeleeHabit = RushHabit = ShieldLaunchHabit = 0f;
            meleeEntered = rushEntered = shieldLaunched = false;
            hasResponse = false;
            Array.Clear(responseLoss, 0, responseLoss.Length);
            initialized = false;
            RecentDamage = RecentHits = GuardHabit = AttackHabit = WhiffPressure = 0f;
            GuardStreak = DashStreak = StaleBeats = 0;
            OpponentDrift = Vector3.zero;
            RetreatHabit = 0f;
            RepositionSpeed = 8f;
            NextAirJumpIsSuper = false;
            lastMove = WarriorAiMove.Wait;
            attackSinceDecision = guardSinceDecision = false;
        }
    }
}
