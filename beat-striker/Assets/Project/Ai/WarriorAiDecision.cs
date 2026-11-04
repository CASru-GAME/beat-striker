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
        readonly List<WarriorAiCandidate> candidates = new(20);

        public WarriorAiChoice Choose(WarriorAiFrame frame, WarriorAiTuning tuning, WarriorAiMatchup matchup, WarriorAiMemory memory) {
            var self = frame.Self;
            if (self.Striker.HitPoint.CurrentValue <= 0f || frame.Opponent.Striker.HitPoint.CurrentValue <= 0f) {
                return Idle("Round ended");
            }
            if (self.Category == StrikerStateCategory.Special) return Idle("Special in progress");
            if (self.State is Core.LargeSatan.StunCancelState) return Idle("Stun recovery in progress");
            if (self.State is Core.LargeSatan.StunState) {
                return new WarriorAiChoice(new AiAction(Vector2.zero, GamePadButton.North), WarriorAiMove.Recover, 0f, "Break hit stun");
            }

            BuildCandidates(frame, tuning, memory);
            WarriorAiChoice best = null;
            foreach (var candidate in candidates) {
                var score = Score(candidate, frame, tuning, matchup, memory);
                if (best != null && score <= best.Score) continue;
                best = new WarriorAiChoice(new AiAction(candidate.Direction, ButtonFor(candidate.Move)), candidate.Move, score, candidate.Reason);
            }
            return best ?? Idle("Observe");
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
                    Add(WarriorAiMove.Attack, Vector2.down, Vector3.down, tuning.DiveAttackSpeed, tuning.AirAttackDuration, 1f, 15f, "Dive attack from above");
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
            Add(WarriorAiMove.Guard, toward, toward, 0f, tuning.GuardSeconds, 1f, 0f, "Dodge into punish range");
            Add(WarriorAiMove.Guard, -toward, -toward, 0f, tuning.GuardSeconds, 1f, 0f, "Dodge incoming threat");
            Add(WarriorAiMove.Guard, Vector2.zero, Vector3.zero, 0f, tuning.GuardSeconds, 1f, 0f, "Dodge without shifting");

            var maxSpecial = self.Striker.MaxSpecialPoint.CurrentValue;
            if (maxSpecial > 0f && self.Striker.SpecialPoint.CurrentValue >= maxSpecial - .001f) {
                Add(WarriorAiMove.Special, Vector2.zero, Vector3.zero, 0f, 1f, 1f, 0f, "Gate of Arrow");
            }
            Add(WarriorAiMove.Wait, Vector2.zero, Vector3.zero, 0f, frame.BeatSeconds, 1f, 0f, "Allow committed move to finish");
        }

        void Add(WarriorAiMove move, Vector2 input, Vector3 motion, float speed, float duration, float ratio, float damage, string reason) {
            candidates.Add(new WarriorAiCandidate(move, input, motion, speed, duration, ratio, damage, reason));
        }

        float Score(WarriorAiCandidate candidate, WarriorAiFrame frame, WarriorAiTuning tuning, WarriorAiMatchup matchup, WarriorAiMemory memory) {
            var horizon = Mathf.Clamp(frame.BeatSeconds * 1.2f, .3f, 1.1f);
            var end = PredictSelf(candidate, frame, tuning, horizon);
            var enemyEnd = frame.Opponent.Body.center + PredictOpponentMove(frame, horizon + frame.ExecutionDelay, memory);
            var startGap = Mathf.Abs(frame.Opponent.Body.center.x - frame.Self.Body.center.x);
            var endGap = Mathf.Abs(enemyEnd.x - end.x);
            var positionGain = Mathf.Abs(startGap - matchup.PreferredDistance) - Mathf.Abs(endGap - matchup.PreferredDistance);
            var score = Mathf.Clamp(positionGain, -6f, 6f) * tuning.PositionWeight * matchup.Pressure;
            var risk = ThreatRisk(candidate, frame, tuning, horizon, matchup.ProjectileCaution);
            risk += AnticipationRisk(candidate, frame, tuning, matchup, memory, end, enemyEnd, horizon);
            var hpRatio = frame.Self.Striker.HitPoint.CurrentValue / Mathf.Max(1f, frame.Self.Striker.MaxHitPoint.CurrentValue);
            score -= risk * tuning.RiskWeight * (hpRatio < .3f ? 1.3f : 1f);
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
                case WarriorAiMove.PrepareThrow:
                    score += startGap >= tuning.ThrowMinimumDistance && startGap <= tuning.ThrowMaximumDistance ? 11f : -10f;
                    score += memory.GuardHabit * 2f;
                    if (Mathf.Abs(enemyEnd.y - end.y) > 6f) score -= 5f;
                    if (frame.Self.State is Core.LargeSatan.EmitState) score -= 3f;
                    break;
                case WarriorAiMove.Dash:
                    score += endGap < tuning.AttackReach + 2f ? 3f : 0f;
                    score += Mathf.Min(memory.StaleBeats, 8) * .2f;
                    if (startGap < matchup.PreferredDistance && Vector3.Dot(candidate.MotionDirection, enemyEnd - end) > 0f) score -= 4f;
                    break;
                case WarriorAiMove.Jump:
                    score += matchup.JumpPreference;
                    if (endGap < startGap) score += memory.GuardHabit * 4f;
                    score -= Mathf.Max(0f, end.y - enemyEnd.y - 2f) * 1.4f;
                    if (!frame.Self.Grounded && frame.Self.Body.center.y >= frame.Opponent.Body.center.y + 2f) score -= 6f;
                    break;
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
            return score;
        }

        static float AttackChance(WarriorAiCandidate candidate, WarriorAiFrame frame, WarriorAiTuning tuning, WarriorAiMemory memory) {
            var best = 0f;
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
                chance *= GuardAvailability(frame.Opponent, t + frame.ExecutionDelay, tuning);
                if (frame.Opponent.Category == StrikerStateCategory.Special) chance *= .1f;
                chance *= 1f - Mathf.Min(.45f, memory.WhiffPressure * .08f + memory.GuardHabit * .2f);
                best = Mathf.Max(best, chance);
            }
            return best;
        }

        static float ThrowChance(WarriorAiCandidate candidate, WarriorAiFrame frame, WarriorAiTuning tuning, WarriorAiMemory memory) {
            if (frame.Self.State is Core.LargeSatan.ChargeState charge && charge.ObservedEmitState != null) {
                var emit = charge.ObservedEmitState;
                var spearOrigin = charge.ObservedPoleArm.transform.position;
                var best = 0f;
                for (var i = 0; i <= 16; i++) {
                    var t = emit.ObservationHoldSeconds + emit.ObservationFlightSeconds * i / 16f;
                    var target = frame.Opponent.Body.center + PredictOpponentMove(frame, t + frame.ExecutionDelay, memory);
                    var delta = target - spearOrigin;
                    var along = Vector3.Dot(delta, candidate.MotionDirection.normalized);
                    var perpendicular = (delta - candidate.MotionDirection.normalized * along).magnitude;
                    var tolerance = Mathf.Max(frame.Opponent.Body.extents.x, .5f) + .5f;
                    var chance = Mathf.Clamp01((tolerance + .4f - perpendicular) / .8f)
                        * Mathf.Clamp01((tolerance + .4f - Mathf.Abs(along - emit.ObserveTravelDistance(t))) / .8f);
                    if (along <= 0f) chance = 0f;
                    best = Mathf.Max(best, chance * GuardAvailability(frame.Opponent, t + frame.ExecutionDelay, tuning));
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
            return estimatedAlong <= 0f ? 0f : Mathf.Clamp01((estimatedTolerance + .4f - miss) / .8f)
                * GuardAvailability(frame.Opponent, hitTime + frame.ExecutionDelay, tuning);
        }

        static float GuardAvailability(WarriorAiActor opponent, float future, WarriorAiTuning tuning) {
            if (opponent.Category != StrikerStateCategory.Guard) return 1f;
            if (opponent.Striker.Striker.CurrentValue is Striker.Warrior or Striker.DemonKing) {
                return future + opponent.StateSeconds < tuning.GuardSeconds ? .02f : .9f;
            }
            // Hero and wizard shields can persist beyond the guarding animation.
            return .25f;
        }

        static float ThreatRisk(WarriorAiCandidate candidate, WarriorAiFrame frame, WarriorAiTuning tuning, float horizon, float projectileCaution) {
            var risk = 0f;
            foreach (var threat in frame.Threats) {
                var start = Mathf.Max(frame.ExecutionDelay, threat.StartsAt);
                if (candidate.Move == WarriorAiMove.Guard) start = Mathf.Max(start, frame.ExecutionDelay + tuning.GuardSeconds);
                var end = Mathf.Min(threat.EndsAt, frame.ExecutionDelay + horizon);
                if (start >= end) continue;
                var expanded = new Bounds(Vector3.zero, threat.Bounds.size + frame.Self.Body.size + Vector3.one * tuning.PredictionMargin * 2f);
                var hit = false;
                for (int i = 0; i < 8; i++) {
                    var t0 = Mathf.Lerp(start, end, i / 8f);
                    var t1 = Mathf.Lerp(start, end, (i + 1) / 8f);
                    var p0 = RelativeThreatPosition(candidate, frame, tuning, threat, t0);
                    var p1 = RelativeThreatPosition(candidate, frame, tuning, threat, t1);
                    if (expanded.Contains(p0) || expanded.Contains(p1) || SegmentIntersects(expanded, p0, p1)) {
                        hit = true;
                        break;
                    }
                }
                if (hit) risk += threat.Severity * (threat.Projectile ? projectileCaution : 1f);
                if (risk >= 3f) return 3f;
            }
            return risk;
        }

        static Vector3 RelativeThreatPosition(WarriorAiCandidate candidate, WarriorAiFrame frame, WarriorAiTuning tuning, WarriorAiThreat threat, float t) {
            var movementTime = threat.Projectile ? Mathf.Max(0f, t - threat.StartsAt) : t;
            var target = threat.Bounds.center + threat.Velocity * movementTime + threat.Acceleration * (movementTime * movementTime * .5f);
            return PredictSelf(candidate, frame, tuning, Mathf.Max(0f, t - frame.ExecutionDelay)) - target;
        }

        static bool SegmentIntersects(Bounds bounds, Vector3 start, Vector3 end) {
            var delta = end - start;
            return delta.sqrMagnitude > .000001f && bounds.IntersectRay(new Ray(start, delta.normalized), out var distance) && distance <= delta.magnitude;
        }

        static float AnticipationRisk(WarriorAiCandidate candidate, WarriorAiFrame frame, WarriorAiTuning tuning,
            WarriorAiMatchup matchup, WarriorAiMemory memory, Vector3 selfEnd, Vector3 enemyEnd, float horizon) {
            if (candidate.Move == WarriorAiMove.Guard && horizon <= tuning.GuardSeconds) return 0f;
            var opponent = frame.Opponent;
            var gap = Mathf.Abs(selfEnd.x - enemyEnd.x);
            var vertical = Mathf.Abs(selfEnd.y - enemyEnd.y);
            var range = matchup.OpponentReach;
            var aggression = .12f + memory.AttackHabit * .3f;
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
            if (candidate.Move == WarriorAiMove.PrepareThrow && opponent.Striker.Striker.CurrentValue == Striker.Wizard) {
                // Ice and falling rocks target the current location even from across the arena.
                risk += memory.ChargeHabit * .15f;
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
                    if (seconds > tuning.GuardSeconds) move += Physics.gravity * (.5f * Mathf.Pow(seconds - tuning.GuardSeconds, 2));
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
            if (candidate.Move != WarriorAiMove.Wait && frame.Self.State is Core.LargeSatan.EmitState emit) {
                // Any new command exits EmitState and warps to the current spear position.
                return frame.Self.Body.center + emit.ObservedPoleArm.transform.position - frame.Self.Position;
            }
            return frame.Self.Body.center + frame.Self.Velocity * frame.ExecutionDelay;
        }

        static Vector3 ClampToArena(Vector3 center, WarriorAiActor actor) {
            if (!actor.Arena.HasValue) return center;
            var arena = actor.Arena.Value;
            var extents = actor.Body.extents;
            return new Vector3(Mathf.Clamp(center.x, arena.min.x + extents.x, arena.max.x - extents.x),
                Mathf.Clamp(center.y, arena.min.y + extents.y, arena.max.y - extents.y), center.z);
        }

        static Vector3 PredictOpponentMove(WarriorAiFrame frame, float seconds, WarriorAiMemory memory) {
            var opponent = frame.Opponent;
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
            return ClampToArena(opponent.Body.center + move, opponent) - opponent.Body.center;
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
