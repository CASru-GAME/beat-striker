using System.Collections.Generic;
using UnityEngine;

namespace Alice {
    internal enum WarriorAiMove { Wait, Attack, PrepareThrow, Throw, Dash, Jump, Dive, Guard, Special, Recover }

    internal sealed record WarriorAiChoice(AiAction Action, WarriorAiMove Move, float Score, string Reason);

    internal sealed record WarriorAiCandidate(
        WarriorAiMove Move, Vector2 Direction, Vector3 MotionDirection,
        float Speed, float Duration, float EndSpeedRatio, float Damage, string Reason
    );

    internal sealed class WarriorAiDecision {
        const int SpearSamples = 12;

        bool advancedTactics;
        int opponentReply;
        float predictionWeight = 1f;
        float followUpWeight = 1f;
        float threatWeight = 1f;

        readonly List<WarriorAiThreat> forecast = new(8);
        bool hasMeleeForecast, hasRushForecast;
        readonly List<WarriorAiCandidate> candidates = new(20);
        readonly List<WarriorAiThreat> spearForecast = new(SpearSamples * 2);

        public WarriorAiChoice Choose(WarriorAiFrame frame, WarriorAiTuning tuning, WarriorAiMatchup matchup, WarriorAiMemory memory, AiDifficulty difficulty) {
            advancedTactics = difficulty.UsesAdvancedTactics;
            opponentReply = 0;
            predictionWeight = difficulty.PredictionWeight;
            followUpWeight = difficulty.FollowUpWeight;
            threatWeight = difficulty.ThreatWeight;
            var self = frame.Self;
            if (self.Striker.HitPoint.CurrentValue <= 0f || frame.Opponent.Striker.HitPoint.CurrentValue <= 0f) {
                return Idle("Round ended");
            }
            if (difficulty.MissesBeat()) return Idle("Missed the beat");
            if (self.Category == StrikerStateCategory.Special) return Idle("Special in progress");
            if (self.State is Core.LargeSatan.StunCancelState) return Idle("Stun recovery in progress");
            if (self.State is Core.LargeSatan.StunState) {
                return new WarriorAiChoice(new AiAction(Vector2.zero, GamePadButton.North), WarriorAiMove.Recover, 0f, "Break hit stun");
            }

            BuildCandidates(frame, tuning, memory);
            BuildSpearForecast(frame);
            var (selected, score) = difficulty.SelectCandidate(
                candidates,
                static candidate => candidate.Move != WarriorAiMove.Wait,
                static candidate => ButtonFor(candidate.Move),
                candidate => IsDefensive(candidate, frame),
                (candidate, anticipationWeight) => ScoreReplies(candidate, frame, tuning, matchup, memory, anticipationWeight));
            if (selected == null) return Idle("Observe");
            return new(new AiAction(selected.Direction, ButtonFor(selected.Move)), selected.Move, score, selected.Reason);
        }

        static bool IsDefensive(WarriorAiCandidate candidate, WarriorAiFrame frame) {
            var toward = frame.Opponent.Body.center.x - frame.Self.Body.center.x;
            return candidate.Move == WarriorAiMove.Guard
                || candidate.Move is WarriorAiMove.Dash or WarriorAiMove.Jump or WarriorAiMove.Dive
                    && (candidate.Direction.x * toward <= 0f
                        || candidate.Direction.y > 0f && frame.Opponent.Body.center.y < frame.Self.Body.center.y + 2f);
        }

        void BuildCandidates(WarriorAiFrame frame, WarriorAiTuning tuning, WarriorAiMemory memory) {
            candidates.Clear();
            var self = frame.Self;
            var delta = frame.Opponent.Body.center - self.Body.center;
            var toward = delta.x >= 0f ? Vector2.right : Vector2.left;
            var aim = SnapDirection(delta + PredictOpponentMove(frame, tuning.ThrowReleaseDelay + delta.magnitude / Mathf.Max(tuning.ThrowSpeed, .01f), memory));
            var isCharging = self.State is Core.LargeSatan.ChargeState || self.Category == StrikerStateCategory.Charge;
            if (isCharging) {
                var throwDirection = self.State is Core.LargeSatan.ChargeState aimedCharge && aimedCharge.ObservedEmitState != null
                    ? aimedCharge.ObservedEmitState.ObserveThrowDirection(aim, self.Striker.LookDirection.CurrentValue) : (Vector3)aim;
                Add(WarriorAiMove.Throw, aim, throwDirection, 0f, tuning.ThrowSeconds, 1f, 10f, "Lead spear throw");
                if (self.State is Core.LargeSatan.ChargeState charge) {
                    var look = self.Striker.LookDirection.CurrentValue;
                    var stored = Quaternion.AngleAxis(-charge.ObservedPoleArm.PlannedEmitAngle * Mathf.Sign(look.x), Vector3.forward) * look;
                    Add(WarriorAiMove.Throw, Vector2.zero, stored, 0f, tuning.ThrowSeconds, 1f, 10f, "Release aimed spear");
                    if (advancedTactics && charge.ObservedEmitState != null) {
                        for (var i = 0; i < 8; i++) {
                            var angle = i * Mathf.PI * .25f;
                            var input = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                            var direction = charge.ObservedEmitState.ObserveThrowDirection(input, look);
                            Add(WarriorAiMove.Throw, input, direction, 0f, tuning.ThrowSeconds, 1f, 10f, "Choose a spear lane for the hit and warp follow-up");
                        }
                    }
                }
            } else {
                var isLinkedAttack = self.State is Core.LargeSatan.Attack0State or Core.LargeSatan.AirAttackState;
                var speed = self.Grounded || isLinkedAttack ? tuning.AttackSpeed : tuning.AirAttackSpeed;
                var duration = self.State is Core.LargeSatan.Attack0State ? tuning.ComboDuration : tuning.AttackDuration;
                if (!self.Grounded && !isLinkedAttack) duration = tuning.AirAttackDuration;
                var damage = 10f;
                var reason = "Lunge into opening";
                if (self.State is Core.LargeSatan.Attack1State) {
                    speed = tuning.FinisherSpeed;
                    duration = tuning.ComboDuration;
                    damage = 15f;
                    reason = "Finish thrust combo";
                } else if (self.State is Core.LargeSatan.Attack0State) {
                    reason = "Continue confirmed combo";
                }
                Add(WarriorAiMove.Attack, toward, toward, speed, duration, .1f, damage, reason);
                if (self.State is Core.LargeSatan.Attack1State) {
                    Add(WarriorAiMove.Attack, Vector2.zero, toward, tuning.AttackSpeed, tuning.AttackDuration, .1f, 10f, "Reset close-range combo");
                }
                if (self.Grounded && self.State is not (Core.LargeSatan.Attack0State or Core.LargeSatan.Attack1State) && delta.y > 1f) {
                    var rising = (toward + Vector2.up).normalized;
                    Add(WarriorAiMove.Attack, rising, rising, speed, duration, .1f, damage, "Intercept airborne opponent");
                    Add(WarriorAiMove.Attack, -toward, Vector3.up, speed, duration, .1f, damage, "Vertical anti-air");
                }
                if (!self.Grounded && !isLinkedAttack && self.State is not Core.LargeSatan.Attack1State && delta.y < -.6f) {
                    Add(WarriorAiMove.Attack, Vector2.down, Vector3.down, tuning.DiveAttackSpeed, tuning.AirAttackDuration, 1f, 20f, "Dive attack from above");
                }
                Add(WarriorAiMove.PrepareThrow, aim, Vector3.zero, .5f, frame.BeatSeconds, 1f, 0f, "Prepare ranged pressure");
            }

            var dashSpeed = tuning.DashSpeed + Mathf.Min(memory.DashStreak, 2) * 4f;
            // In the air, the dash node selects an air jump unless the input is down.
            if (self.Grounded) {
                Add(WarriorAiMove.Dash, toward, toward, dashSpeed, tuning.DashDuration, .1f, 0f, "Close distance");
                Add(WarriorAiMove.Dash, -toward, -toward, tuning.DashSpeed, tuning.DashDuration, .1f, 0f, "Bait and retreat");
            } else {
                Add(WarriorAiMove.Dive, Vector2.down, Vector3.down, tuning.DiveSpeed, .45f, 1f, 0f, "Land outside threat");
            }
            var jumpSpeed = self.Grounded ? tuning.JumpSpeed : memory.NextAirJumpIsSuper ? tuning.AirSuperJumpSpeed : tuning.AirJumpSpeed;
            var jumpDuration = self.Grounded ? tuning.JumpDuration : tuning.AirJumpDuration;
            Add(WarriorAiMove.Jump, (toward + Vector2.up).normalized, (toward + Vector2.up).normalized, jumpSpeed, jumpDuration, .01f, 0f, "Approach over projectiles");
            Add(WarriorAiMove.Jump, (-toward + Vector2.up).normalized, (-toward + Vector2.up).normalized, jumpSpeed, jumpDuration, .01f, 0f, "Escape upward and away");
            Add(WarriorAiMove.Jump, Vector2.up, Vector3.up, jumpSpeed, jumpDuration, .01f, 0f, "Evade ground attack");
            var guardSeconds = (self.Striker as IStrikerCombatObservationSource)?.NextGuardSeconds ?? 1f;
            Add(WarriorAiMove.Guard, toward, toward, 0f, guardSeconds, 1f, 0f, "Dodge into punish range");
            Add(WarriorAiMove.Guard, -toward, -toward, 0f, guardSeconds, 1f, 0f, "Dodge incoming threat");
            Add(WarriorAiMove.Guard, Vector2.zero, Vector3.zero, 0f, guardSeconds, 1f, 0f, "Dodge without shifting");

            var maxSpecial = self.Striker.MaxSpecialPoint.CurrentValue;
            if (maxSpecial > 0f && self.Striker.SpecialPoint.CurrentValue >= maxSpecial - .001f) {
                Add(WarriorAiMove.Special, Vector2.zero, Vector3.zero, 0f, 1f, 1f, 0f, "Gate of Arrow");
            }
            Add(WarriorAiMove.Wait, Vector2.zero, Vector3.zero, 0f, frame.BeatSeconds, 1f, 0f, "Allow committed move to finish");
        }

        void Add(WarriorAiMove move, Vector2 input, Vector3 motion, float speed, float duration, float ratio, float damage, string reason) {
            candidates.Add(new WarriorAiCandidate(move, input, motion, speed, duration, ratio, damage, reason));
        }

        // Evaluate visible motion and both horizontal replies. No opponent input is read.
        float ScoreReplies(WarriorAiCandidate candidate, WarriorAiFrame frame, WarriorAiTuning tuning,
            WarriorAiMatchup matchup, WarriorAiMemory memory, float anticipationWeight) {
            opponentReply = 0;
            var continuing = Score(candidate, frame, tuning, matchup, memory, anticipationWeight);
            if (!advancedTactics || float.IsInfinity(continuing)) return continuing;
            opponentReply = -1;
            var retreating = Score(candidate, frame, tuning, matchup, memory, anticipationWeight);
            opponentReply = 1;
            var approaching = Score(candidate, frame, tuning, matchup, memory, anticipationWeight);
            opponentReply = 0;
            var retreatWeight = Mathf.Lerp(.15f, .4f, memory.RetreatHabit);
            var approachWeight = Mathf.Lerp(.15f, .35f, memory.AttackHabit);
            var expected = continuing * (1f - retreatWeight - approachWeight)
                + retreating * retreatWeight + approaching * approachWeight;
            // Keep a small worst-case weight so a single optimistic read cannot dominate.
            return expected * .85f + Mathf.Min(continuing, Mathf.Min(retreating, approaching)) * .15f;
        }

        // A body attack that has not started may be interrupted by a reliable fast counter.
        // Existing hitboxes/projectiles keep their full risk in ThreatRisk.
        float CounterPressure(WarriorAiFrame frame, float chance, float impact) {
            if (!advancedTactics || chance < .7f || impact > .25f
                || frame.Opponent.Striker.Striker.CurrentValue == Striker.Hero
                || frame.Opponent.Category is StrikerStateCategory.Special or StrikerStateCategory.Guard
                || frame.Opponent.State is Core.DarkSatan.Attack2State or Core.LargeSatan.Attack2State) return 1f;
            return Mathf.Lerp(1f, .3f, chance * Mathf.Clamp01((.3f - impact) / .25f));
        }

        Vector3 ReplyPosition(WarriorAiFrame frame, Vector3 predicted, float seconds, WarriorAiMemory memory) {
            if (!advancedTactics || opponentReply == 0 || seconds <= frame.ExecutionDelay) return predicted;
            var enemy = frame.Opponent;
            if (enemy.Category is StrikerStateCategory.Special or StrikerStateCategory.Unknown
                || enemy.State is Core.LargeHero.StunState or Core.LargeWizard.StunState or Core.LargeSatan.StunState
                    or Core.DarkSatan.StunState) return predicted;
            var locked = enemy.State is Core.LargeHero.GuardState guard ? guard.ObservationDuration
                : enemy.State is Core.DarkSatan.DemonDirectionalAttackState strike ? strike.ObservationDuration : 0f;
            var remaining = Mathf.Max(0f, locked - enemy.StateSeconds - frame.ExecutionDelay);
            var responseAt = frame.ExecutionDelay + Mathf.Ceil(remaining / frame.BeatSeconds) * frame.BeatSeconds;
            var available = Mathf.Max(0f, seconds - responseAt);
            if (available <= 0f) return predicted;
            if (enemy.State is Core.LargeSatan.EmitState emit && opponentReply < 0
                && seconds + enemy.StateSeconds < emit.ObservationFlightSeconds)
                return ClampToArena(enemy.Body.center + enemy.Velocity * Mathf.Min(seconds, .12f), enemy);
            var toward = Mathf.Sign(frame.Self.Body.center.x - enemy.Body.center.x);
            if (toward == 0f) toward = -Mathf.Sign(enemy.Striker.LookDirection.CurrentValue.x);
            // Replace the lateral drift after the reply instead of counting it twice.
            var drift = enemy.Velocity.x * Mathf.Min(available, .3f);
            predicted.x += toward * opponentReply * memory.RepositionSpeed * .24f * (1f - Mathf.Exp(-available / .24f)) - drift;
            return ClampToArena(predicted, enemy);
        }

        float Score(WarriorAiCandidate candidate, WarriorAiFrame frame, WarriorAiTuning tuning, WarriorAiMatchup matchup,
            WarriorAiMemory memory, float anticipationWeight) {
            var horizon = Mathf.Clamp(frame.BeatSeconds * 1.2f, .3f, 1.1f);
            var end = PredictSelf(candidate, frame, tuning, horizon);
            var enemyEnd = frame.Opponent.Body.center + PredictOpponentMove(frame, horizon + frame.ExecutionDelay, memory);
            var startGap = Mathf.Abs(frame.Opponent.Body.center.x - frame.Self.Body.center.x);
            var endGap = Mathf.Abs(enemyEnd.x - end.x);
            var positionGain = Mathf.Abs(startGap - matchup.PreferredDistance) - Mathf.Abs(endGap - matchup.PreferredDistance);
            var score = Mathf.Clamp(positionGain, -6f, 6f) * tuning.PositionWeight * matchup.Pressure;
            BuildSpellForecast(candidate, frame, tuning, anticipationWeight);
            BuildPressureForecast(candidate, frame, tuning, memory, anticipationWeight);
            var risk = ThreatRisk(candidate, frame, tuning, horizon, matchup.ProjectileCaution);
            var anticipated = AnticipationRisk(candidate, frame, tuning, matchup, memory, end, enemyEnd, horizon)
                + SpearRisk(candidate, frame, tuning, horizon) * matchup.ProjectileCaution;
            if (advancedTactics && candidate.Move == WarriorAiMove.Attack) {
                var chance = AttackChance(candidate, frame, tuning, memory, out var impact);
                anticipated *= CounterPressure(frame, chance, impact);
            }
            risk += anticipated * anticipationWeight;
            var hpRatio = frame.Self.Striker.HitPoint.CurrentValue / Mathf.Max(1f, frame.Self.Striker.MaxHitPoint.CurrentValue);
            score -= risk * threatWeight * tuning.RiskWeight * (hpRatio < .3f ? 1.3f : 1f);
            score -= BoundaryCost(candidate, frame, tuning, horizon);

            switch (candidate.Move) {
                case WarriorAiMove.Attack: {
                    var chance = AttackChance(candidate, frame, tuning, memory);
                    var damage = Mathf.Min(candidate.Damage, frame.Opponent.Striker.HitPoint.CurrentValue);
                    score += chance * damage * tuning.DamageWeight * matchup.Pressure;
                    score += chance * Mathf.Min(4f, memory.RecentHits * .3f);
                    score -= (1f - chance) * (6f + memory.WhiffPressure * 2f);
                    if (candidate.Damage >= frame.Opponent.Striker.HitPoint.CurrentValue) score += chance * 12f;
                    if (frame.Self.State is Core.LargeSatan.Attack2State && memory.RecentHits > 0f) score -= 2f;
                    break;
                }
                case WarriorAiMove.Throw: {
                    var chance = ThrowChance(candidate, frame, tuning, memory);
                    score += chance * 21f * matchup.Pressure - (1f - chance) * 9f;
                    break;
                }
                case WarriorAiMove.PrepareThrow: {
                    score += startGap >= tuning.ThrowMinimumDistance && startGap <= tuning.ThrowMaximumDistance ? 11f : -10f;
                    score += (memory.GuardHabit * predictionWeight) * 2f;
                    var throwOrigin = CandidateOrigin(candidate, frame);
                    if (ShieldBlocks(frame, throwOrigin, enemyEnd - throwOrigin, enemyEnd, .2f)) score -= 6f;
                    if (Mathf.Abs(enemyEnd.y - end.y) > 6f) score -= 5f;
                    if (frame.Self.State is Core.LargeSatan.EmitState) score -= 3f;
                    break;
                }
                case WarriorAiMove.Dash:
                    score += endGap < tuning.AttackReach + 2f ? 3f : 0f;
                    score += Mathf.Min(memory.StaleBeats, 8) * .2f;
                    if (startGap < matchup.PreferredDistance && Vector3.Dot(candidate.MotionDirection, enemyEnd - end) > 0f) score -= 4f;
                    break;
                case WarriorAiMove.Jump: {
                    score += matchup.JumpPreference;
                    if (endGap < startGap) score += (memory.GuardHabit * predictionWeight) * 4f;
                    var start = frame.Self.Body.center;
                    var target = frame.Opponent.Body.center;
                    if (endGap < startGap && ShieldBlocks(frame, start, target - start, target, .25f)
                        && !ShieldBlocks(frame, end, enemyEnd - end, enemyEnd, .25f)) score += 4f;
                    score -= Mathf.Max(0f, end.y - enemyEnd.y - 2f) * 1.4f;
                    if (!frame.Self.Grounded && frame.Self.Body.center.y >= frame.Opponent.Body.center.y + 2f) score -= 6f;
                    break;
                }
                case WarriorAiMove.Dive:
                    score += frame.Self.Body.center.y > frame.Opponent.Body.center.y + 1f ? 3f : -4f;
                    break;
                case WarriorAiMove.Guard:
                    score -= 2f + Mathf.Min(memory.GuardStreak, 4) * 1.25f;
                    // A dodge after receiving a hit allows an immediate punish next beat.
                    score += Mathf.Min(2f, memory.RecentDamage * .1f);
                    break;
                case WarriorAiMove.Special:
                    score += tuning.SpecialScore;
                    if (frame.Opponent.Category == StrikerStateCategory.Guard) score -= 6f;
                    break;
                case WarriorAiMove.Wait:
                    score -= 5f + Mathf.Min(memory.StaleBeats, 10);
                    if (frame.Self.Category == StrikerStateCategory.Attack && memory.RecentHits > 0f) score += 2f;
                    break;
            }
            score -= followUpWeight * memory.ResponsePenalty(candidate.Move, candidate.Direction, frame);
            // Reward a safe position that can convert into an attack on the next beat.
            if (followUpWeight > 0f) score += followUpWeight * FollowUpValue(candidate, frame, tuning, memory) / (1f + risk * 2f);
            return score;
        }

        float FollowUpValue(WarriorAiCandidate candidate, WarriorAiFrame frame, WarriorAiTuning tuning, WarriorAiMemory memory) {
            if (advancedTactics) return AdvancedFollowUp(candidate, frame, tuning, memory);
            if (candidate.Move is WarriorAiMove.Wait or WarriorAiMove.PrepareThrow or WarriorAiMove.Special) return 0f;
            var at = frame.BeatSeconds;
            var self = PredictSelf(candidate, frame, tuning, at);
            var target = frame.Opponent.Body.center + PredictOpponentMove(frame, at + frame.ExecutionDelay, memory);
            var delta = target - self;
            var reach = tuning.AttackReach + tuning.AttackSpeed * tuning.AttackDuration * .35f;
            var opening = Mathf.Clamp01((reach + frame.Opponent.Body.extents.x - Mathf.Abs(delta.x)) / 2f)
                * Mathf.Clamp01((tuning.AttackVerticalReach + frame.Opponent.Body.extents.y - Mathf.Abs(delta.y)) / 1.2f);
            if (ShieldBlocks(frame, self, delta, target, .2f)) opening *= .25f;
            return opening * 14f * GuardAvailability(frame.Opponent, at + frame.ExecutionDelay + .1f);
        }

        float AdvancedFollowUp(WarriorAiCandidate candidate, WarriorAiFrame frame, WarriorAiTuning tuning, WarriorAiMemory memory) {
            if (candidate.Move is WarriorAiMove.Wait or WarriorAiMove.Special) return 0f;
            var at = frame.BeatSeconds;
            var self = PredictSelf(candidate, frame, tuning, at);
            if (candidate.Move == WarriorAiMove.Throw && frame.Self.State is Core.LargeSatan.ChargeState charge && charge.ObservedEmitState != null) {
                var origin = charge.ObservedPoleArm.transform.position;
                var distance = charge.ObservedEmitState.ObserveTravelDistance(at);
                if (Physics.SphereCast(origin, charge.ObservedPoleArm.hitRadius, candidate.MotionDirection,
                    out var wall, distance, charge.ObservedPoleArm.wallMask, QueryTriggerInteraction.Ignore)) distance = wall.distance;
                self = ClampToArena(origin + candidate.MotionDirection * distance + frame.Self.Body.center - frame.Self.Position, frame.Self);
            }
            if (candidate.Move == WarriorAiMove.PrepareThrow) {
                var target = frame.Opponent.Body.center + PredictOpponentMove(frame, at + frame.ExecutionDelay + .3f, memory);
                var delta = target - self;
                var aim = ((Vector3)SnapDirection(delta)).normalized;
                var miss = (delta - aim * Vector3.Dot(delta, aim)).magnitude;
                var chance = Mathf.Clamp01((frame.Opponent.Body.extents.y + .7f - miss) / .8f);
                if (delta.magnitude > tuning.ThrowMaximumDistance * 1.5f || ShieldBlocks(frame, self, aim, target, .2f)) chance *= .15f;
                return chance * 12f * GuardAvailability(frame.Opponent, at + frame.ExecutionDelay + .3f);
            }
            var damage = candidate.Move == WarriorAiMove.Attack && frame.Self.State is Core.LargeSatan.Attack0State ? 15f : 10f;
            var reach = tuning.AttackReach + DecayingDistance(damage == 15f ? tuning.FinisherSpeed : tuning.AttackSpeed,
                .2f, tuning.AttackDuration, .1f);
            var best = 0f;
            for (var i = 0; i < 3; i++) {
                var impact = at + .05f + i * .1f;
                var target = frame.Opponent.Body.center + PredictOpponentMove(frame, impact + frame.ExecutionDelay, memory);
                var delta = target - self;
                var chance = Mathf.Clamp01((reach + frame.Opponent.Body.extents.x - Mathf.Abs(delta.x)) / 1.5f)
                    * Mathf.Clamp01((tuning.AttackVerticalReach + frame.Opponent.Body.extents.y * .4f - Mathf.Abs(delta.y)) / .7f);
                if (ShieldBlocks(frame, self, delta, target, .2f)) chance *= .2f;
                best = Mathf.Max(best, chance * GuardAvailability(frame.Opponent, impact + frame.ExecutionDelay));
            }
            return best * Mathf.Min(damage, frame.Opponent.Striker.HitPoint.CurrentValue) * 1.1f;
        }

        float AttackChance(WarriorAiCandidate candidate, WarriorAiFrame frame, WarriorAiTuning tuning, WarriorAiMemory memory)
            => AttackChance(candidate, frame, tuning, memory, out _);

        float AttackChance(WarriorAiCandidate candidate, WarriorAiFrame frame, WarriorAiTuning tuning, WarriorAiMemory memory, out float firstImpact) {
            firstImpact = float.PositiveInfinity;
            var best = 0f;
            var origin = CandidateOrigin(candidate, frame);
            for (int i = 0; i <= 10; i++) {
                var t = .05f * i;
                var self = PredictSelf(candidate, frame, tuning, t);
                var enemy = frame.Opponent.Body.center + PredictOpponentMove(frame, t + frame.ExecutionDelay, memory);
                var delta = enemy - self;
                var facing = candidate.MotionDirection.x != 0f ? Mathf.Sign(candidate.MotionDirection.x)
                    : Mathf.Sign(frame.Opponent.Position.x - frame.Self.Position.x);
                var reach = tuning.AttackReach + frame.Opponent.Body.extents.x;
                var verticalReach = tuning.AttackVerticalReach + frame.Opponent.Body.extents.y * .4f;
                var horizontal = delta.x * facing;
                var chance = Mathf.Clamp01((reach - horizontal) / .8f) * Mathf.Clamp01((horizontal + 1f) / .7f)
                    * Mathf.Clamp01((verticalReach - Mathf.Abs(delta.y)) / .6f);
                chance *= GuardAvailability(frame.Opponent, t + frame.ExecutionDelay);
                if (chance > 0f && ShieldBlocks(frame, origin, enemy - origin, enemy, .25f)) chance *= .3f;
                if (frame.Opponent.Category == StrikerStateCategory.Special) chance *= .1f;
                chance *= 1f - Mathf.Min(.45f, memory.WhiffPressure * .08f + (memory.GuardHabit * predictionWeight) * .2f);
                if (chance >= .7f) firstImpact = Mathf.Min(firstImpact, t);
                best = Mathf.Max(best, chance);
            }
            return best;
        }

        float ThrowChance(WarriorAiCandidate candidate, WarriorAiFrame frame, WarriorAiTuning tuning, WarriorAiMemory memory) {
            if (frame.Self.State is Core.LargeSatan.ChargeState charge && charge.ObservedEmitState != null) {
                var emit = charge.ObservedEmitState;
                var spearOrigin = charge.ObservedPoleArm.transform.position;
                var direction = candidate.MotionDirection.normalized;
                var best = 0f;
                for (var i = 0; i <= 16; i++) {
                    var t = emit.ObservationHoldSeconds + emit.ObservationFlightSeconds * i / 16f;
                    var target = frame.Opponent.Body.center + PredictOpponentMove(frame, t + frame.ExecutionDelay, memory);
                    var delta = target - spearOrigin;
                    var along = Vector3.Dot(delta, direction);
                    var perpendicular = (delta - direction * along).magnitude;
                    var tolerance = Mathf.Max(frame.Opponent.Body.extents.x, .5f) + .5f;
                    var chance = Mathf.Clamp01((tolerance + .4f - perpendicular) / .8f)
                        * Mathf.Clamp01((tolerance + .4f - Mathf.Abs(along - emit.ObserveTravelDistance(t))) / .8f);
                    if (along <= 0f) chance = 0f;
                    if (chance > 0f && ShieldBlocks(frame, spearOrigin, direction, target, charge.ObservedPoleArm.hitRadius)) chance *= .15f;
                    best = Mathf.Max(best, chance * GuardAvailability(frame.Opponent, t + frame.ExecutionDelay));
                }
                return best;
            }
            var origin = CandidateOrigin(candidate, frame);
            var flight = Vector3.Distance(origin, frame.Opponent.Body.center) / Mathf.Max(tuning.ThrowSpeed, .01f);
            var hitTime = tuning.ThrowReleaseDelay + flight;
            if (hitTime > tuning.ThrowSeconds) return 0f;
            var estimatedTarget = frame.Opponent.Body.center + PredictOpponentMove(frame, hitTime + frame.ExecutionDelay, memory);
            var estimatedDelta = estimatedTarget - origin;
            var forward = candidate.MotionDirection.normalized;
            var estimatedAlong = Vector3.Dot(estimatedDelta, forward);
            var miss = (estimatedDelta - forward * estimatedAlong).magnitude;
            var estimatedTolerance = Mathf.Max(frame.Opponent.Body.extents.x, .5f) + .5f;
            var transmission = ShieldBlocks(frame, origin, forward, estimatedTarget, .2f) ? .15f : 1f;
            return estimatedAlong <= 0f ? 0f : Mathf.Clamp01((estimatedTolerance + .4f - miss) / .8f) * transmission
                * GuardAvailability(frame.Opponent, hitTime + frame.ExecutionDelay);
        }

        static bool ShieldBlocks(WarriorAiFrame frame, Vector3 origin, Vector3 direction, Vector3 enemyCenter, float radius) {
            if (frame.OpponentShields.Count == 0 || direction.sqrMagnitude < .000001f) return false;
            var ray = new Ray(origin, direction.normalized);
            var body = frame.Opponent.Body;
            var translation = enemyCenter - body.center;
            body.center = enemyCenter;
            body.Expand(radius * 2f);
            if (body.Contains(origin) || !body.IntersectRay(ray, out var bodyDistance)) return false;
            foreach (var observedShield in frame.OpponentShields) {
                var shield = observedShield;
                shield.center += translation;
                shield.Expand(radius * 2f);
                if (shield.Contains(origin) || (shield.IntersectRay(ray, out var shieldDistance) && shieldDistance < bodyDistance)) return true;
            }
            return false;
        }

        float GuardAvailability(WarriorAiActor opponent, float future) {
            future *= predictionWeight;
            if (opponent.Category != StrikerStateCategory.Guard) return 1f;
            if (opponent.Striker.Striker.CurrentValue is Striker.Warrior or Striker.DemonKing) {
                var guardSeconds = (opponent.Striker as IStrikerCombatObservationSource)?.ActiveGuardSeconds ?? 1f;
                return future + opponent.StateSeconds < guardSeconds ? .02f : .9f;
            }
            // Wizard shields are evaluated by their actual position, including after GuardState.
            if (opponent.Striker.Striker.CurrentValue == Striker.Wizard) return 1f;
            if (advancedTactics && opponent.State is Core.LargeHero.GuardState heroGuard)
                return opponent.StateSeconds + future >= heroGuard.ObservationDuration ? 1f : .25f;
            return .25f;
        }

        void BuildSpellForecast(WarriorAiCandidate candidate, WarriorAiFrame frame, WarriorAiTuning tuning, float anticipationWeight) {
            forecast.Clear();
            var wizard = frame.OpponentWizard;
            if (wizard == null || anticipationWeight <= 0f) return;
            var enemy = frame.Opponent;
            var energy = wizard.Energy;
            if (enemy.State is Core.LargeWizard.ChargeState { IsTutorialActionCommitted: false }
                && enemy.StateSeconds + frame.ExecutionDelay >= wizard.ChargeSeconds) energy = Mathf.Min(3, energy + 1);
            var (spell, spawnTime, confidence) = enemy.State switch {
                Core.LargeWizard.Attack1State { IsTutorialActionCommitted: false } => (1, Mathf.Max(0f, wizard.FireReleaseSeconds - enemy.StateSeconds), 1f),
                Core.LargeWizard.Attack2State { IsTutorialActionCommitted: false } => (2, Mathf.Max(0f, wizard.IceReleaseSeconds - enemy.StateSeconds), 1f),
                Core.LargeWizard.Attack3State { IsTutorialActionCommitted: false } => (3, Mathf.Max(0f, wizard.RockReleaseSeconds - enemy.StateSeconds), 1f),
                Core.LargeWizard.Attack1State or Core.LargeWizard.Attack2State or Core.LargeWizard.Attack3State
                    or Core.LargeWizard.StunState or Core.LargeWizard.StunCancelState => (0, 0f, 0f),
                _ when enemy.Category is StrikerStateCategory.Special or StrikerStateCategory.Unknown => (0, 0f, 0f),
                _ when energy == 1 => (1, frame.ExecutionDelay + wizard.FireReleaseSeconds, .7f),
                _ when energy == 2 => (2, frame.ExecutionDelay + wizard.IceReleaseSeconds, .9f),
                _ when energy >= 3 => (3, frame.ExecutionDelay + wizard.RockReleaseSeconds, .8f),
                _ => (0, 0f, 0f)
            };
            if (spell == 0) return;
            var weight = confidence * anticipationWeight;
            // Targeted spells choose the defender's position at spawn, not at command input.
            var target = spawnTime < frame.ExecutionDelay
                ? ClampToArena(frame.Self.Body.center + frame.Self.Velocity * spawnTime, frame.Self)
                : PredictSelf(candidate, frame, tuning, spawnTime - frame.ExecutionDelay);
            target += frame.Self.Position - frame.Self.Body.center;
            if (spell == 2) {
                var ice = wizard.IcePrefab;
                target.y = frame.Self.Arena?.min.y ?? frame.Self.Body.min.y;
                forecast.Add(new WarriorAiThreat(ice.ObserveSpawnBounds(target), Vector3.zero, Vector3.zero,
                    spawnTime + ice.ObservationActivationDelay, spawnTime + ice.ObservationLifetime,
                    ice.ObservationDamage / 10f, true, true, Confidence: weight));
            } else if (spell == 3) {
                var rock = wizard.RockPrefab;
                target += Vector3.up * wizard.RockSpawnHeight;
                forecast.Add(new WarriorAiThreat(rock.ObserveSpawnBounds(target), Vector3.zero, Physics.gravity,
                    spawnTime, spawnTime + rock.ObservationLifetime, rock.ObservationDamage / 10f, true, Confidence: weight));
            } else {
                var fire = wizard.FirePrefab;
                var origin = wizard.FireOrigin + enemy.Velocity * Mathf.Min(spawnTime, .12f);
                forecast.Add(new WarriorAiThreat(new Bounds(origin, Vector3.one * fire.ObservationRadius * 2f),
                    enemy.Striker.LookDirection.CurrentValue.normalized * fire.ObservationSpeed, Vector3.zero,
                    spawnTime, spawnTime + wizard.FireLifetime, fire.ObservationDamage / 10f, true, Confidence: weight));
            }
        }

        void BuildPressureForecast(WarriorAiCandidate candidate, WarriorAiFrame frame, WarriorAiTuning tuning,
            WarriorAiMemory memory, float anticipationWeight) {
            hasMeleeForecast = hasRushForecast = false;
            if (anticipationWeight <= 0f) return;
            var enemy = frame.Opponent;
            // Hit-stun requires a recovery command. Committed specials are already sensed.
            if (enemy.Category is StrikerStateCategory.Special or StrikerStateCategory.Unknown
                || enemy.State is Core.LargeSatan.StunState or Core.LargeSatan.StunCancelState
                    or Core.LargeWizard.StunState or Core.LargeWizard.StunCancelState or Core.LargeHero.StunState) return;
            var nextMelee = enemy.State is Core.LargeSatan.IWarriorMeleeObservation current
                ? current.ObservationNextAttack ?? memory.MeleeOpener : memory.MeleeOpener;
            var hero = enemy.Hero;
            var wizard = frame.OpponentWizard;
            var firstCommand = enemy.State is Core.LargeHero.GuardState guard
                ? Mathf.Ceil(Mathf.Max(0f, guard.ObservationDuration - enemy.StateSeconds - frame.ExecutionDelay) / frame.BeatSeconds) : 0f;
            var guardDuration = candidate.Move == WarriorAiMove.Guard ? candidate.Duration : frame.BeatSeconds;
            var commands = Mathf.Clamp(Mathf.CeilToInt(guardDuration / frame.BeatSeconds) + 1, 2, 5);
            var launchIndex = -1;
            var nearestLaunch = float.PositiveInfinity;
            if (wizard != null) {
                for (var i = 0; i < frame.OpponentShields.Count; i++) {
                    var bounds = frame.OpponentShields[i];
                    var distance = (bounds.center - wizard.ShieldLaunchPosition).sqrMagnitude;
                    if (distance < nearestLaunch) { nearestLaunch = distance; launchIndex = i; }
                }
            }
            Vector3? pressureOrigin = null;
            var pressureFacing = enemy.Striker.LookDirection.CurrentValue.normalized;
            for (var beat = (int)firstCommand; beat < commands; beat++) {
                var commandAt = frame.ExecutionDelay + frame.BeatSeconds * beat;
                // Beyond the next decision our next input is unknown; discount continued exposure.
                var weight = anticipationWeight * (beat == 0 ? 1f : followUpWeight * Mathf.Pow(.6f, beat));
                if (weight <= 0f) continue;
                var origin = pressureOrigin ?? (enemy.Body.center + PredictOpponentMove(frame, commandAt, memory));
                var target = PredictSelf(candidate, frame, tuning, frame.BeatSeconds * beat);
                var toward = target.x >= origin.x ? Vector3.right : Vector3.left;
                if (hero != null && !hero.UsesAirInputs(enemy.State)
                    && enemy.State is not (Core.LargeHero.Slash1State or Core.LargeHero.Slash2State or Core.LargeHero.Slash3State)) {
                    var offset = hero.RushOrigin - enemy.Body.center;
                    offset.x *= toward.x * Mathf.Sign(enemy.Striker.LookDirection.CurrentValue.x);
                    var bounds = hero.RushBounds(origin + offset, toward);
                    var lifetime = hero.RushProjectileLifetimeSeconds ?? frame.BeatSeconds * 2f;
                    forecast.Add(new WarriorAiThreat(bounds, toward * hero.RushProjectileSpeed, Vector3.zero,
                        commandAt, commandAt + lifetime, hero.RushDamage / 10f, true,
                        Confidence: weight * Mathf.Lerp(.25f, .95f, memory.RushHabit)));
                    hasRushForecast = true;
                    pressureOrigin = ClampToArena(origin + toward * (hero.RushSpeed * Mathf.Min(frame.BeatSeconds, hero.RushSeconds)), enemy);
                    pressureFacing = toward;
                } else if (enemy.Striker.Striker.CurrentValue == Striker.Warrior && nextMelee != null
                    && (enemy.Grounded || enemy.State is Core.LargeSatan.IWarriorMeleeObservation)
                    && enemy.State is not Core.LargeSatan.ChargeState) {
                    // The combo link selects the finisher before it has an active hitbox.
                    var attack = nextMelee;
                    var hit = attack.ObservationHitWindow;
                    var decay = Mathf.Log(1f / Mathf.Clamp(attack.ObservationEndSpeedRatio, .0001f, 1f))
                        / Mathf.Max(.01f, attack.ObservationDuration);
                    var facing = attack is Core.LargeSatan.Attack2State ? pressureFacing : toward;
                    var bounds = attack.ObservationBounds;
                    var offset = bounds.center - enemy.Body.center;
                    offset.x *= facing.x * Mathf.Sign(enemy.Striker.LookDirection.CurrentValue.x);
                    var travel = decay > .0001f ? (1f - Mathf.Exp(-decay * hit.x)) / decay : hit.x;
                    bounds.center = origin + offset + facing * (attack.ObservationSpeed * travel);
                    forecast.Add(new WarriorAiThreat(bounds, facing * (attack.ObservationSpeed * Mathf.Exp(-decay * hit.x)), Vector3.zero,
                        commandAt + hit.x, commandAt + hit.y, attack.ObservationDamage / 10f, false,
                        Confidence: weight * Mathf.Lerp(.25f, .95f, memory.MeleeHabit), MotionDecay: decay, AnchoredAtStart: true));
                    hasMeleeForecast = true;
                    var step = decay > .0001f ? (1f - Mathf.Exp(-decay * frame.BeatSeconds)) / decay : frame.BeatSeconds;
                    pressureOrigin = ClampToArena(origin + facing * (attack.ObservationSpeed * step), enemy);
                    pressureFacing = facing;
                    nextMelee = attack.ObservationNextAttack ?? memory.MeleeOpener;
                } else if (wizard != null && launchIndex >= 0 && (beat == 0 || frame.OpponentShields.Count >= 3)) {
                    // Stacking three shields can replenish and launch on every beat, even with neutral input.
                    var bounds = frame.OpponentShields[launchIndex];
                    bounds.center += origin - enemy.Body.center;
                    forecast.Add(new WarriorAiThreat(bounds, enemy.Striker.LookDirection.CurrentValue.normalized * wizard.ShieldSpeed,
                        Vector3.zero, commandAt, commandAt + wizard.ShieldLifetime, Mathf.Max(.25f, wizard.ShieldDamage / 10f), true,
                        Confidence: weight * Mathf.Lerp(.2f, .95f, Mathf.Max(memory.ShieldLaunchHabit, memory.GuardHabit * .65f))));
                }
            }
        }

        float ThreatRisk(WarriorAiCandidate candidate, WarriorAiFrame frame, WarriorAiTuning tuning, float horizon, float projectileCaution) {
            var risk = 0f;
            for (var threatIndex = 0; threatIndex < frame.Threats.Count + forecast.Count; threatIndex++) {
                var threat = threatIndex < frame.Threats.Count ? frame.Threats[threatIndex] : forecast[threatIndex - frame.Threats.Count];
                if (ThreatHits(candidate, frame, tuning, threat, horizon))
                    risk += threat.Severity * threat.Confidence * (threat.Projectile ? projectileCaution : 1f);
                if (risk >= 3f) return 3f;
            }
            return risk;
        }

        bool ThreatHits(WarriorAiCandidate candidate, WarriorAiFrame frame, WarriorAiTuning tuning, WarriorAiThreat threat, float horizon, int samples = 8) {
            var start = Mathf.Max(frame.ExecutionDelay, threat.StartsAt);
            if (candidate.Move == WarriorAiMove.Guard) start = Mathf.Max(start, frame.ExecutionDelay + candidate.Duration);
            horizon += frame.BeatSeconds * followUpWeight * .5f;
            if (candidate.Move == WarriorAiMove.Guard) horizon = Mathf.Max(horizon, candidate.Duration + frame.BeatSeconds);
            if (threat.GroundHazard) horizon = Mathf.Max(horizon, frame.BeatSeconds * (1f + followUpWeight));
            var end = Mathf.Min(threat.EndsAt, frame.ExecutionDelay + horizon);
            if (start >= end) return false;
            var expanded = new Bounds(Vector3.zero, threat.Bounds.size + frame.Self.Body.size + Vector3.one * tuning.PredictionMargin * 2f);
            var previous = RelativeThreatPosition(candidate, frame, tuning, threat, start);
            if (expanded.Contains(previous)) return true;
            for (var i = 1; i <= samples; i++) {
                var current = RelativeThreatPosition(candidate, frame, tuning, threat, Mathf.Lerp(start, end, (float)i / samples));
                if (expanded.Contains(current) || SegmentIntersects(expanded, previous, current)) return true;
                previous = current;
            }
            return false;
        }

        static Vector3 RelativeThreatPosition(WarriorAiCandidate candidate, WarriorAiFrame frame, WarriorAiTuning tuning, WarriorAiThreat threat, float t) {
            var movementTime = threat.Projectile || threat.AnchoredAtStart ? Mathf.Max(0f, t - threat.StartsAt) : t;
            var travel = threat.MotionDecay > .0001f ? (1f - Mathf.Exp(-threat.MotionDecay * movementTime)) / threat.MotionDecay : movementTime;
            var target = threat.Bounds.center + threat.Velocity * travel + threat.Acceleration * (movementTime * movementTime * .5f);
            return PredictSelf(candidate, frame, tuning, Mathf.Max(0f, t - frame.ExecutionDelay)) - target;
        }

        static bool SegmentIntersects(Bounds bounds, Vector3 start, Vector3 end) {
            var delta = end - start;
            return delta.sqrMagnitude > .000001f && bounds.IntersectRay(new Ray(start, delta.normalized), out var distance) && distance <= delta.magnitude;
        }

        float AnticipationRisk(WarriorAiCandidate candidate, WarriorAiFrame frame, WarriorAiTuning tuning,
            WarriorAiMatchup matchup, WarriorAiMemory memory, Vector3 selfEnd, Vector3 enemyEnd, float horizon) {
            if (hasMeleeForecast || hasRushForecast) return 0f;
            if (candidate.Move == WarriorAiMove.Guard && horizon <= candidate.Duration) return 0f;
            var opponent = frame.Opponent;
            if (opponent.State is Core.LargeHero.StunState or Core.LargeWizard.StunState or Core.LargeWizard.StunCancelState
                or Core.LargeSatan.StunState or Core.LargeSatan.StunCancelState) return 0f;
            if (opponent.State is Core.DarkSatan.StunState stun
                && (!stun.ObservationAutoRecovers || opponent.StateSeconds + frame.ExecutionDelay + horizon < stun.ObservationDuration)) return 0f;
            if (opponent.State is Core.DarkSatan.StunCancelState cancel
                && opponent.StateSeconds + frame.ExecutionDelay + horizon < cancel.ObservationDuration) return 0f;
            var gap = Mathf.Abs(selfEnd.x - enemyEnd.x);
            var vertical = Mathf.Abs(selfEnd.y - enemyEnd.y);
            var range = matchup.OpponentReach;
            var aggression = .12f + (memory.AttackHabit * predictionWeight) * .3f;
            if (opponent.Category == StrikerStateCategory.Guard || opponent.Category == StrikerStateCategory.Unknown) aggression *= .3f;
            if (opponent.Category == StrikerStateCategory.Charge) {
                aggression *= opponent.Striker.Striker.CurrentValue == Striker.DemonKing ? 1.3f : .4f;
            }
            if (opponent.Category == StrikerStateCategory.Special) {
                aggression = 2f;
                if (opponent.Striker.Striker.CurrentValue == Striker.DemonKing) {
                    // The actual rush phase, direction and remaining hit window are sensed separately.
                    aggression = opponent.State is Core.DarkSatan.SpecialState ? .1f : 2f;
                    if (opponent.State is not Core.DarkSatan.SpecialState) range += 26f * Mathf.Min(horizon, .6f);
                } else if (opponent.Striker.Striker.CurrentValue == Striker.Hero) {
                    range += 5f;
                } else {
                    // Ranged specials are handled by the projectile sensor once released.
                    aggression = .25f;
                }
            }
            var inRange = Mathf.Clamp01((range + .8f - gap) / 1.5f) * Mathf.Clamp01((2.5f - vertical) / 1.3f);
            var risk = inRange * aggression;
            if (opponent.WarpBodyCenter is Vector3 warp) {
                var exposedAt = candidate.Move == WarriorAiMove.Guard ? candidate.Duration : 0f;
                var followUpEnds = Mathf.Min(horizon, tuning.AttackDuration);
                for (var t = exposedAt; t < followUpEnds; t += .05f) {
                    var separation = PredictSelf(candidate, frame, tuning, t) - warp;
                    var followUp = Mathf.Clamp01((matchup.OpponentReach - Mathf.Abs(separation.x)) / 1.5f)
                        * Mathf.Clamp01((2.5f - Mathf.Abs(separation.y)) / 1.3f);
                    risk = Mathf.Max(risk, followUp * (.55f + (memory.AttackHabit * predictionWeight) * .45f));
                }
            }
            return risk;
        }

        void BuildSpearForecast(WarriorAiFrame frame) {
            spearForecast.Clear();
            if (frame.Opponent.State is not Core.LargeSatan.ChargeState charge || charge.ObservedEmitState == null) return;

            var emit = charge.ObservedEmitState;
            var spear = charge.ObservedPoleArm;
            var look = frame.Opponent.Striker.LookDirection.CurrentValue;
            var aim = emit.ObserveThrowDirection(Vector2.zero, look);
            var target = frame.Self.Body.center + frame.Self.Velocity * frame.ExecutionDelay;
            var adjustedAim = emit.ObserveThrowDirection(SnapDirection(target - spear.transform.position), look);
            AddSpearPath(frame.ExecutionDelay, emit, spear, aim, 1.15f);
            if (Vector3.Dot(aim, adjustedAim) < .999f)
                AddSpearPath(frame.ExecutionDelay, emit, spear, adjustedAim, .75f);
        }

        void AddSpearPath(float commandTime, Core.LargeSatan.EmitState emit, PoleArm spear, Vector3 direction, float severity) {
            var origin = spear.transform.position;
            var hold = emit.ObservationHoldSeconds;
            var flight = emit.ObservationFlightSeconds;
            var distance = emit.ObserveTravelDistance(hold + flight);
            if (Physics.SphereCast(origin, spear.hitRadius, direction, out var wall, distance, spear.wallMask, QueryTriggerInteraction.Ignore))
                distance = wall.distance;

            var lastTime = hold;
            var lastDistance = 0f;
            var size = Vector3.one * (spear.hitRadius * 2f);
            for (var i = 1; i <= SpearSamples; i++) {
                var time = hold + flight * i / SpearSamples;
                var nextDistance = emit.ObserveTravelDistance(time);
                var speed = (nextDistance - lastDistance) / Mathf.Max(.001f, time - lastTime);
                var reachesWall = nextDistance >= distance;
                var end = reachesWall && speed > 0f ? lastTime + (distance - lastDistance) / speed : time;
                spearForecast.Add(new WarriorAiThreat(new Bounds(origin + direction * lastDistance, size), direction * speed, Vector3.zero,
                    commandTime + lastTime, commandTime + end, severity, true));
                if (reachesWall) break;
                lastTime = time;
                lastDistance = nextDistance;
            }
        }

        float SpearRisk(WarriorAiCandidate candidate, WarriorAiFrame frame, WarriorAiTuning tuning, float horizon) {
            var risk = 0f;
            // A single spear follows one aim, so overlapping forecasts contribute only once.
            foreach (var segment in spearForecast) {
                if (segment.Severity > risk && ThreatHits(candidate, frame, tuning, segment, horizon, 2))
                    risk = segment.Severity;
            }
            return risk;
        }

        static float BoundaryCost(WarriorAiCandidate candidate, WarriorAiFrame frame, WarriorAiTuning tuning, float horizon) {
            if (!frame.Self.Arena.HasValue) return 0f;
            var raw = PredictSelf(candidate, frame, tuning, horizon, false);
            var safe = ClampToArena(raw, frame.Self);
            var clipped = raw - safe;
            // Landing on the floor is intended. Horizontal or ceiling clipping wastes an escape.
            var cost = Mathf.Abs(clipped.x) * 4f + Mathf.Max(0f, clipped.y) * 3f;
            if (candidate.Move is WarriorAiMove.Dash or WarriorAiMove.Jump) {
                var arena = frame.Self.Arena.Value;
                var margin = Mathf.Min(safe.x - arena.min.x, arena.max.x - safe.x) - frame.Self.Body.extents.x;
                cost += Mathf.Max(0f, .7f - margin) * 3f;
            }
            return cost;
        }

        static Vector3 PredictSelf(WarriorAiCandidate candidate, WarriorAiFrame frame, WarriorAiTuning tuning, float seconds, bool clamp = true) {
            var origin = CandidateOrigin(candidate, frame);
            var move = Vector3.zero;
            switch (candidate.Move) {
                case WarriorAiMove.Guard:
                    move = candidate.MotionDirection * tuning.GuardShift;
                    if (seconds > candidate.Duration) move += Physics.gravity * (.5f * Mathf.Pow(seconds - candidate.Duration, 2));
                    break;
                case WarriorAiMove.Throw:
                    move = -candidate.MotionDirection * DecayingDistance(tuning.RecoilSpeed, Mathf.Max(0f, seconds - .1f), tuning.ThrowSeconds - .1f, .01f);
                    break;
                case WarriorAiMove.PrepareThrow:
                    move = Vector3.right * candidate.Direction.x * .5f * seconds;
                    break;
                case WarriorAiMove.Special:
                    move = Vector3.up * Mathf.Min(3f, seconds / .792f * 3f);
                    break;
                case WarriorAiMove.Wait:
                    move = frame.Self.Velocity * Mathf.Min(seconds, .15f);
                    break;
                default:
                    move = candidate.MotionDirection * DecayingDistance(candidate.Speed, seconds, candidate.Duration, candidate.EndSpeedRatio);
                    if (candidate.Move == WarriorAiMove.Jump) {
                        if (!frame.Self.Grounded) {
                            var active = Mathf.Min(seconds, candidate.Duration);
                            var after = Mathf.Max(0f, seconds - candidate.Duration);
                            var initial = candidate.MotionDirection * candidate.Speed;
                            move = initial * seconds + Vector3.down * (tuning.AirJumpAcceleration * (active * active * .5f + active * after))
                                + Physics.gravity * (after * after * .5f);
                        } else if (seconds > candidate.Duration) {
                            move += Physics.gravity * (.5f * Mathf.Pow(seconds - candidate.Duration, 2));
                        }
                    }
                    break;
            }
            var predicted = origin + move;
            return clamp ? ClampToArena(predicted, frame.Self) : predicted;
        }

        static Vector3 CandidateOrigin(WarriorAiCandidate candidate, WarriorAiFrame frame) {
            if (candidate.Move != WarriorAiMove.Wait && frame.Self.WarpBodyCenter is Vector3 warp) return warp;
            return frame.Self.Body.center + frame.Self.Velocity * frame.ExecutionDelay;
        }

        static Vector3 ClampToArena(Vector3 center, WarriorAiActor actor) {
            if (!actor.Arena.HasValue) return center;
            var arena = actor.Arena.Value;
            var extents = actor.Body.extents;
            return new Vector3(Mathf.Clamp(center.x, arena.min.x + extents.x, arena.max.x - extents.x),
                Mathf.Clamp(center.y, arena.min.y + extents.y, arena.max.y - extents.y), center.z);
        }

        Vector3 PredictOpponentMove(WarriorAiFrame frame, float seconds, WarriorAiMemory memory) {
            var opponent = frame.Opponent;
            seconds *= predictionWeight;
            if (advancedTactics && opponent.State is Core.LargeSatan.EmitState warpState && opponent.Striker is IStrikerContext context) {
                var destination = warpState.ObserveWarpPosition(context, Mathf.Min(seconds, frame.ExecutionDelay));
                if (destination.HasValue) return ReplyPosition(frame, ClampToArena(destination.Value + opponent.Body.center - opponent.Position, opponent), seconds, memory) - opponent.Body.center;
            }
            var velocity = opponent.Velocity;
            Vector3 move;
            if (opponent.State is Core.LargeHero.RushState or Core.LargeHero.SpecialApproachState) {
                move = velocity * Mathf.Min(seconds, .5f);
            } else if (opponent.Category is StrikerStateCategory.Attack or StrikerStateCategory.Dash) {
                move = velocity * (.2f * (1f - Mathf.Exp(-seconds / .2f)));
            } else if (opponent.Category is StrikerStateCategory.Guard or StrikerStateCategory.Charge or StrikerStateCategory.Special) {
                move = velocity * Mathf.Min(seconds, .1f);
            } else {
                move = Vector3.Lerp(velocity, memory.OpponentDrift, .4f) * Mathf.Min(seconds, .4f);
            }
            if (!opponent.Grounded && velocity.y < 0f) move.y += Physics.gravity.y * seconds * seconds * .5f;
            return ReplyPosition(frame, ClampToArena(opponent.Body.center + move, opponent), seconds, memory) - opponent.Body.center;
        }

        static float DecayingDistance(float speed, float seconds, float duration, float ratio) {
            seconds = Mathf.Min(seconds, Mathf.Max(duration, .01f));
            if (ratio >= .999f) return speed * seconds;
            var decay = Mathf.Log(1f / Mathf.Max(ratio, .0001f)) / Mathf.Max(duration, .01f);
            return speed * (1f - Mathf.Exp(-decay * seconds)) / decay;
        }

        static Vector2 SnapDirection(Vector3 direction) {
            if (direction.sqrMagnitude < .000001f) return Vector2.right;
            var angle = Mathf.Round(Mathf.Atan2(direction.y, direction.x) / (Mathf.PI * .25f)) * Mathf.PI * .25f;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        static GamePadButton? ButtonFor(WarriorAiMove move) => move switch {
            WarriorAiMove.Attack or WarriorAiMove.Throw => GamePadButton.East,
            WarriorAiMove.PrepareThrow => GamePadButton.South,
            WarriorAiMove.Dash or WarriorAiMove.Jump or WarriorAiMove.Dive => GamePadButton.West,
            WarriorAiMove.Guard => GamePadButton.North,
            WarriorAiMove.Special => GamePadButton.Start,
            _ => null
        };

        static WarriorAiChoice Idle(string reason) => new(AiAction.None, WarriorAiMove.Wait, 0f, reason);
    }
}
