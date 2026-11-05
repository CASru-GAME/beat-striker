using System.Collections.Generic;
using UnityEngine;

namespace Alice.DemonKingAi {
    internal enum DemonKingAiMove { Wait, Walk, Melee, Uppercut, HeelDrop, AirAttack, Charge, Launch, Move, Dive, Guard, Special, Recover }
    internal sealed record DemonKingAiChoice(AiAction Action, DemonKingAiMove Move, float Score, string Reason);
    internal sealed record DemonKingAiCandidate(DemonKingAiMove Move, Vector2 Direction, string Reason);

    internal sealed class DemonKingAiDecision {
        readonly List<DemonKingAiCandidate> candidates = new(20);
        readonly HashSet<int> consumedShields = new();

        public DemonKingAiChoice Choose(DemonKingAiFrame frame, DemonKingAiTuning tuning, DemonKingAiMatchup matchup, DemonKingAiMemory memory, AiDifficulty difficulty) {
            var self = frame.Self;
            if (self.Striker.HitPoint.CurrentValue <= 0f || frame.Opponent.Striker.HitPoint.CurrentValue <= 0f) return Idle("Round ended");
            if (self.Category == StrikerStateCategory.Special) return Idle("Complete the Overlord rush");
            if (self.State is Core.DarkSatan.StunState stun && !stun.ObservationAutoRecovers)
                return new(new AiAction(Vector2.zero, GamePadButton.North), DemonKingAiMove.Recover, 0f, "Break hit stun");
            if (self.State is Core.DarkSatan.DemonDirectionalAttackState committed
                && self.StateSeconds + frame.ExecutionDelay < committed.ObservationDuration) return Idle("Finish the committed directional strike");

            BuildCandidates(frame);
            var (selected, score) = difficulty.SelectCandidate(
                candidates,
                static candidate => candidate.Move != DemonKingAiMove.Wait,
                static candidate => ButtonFor(candidate.Move),
                (candidate, anticipationWeight) => Score(candidate, frame, tuning, matchup, memory, anticipationWeight));
            if (selected == null) return Idle("Observe");
            return new(new AiAction(selected.Direction, ButtonFor(selected.Move)), selected.Move, score, selected.Reason);
        }

        void BuildCandidates(DemonKingAiFrame frame) {
            candidates.Clear();
            var source = frame.Self.Demon;
            var toward = frame.Opponent.Position.x >= frame.Self.Position.x ? Vector2.right : Vector2.left;
            // NormalGroup routes every A input to Launch while any banked volley remains.
            if (source.HasVolley && frame.Self.State is not Core.DarkSatan.AirAttackState) Add(DemonKingAiMove.Launch, Vector2.zero, "Release the banked notes along their original aim");
            else if (frame.Self.State is not Core.DarkSatan.AirAttackState
                && !(frame.Self.State is Core.DarkSatan.DemonChargeAttackState && source.VolleyFired)) {
                Add(frame.Self.Grounded || frame.Self.State is Core.DarkSatan.DemonMeleeAttackState ? DemonKingAiMove.Melee : DemonKingAiMove.AirAttack,
                    Vector2.zero, "Punish with the close-range combo");
                Add(DemonKingAiMove.Uppercut, Vector2.up, "Intercept above the shield line");
                Add(DemonKingAiMove.HeelDrop, Vector2.down, "Strike down through the opening");
            }
            Add(DemonKingAiMove.Charge, Vector2.zero, "Summon converging notes for a later volley");
            Add(DemonKingAiMove.Guard, Vector2.zero, "Dodge the incoming attack");
            Add(DemonKingAiMove.Guard, toward, "Dodge forward into punish range");
            Add(DemonKingAiMove.Guard, -toward, "Dodge back out of pressure");
            Add(DemonKingAiMove.Move, toward, "Close the distance");
            Add(DemonKingAiMove.Move, -toward, "Escape the attack lane");
            Add(DemonKingAiMove.Move, (toward + Vector2.up).normalized, "Approach above shields and spear");
            Add(DemonKingAiMove.Move, (-toward + Vector2.up).normalized, "Evade the spear and its follow-up");
            Add(DemonKingAiMove.Move, Vector2.up, "Match the opponent's altitude");
            if (!frame.Self.Grounded) {
                Add(DemonKingAiMove.Move, (toward + Vector2.down * .35f).normalized, "Pursue a lower airborne opponent");
                Add(DemonKingAiMove.Dive, Vector2.down, "Land outside the attack lane");
            } else {
                Add(DemonKingAiMove.Walk, toward, "Adjust spacing");
                Add(DemonKingAiMove.Walk, -toward, "Bait the opponent's approach");
            }
            var max = frame.Self.Striker.MaxSpecialPoint.CurrentValue;
            if (max > 0f && frame.Self.Striker.SpecialPoint.CurrentValue >= max - .001f)
                Add(DemonKingAiMove.Special, Vector2.zero, "Overlord rush through an open lane");
            Add(DemonKingAiMove.Wait, Vector2.zero, "Finish the current action");
        }

        void Add(DemonKingAiMove move, Vector2 direction, string reason) => candidates.Add(new(move, direction, reason));

        float Score(DemonKingAiCandidate candidate, DemonKingAiFrame frame, DemonKingAiTuning tuning, DemonKingAiMatchup matchup,
            DemonKingAiMemory memory, float anticipationWeight) {
            var source = frame.Self.Demon;
            var horizon = Mathf.Clamp(frame.BeatSeconds, .3f, 1.2f);
            var end = PredictSelf(candidate, frame, tuning, horizon);
            var enemy = PredictOpponent(frame, horizon + frame.ExecutionDelay, memory);
            var gap = Mathf.Abs(enemy.x - end.x);
            var startGap = Mathf.Abs(frame.Opponent.Body.center.x - frame.Self.Body.center.x);
            var altitude = Mathf.Abs(frame.Opponent.Body.center.y - frame.Self.Body.center.y);
            var gain = Mathf.Abs(startGap - matchup.PreferredDistance) - Mathf.Abs(gap - matchup.PreferredDistance);
            var score = Mathf.Clamp(gain, -6f, 6f) * tuning.PositionWeight * matchup.Pressure * (altitude > 4f ? .2f : 1f);
            score += Mathf.Clamp(altitude - Mathf.Abs(enemy.y - end.y), -4f, 4f) * (altitude > 4f ? 7f : 2f);
            var risk = ThreatRisk(candidate, frame, tuning, horizon, matchup.ProjectileCaution)
                + AnticipationRisk(candidate, frame, tuning, matchup, memory, horizon) * anticipationWeight;
            var hp = frame.Self.Striker.HitPoint.CurrentValue / Mathf.Max(1f, frame.Self.Striker.MaxHitPoint.CurrentValue);
            score -= risk * tuning.RiskWeight * (hp < .3f ? 1.3f : 1f);
            var raw = PredictSelf(candidate, frame, tuning, horizon, false);
            score -= Vector3.Distance(raw, end) * 4f;

            if (frame.Self.State is Core.DarkSatan.DemonChargeAttackState) {
                var pending = source.VolleyFired ? source.LaunchSeconds : source.SpreadSeconds;
                if (frame.Self.StateSeconds + frame.ExecutionDelay < pending + .02f) {
                    if (candidate.Move == DemonKingAiMove.Wait) score += 16f;
                    else if (candidate.Move != DemonKingAiMove.Launch) score -= 20f;
                }
            }
            switch (candidate.Move) {
                case DemonKingAiMove.Melee:
                case DemonKingAiMove.Uppercut:
                case DemonKingAiMove.HeelDrop:
                case DemonKingAiMove.AirAttack: {
                    var chance = MeleeChance(candidate, frame, tuning, memory);
                    var damage = Damage(candidate.Move, source, tuning);
                    score += chance * Mathf.Min(damage, frame.Opponent.Striker.HitPoint.CurrentValue) * tuning.DamageWeight * matchup.Pressure;
                    score -= (1f - chance) * (8f + memory.WhiffPressure * 2f);
                    if (chance > .7f && damage >= frame.Opponent.Striker.HitPoint.CurrentValue) score += 18f;
                    // Contact can remove one disposable shield and create a route for the next strike.
                    if (frame.OpponentShields.Count > 0 && startGap < 4.5f) score += 5f * memory.ShieldHabit;
                    break;
                }
                case DemonKingAiMove.Launch: {
                    var expected = VolleyDamage(frame, memory);
                    score += expected * tuning.DamageWeight;
                    score -= expected < source.BulletDamage * .4f ? 12f : 0f;
                    score += Mathf.Min(8f, source.VolleyCount * 2f);
                    // Release an obsolete aim instead of indefinitely hoarding it.
                    if (memory.StaleBeats > 5 && source.VolleyCount >= tuning.MaximumBankedVolleys) score += 15f;
                    break;
                }
                case DemonKingAiMove.Charge: {
                    if (source.VolleyCount >= tuning.MaximumBankedVolleys) return float.NegativeInfinity;
                    var steady = Mathf.Clamp01(1f - frame.Opponent.Velocity.magnitude / 28f);
                    score += 8f + steady * 6f + Mathf.Min(startGap, 12f) * .3f;
                    score -= source.VolleyCount * 4f;
                    if (frame.OpponentShields.Count > 0) score += memory.ShieldHabit * 4f;
                    if (source.HasVolley && VolleyDamage(frame, memory) > source.BulletDamage * 2f) score -= 12f;
                    break;
                }
                case DemonKingAiMove.Guard:
                    score -= 3f + Mathf.Min(memory.GuardStreak, 6) * 2f + Mathf.Min(memory.StaleBeats, 14) * .5f;
                    break;
                case DemonKingAiMove.Special: {
                    var chance = SpecialChance(frame, memory);
                    if (chance < .15f) return float.NegativeInfinity;
                    score += tuning.SpecialScore * chance - (1f - chance) * 35f;
                    break;
                }
                case DemonKingAiMove.Move:
                    score -= 4f;
                    if (end.y > enemy.y + 2f) score -= end.y - enemy.y - 2f;
                    if (memory.ShieldHabit > .3f && candidate.Direction.y > .5f && gap < startGap) score += 5f;
                    break;
                case DemonKingAiMove.Dive:
                    score -= 4f;
                    break;
                case DemonKingAiMove.Walk:
                    score -= 5f;
                    break;
                case DemonKingAiMove.Wait:
                    score -= 6f + Mathf.Min(memory.StaleBeats, 8);
                    break;
            }
            return score;
        }

        static float Damage(DemonKingAiMove move, Core.DarkSatan.DemonKingCombatObservation source, DemonKingAiTuning tuning) => move switch {
            DemonKingAiMove.Uppercut => source.Uppercut.ObservationDamage,
            DemonKingAiMove.HeelDrop => source.HeelDrop.ObservationDamage,
            DemonKingAiMove.AirAttack => tuning.AirAttackDamage,
            _ => source.NextStrike.Damage
        };

        static float MeleeChance(DemonKingAiCandidate candidate, DemonKingAiFrame frame, DemonKingAiTuning tuning, DemonKingAiMemory memory) {
            var source = frame.Self.Demon;
            var technique = candidate.Move == DemonKingAiMove.Uppercut ? source.Uppercut : source.HeelDrop;
            var directional = candidate.Move is DemonKingAiMove.Uppercut or DemonKingAiMove.HeelDrop;
            var start = directional ? technique.ObservationHitStart : candidate.Move == DemonKingAiMove.AirAttack ? .05f : source.NextStrike.HitStart;
            var end = directional ? technique.ObservationHitEnd : candidate.Move == DemonKingAiMove.AirAttack ? .3f : source.NextStrike.HitEnd;
            var buffer = AttackBufferSeconds(candidate, frame);
            start += buffer;
            end += buffer;
            var chance = 0f;
            var facing = frame.Opponent.Position.x >= frame.Self.Position.x ? 1f : -1f;
            for (var i = 0; i <= 8; i++) {
                var t = Mathf.Lerp(start, end, i / 8f);
                var self = PredictSelf(candidate, frame, tuning, t);
                var opponent = PredictOpponent(frame, t + frame.ExecutionDelay, memory);
                var horizontal = Mathf.Abs(opponent.x - self.x);
                var vertical = Mathf.Abs(opponent.y - self.y);
                var reach = tuning.MeleeReach + frame.Opponent.Body.extents.x;
                var hit = Mathf.Clamp01((reach - horizontal) / .8f) * Mathf.Clamp01((1.5f + frame.Opponent.Body.extents.y - vertical) / .9f);
                if ((opponent.x - self.x) * facing < -.5f) hit *= .2f;
                hit *= ShieldTransmission(self, opponent, .4f, frame) * Vulnerability(frame.Opponent, t + frame.ExecutionDelay);
                chance = Mathf.Max(chance, hit);
            }
            return chance * (1f - memory.WhiffPressure * .04f);
        }

        float VolleyDamage(DemonKingAiFrame frame, DemonKingAiMemory memory) {
            consumedShields.Clear();
            var damage = 0f;
            foreach (var projectile in frame.ReadyProjectiles) {
                if (projectile.Velocity.sqrMagnitude < .001f) continue;
                var arrival = Vector3.Distance(projectile.Position, frame.Opponent.Body.center) / projectile.Velocity.magnitude;
                if (arrival > projectile.LifetimeSeconds) continue;
                var target = PredictOpponent(frame, arrival + frame.ExecutionDelay, memory);
                var body = frame.Opponent.Body;
                body.center = target;
                body.Expand(projectile.Radius * 2f);
                var ray = new Ray(projectile.Position, projectile.Velocity.normalized);
                if (!body.IntersectRay(ray, out var bodyDistance) || bodyDistance > projectile.Velocity.magnitude * projectile.LifetimeSeconds) continue;
                var blocked = -1;
                var nearest = bodyDistance;
                for (var i = 0; i < frame.OpponentShields.Count; i++) {
                    if (consumedShields.Contains(i)) continue;
                    var shield = frame.OpponentShields[i].Bounds;
                    shield.Expand(projectile.Radius * 2f);
                    if (shield.IntersectRay(ray, out var shieldDistance) && shieldDistance < nearest) { blocked = i; nearest = shieldDistance; }
                }
                if (blocked >= 0) { consumedShields.Add(blocked); damage += frame.Self.Demon.BulletDamage * .15f; }
                else damage += frame.Self.Demon.BulletDamage * Vulnerability(frame.Opponent, arrival + frame.ExecutionDelay);
            }
            return Mathf.Min(damage, frame.Opponent.Striker.HitPoint.CurrentValue + 5f);
        }

        static float SpecialChance(DemonKingAiFrame frame, DemonKingAiMemory memory) {
            if (frame.Opponent.Category == StrikerStateCategory.Special) return 0f;
            var source = frame.Self.Demon;
            var gap = Mathf.Abs(frame.Opponent.Position.x - frame.Self.Position.x);
            var at = frame.ExecutionDelay + gap / Mathf.Max(1f, source.SpecialSpeed) + .15f;
            var target = PredictOpponent(frame, at, memory);
            var origin = new Vector3(frame.Self.Position.x, (frame.Self.Arena?.min.y ?? frame.Self.Position.y) + source.SpecialHeight, frame.Self.Position.z);
            if (Mathf.Abs(target.x - origin.x) > source.SpecialReach) return 0f;
            return Mathf.Clamp01((source.SpecialRadius + frame.Opponent.Body.extents.y - Mathf.Abs(target.y - origin.y)) / 1.2f)
                * ShieldTransmission(origin, target, source.SpecialRadius, frame) * Vulnerability(frame.Opponent, at);
        }

        static float ShieldTransmission(Vector3 origin, Vector3 target, float radius, DemonKingAiFrame frame) {
            var delta = target - origin;
            if (delta.sqrMagnitude < .000001f) return frame.OpponentShields.Count > 0 ? .35f : 1f;
            var body = frame.Opponent.Body;
            body.center = target;
            body.Expand(radius * 2f);
            var ray = new Ray(origin, delta.normalized);
            body.IntersectRay(ray, out var bodyDistance);
            foreach (var shield in frame.OpponentShields) {
                var bounds = shield.Bounds;
                bounds.Expand(radius * 2f);
                if (bounds.IntersectRay(ray, out var shieldDistance) && shieldDistance < bodyDistance - .05f) return .08f;
            }
            return 1f;
        }

        static float Vulnerability(DemonKingAiActor opponent, float future) {
            if (opponent.Category == StrikerStateCategory.Guard && opponent.Striker.Striker.CurrentValue is Striker.Warrior or Striker.DemonKing) {
                var seconds = (opponent.Striker as IStrikerCombatObservationSource)?.ActiveGuardSeconds ?? 1f;
                return opponent.StateSeconds + future < seconds ? .03f : 1f;
            }
            return opponent.Category == StrikerStateCategory.Special ? .15f : 1f;
        }

        static float ThreatRisk(DemonKingAiCandidate candidate, DemonKingAiFrame frame, DemonKingAiTuning tuning, float horizon, float caution) {
            var risk = 0f;
            foreach (var threat in frame.Threats) {
                var start = Mathf.Max(frame.ExecutionDelay, threat.StartsAt);
                if (candidate.Move == DemonKingAiMove.Guard) start = Mathf.Max(start, frame.ExecutionDelay + frame.Self.NextGuardSeconds);
                var end = Mathf.Min(threat.EndsAt, frame.ExecutionDelay + horizon);
                if (start >= end) continue;
                var body = new Bounds(Vector3.zero, threat.Bounds.size + frame.Self.Body.size + Vector3.one * tuning.PredictionMargin * 2f);
                for (var i = 0; i < 12; i++) {
                    var a = Mathf.Lerp(start, end, i / 12f);
                    var b = Mathf.Lerp(start, end, (i + 1) / 12f);
                    var p0 = PredictSelf(candidate, frame, tuning, a - frame.ExecutionDelay) - ThreatPosition(threat, a);
                    var p1 = PredictSelf(candidate, frame, tuning, b - frame.ExecutionDelay) - ThreatPosition(threat, b);
                    if (!SegmentIntersects(body, p0, p1)) continue;
                    risk += threat.Severity * (threat.Projectile ? caution : 1f);
                    break;
                }
            }
            return Mathf.Min(4f, risk);
        }

        static Vector3 ThreatPosition(DemonKingAiThreat threat, float seconds) {
            var moving = threat.Projectile ? Mathf.Max(0f, seconds - threat.StartsAt) : seconds;
            return threat.Bounds.center + threat.Velocity * moving + threat.Acceleration * (.5f * moving * moving);
        }

        static float AnticipationRisk(DemonKingAiCandidate candidate, DemonKingAiFrame frame, DemonKingAiTuning tuning, DemonKingAiMatchup matchup, DemonKingAiMemory memory, float horizon) {
            var enemy = frame.Opponent;
            if (enemy.State is Core.LargeHero.StunState or Core.LargeWizard.StunState or Core.LargeWizard.StunCancelState
                or Core.LargeSatan.StunState or Core.LargeSatan.StunCancelState) return 0f;
            if (enemy.State is Core.DarkSatan.StunState stun && !stun.ObservationAutoRecovers) return 0f;
            var risk = 0f;
            if (enemy.Wizard != null && enemy.Wizard.Energy == 2) {
                var release = enemy.Wizard.IceReleaseSeconds;
                var at = PredictSelf(candidate, frame, tuning, release);
                var grown = PredictSelf(candidate, frame, tuning, release + tuning.IceGrowthSeconds);
                var floor = frame.Self.Arena?.min.y ?? frame.Self.Body.min.y;
                if (candidate.Move != DemonKingAiMove.Guard || release + tuning.IceGrowthSeconds >= frame.Self.NextGuardSeconds)
                    risk += Mathf.Clamp01((tuning.IceHalfWidth + frame.Self.Body.extents.x + tuning.PredictionMargin - Mathf.Abs(grown.x - at.x)) / .7f)
                        * Mathf.Clamp01((tuning.IceHeight + tuning.PredictionMargin - (grown.y - frame.Self.Body.extents.y - floor)) / .8f) * 1.3f;
            }
            for (var i = 1; i <= 8; i++) {
                var t = horizon * i / 8f;
                if (candidate.Move == DemonKingAiMove.Guard && t < frame.Self.NextGuardSeconds) continue;
                var self = PredictSelf(candidate, frame, tuning, t);
                var opponent = PredictOpponent(frame, t + frame.ExecutionDelay, memory);
                var reach = matchup.OpponentReach;
                var aggression = .1f + memory.AttackHabit * .45f;
                if (enemy.Striker.Striker.CurrentValue == Striker.Warrior) {
                    reach = tuning.WarriorLungeReach;
                    aggression += memory.AbHabit * .5f;
                    if (enemy.State is Core.LargeSatan.EmitState) { reach = tuning.WarriorWarpReach; aggression = .7f + memory.AbHabit * .7f; }
                    if (enemy.State is Core.LargeSatan.ChargeState charge && charge.ObservedEmitState != null) {
                        var direction = charge.ObservedEmitState.ObserveThrowDirection(Vector2.zero, enemy.Striker.LookDirection.CurrentValue);
                        var origin = charge.ObservedPoleArm.transform.position;
                        var spear = origin + direction * charge.ObservedEmitState.ObserveTravelDistance(t);
                        var lane = new Bounds(Vector3.zero, frame.Self.Body.size + Vector3.one * .8f);
                        if (SegmentIntersects(lane, origin - self, spear - self)) risk += (.9f + memory.AbHabit * .5f) / 8f;
                    }
                }
                if (enemy.Category == StrikerStateCategory.Special) aggression = 1.5f;
                if (enemy.Category == StrikerStateCategory.Guard) aggression *= .2f;
                var vertical = Mathf.Clamp01((2.8f - Mathf.Abs(self.y - opponent.y)) / 1.4f);
                var proximity = Mathf.Clamp01((reach - Mathf.Abs(self.x - opponent.x)) / 2f) * vertical;
                if (enemy.Striker.Striker.CurrentValue is Striker.Hero or Striker.DemonKing) {
                    var lunge = enemy.Striker.Striker.CurrentValue == Striker.Hero ? 11f : 5f;
                    var gap = Mathf.Max(0f, Mathf.Abs(self.x - enemy.Body.center.x) - lunge * Mathf.Clamp01(t / Mathf.Max(.08f, frame.BeatSeconds)));
                    proximity = Mathf.Max(proximity, Mathf.Clamp01((3f - gap) / 2f) * vertical);
                    aggression = Mathf.Max(aggression, .5f + memory.AttackHabit);
                }
                risk += proximity * aggression / 8f;
                if (enemy.State is Core.LargeWizard.Attack3State && enemy.Wizard != null
                    && enemy.StateSeconds + frame.ExecutionDelay < enemy.Wizard.RockReleaseSeconds + .1f)
                    risk += Mathf.Clamp01(1f - Mathf.Abs(self.x - frame.Self.Body.center.x) / 2f) * .7f / 8f;
            }
            return risk;
        }

        static Vector3 PredictOpponent(DemonKingAiFrame frame, float seconds, DemonKingAiMemory memory) {
            var enemy = frame.Opponent;
            if (enemy.State is Core.LargeSatan.EmitState emit) {
                var spear = emit.ObservedPoleArm.ObserveProjectile();
                return ClampToArena(emit.ObservedPoleArm.transform.position + (spear?.Velocity ?? Vector3.zero) * Mathf.Min(seconds, .15f) + enemy.Body.center - enemy.Position, enemy);
            }
            var duration = enemy.State is Core.LargeHero.RushState or Core.LargeHero.SpecialApproachState ? .6f
                : enemy.Category is StrikerStateCategory.Attack or StrikerStateCategory.Dash ? .3f
                : enemy.Category is StrikerStateCategory.Guard or StrikerStateCategory.Charge or StrikerStateCategory.Special ? .12f : .5f;
            var velocity = enemy.Category == StrikerStateCategory.Idle ? Vector3.Lerp(enemy.Velocity, memory.OpponentDrift, .35f) : enemy.Velocity;
            var move = velocity * Mathf.Min(seconds, duration);
            if (!enemy.Grounded && velocity.y < 0f) move.y += Physics.gravity.y * .5f * seconds * seconds;
            return ClampToArena(enemy.Body.center + move, enemy);
        }

        static Vector3 PredictSelf(DemonKingAiCandidate candidate, DemonKingAiFrame frame, DemonKingAiTuning tuning, float seconds, bool clamp = true) {
            var actor = frame.Self;
            var source = actor.Demon;
            var t = Mathf.Max(0f, seconds);
            var buffer = AttackBufferSeconds(candidate, frame);
            var direction = (Vector3)candidate.Direction;
            var toward = frame.Opponent.Position.x >= actor.Position.x ? Vector3.right : Vector3.left;
            var delay = actor.Velocity * Mathf.Min(.12f, frame.ExecutionDelay);
            delay += actor.Velocity * Mathf.Min(t, buffer);
            t = Mathf.Max(0f, t - buffer);
            Vector3 move;
            switch (candidate.Move) {
                case DemonKingAiMove.Move:
                    if (actor.Grounded) {
                        var jump = direction.y > .5f;
                        var speed = jump ? source.JumpSpeed : source.DashSpeed(Vector3.Dot(direction, actor.Striker.LookDirection.CurrentValue) >= 0f);
                        var duration = jump ? source.JumpSeconds : source.DashSeconds;
                        move = DecayMove(direction * speed, jump ? source.JumpEndRatio : source.DashEndRatio, duration, t);
                        if (jump && t > duration) move += Physics.gravity * (.5f * (t - duration) * (t - duration));
                    } else {
                        var time = Mathf.Min(t, source.AirJumpSeconds);
                        move = direction * source.AirJumpSpeed * time + Vector3.down * (.5f * source.AirJumpAcceleration * time * time);
                        if (t > time) move += (direction * source.AirJumpSpeed + Vector3.down * source.AirJumpAcceleration * time) * (t - time) + Physics.gravity * (.5f * (t - time) * (t - time));
                    }
                    break;
                case DemonKingAiMove.Dive:
                    move = DecayMove(Vector3.down * tuning.DiveSpeed, tuning.DiveEndRatio, tuning.DiveSeconds, t);
                    break;
                case DemonKingAiMove.Guard:
                    move = direction * source.GuardShift;
                    var afterGuard = Mathf.Max(0f, t - actor.NextGuardSeconds);
                    if (!actor.Grounded) move += Physics.gravity * (.5f * afterGuard * afterGuard);
                    break;
                case DemonKingAiMove.Melee: {
                    var strike = source.NextStrike;
                    var step = strike.StepSpeed * (Mathf.Min(t, strike.StepDuration) + Mathf.Min(Mathf.Max(0f, t - strike.StepDuration), strike.StepBrakeDuration) * .5f);
                    var distance = Mathf.Max(0f, Mathf.Abs(frame.Opponent.Position.x - actor.Position.x) - source.MeleeStopDistance);
                    move = toward * Mathf.Min(step, distance);
                    var progress = Mathf.Clamp01(t / strike.Duration);
                    move.y = 4f * strike.JumpHeight * progress * (1f - progress);
                    break;
                }
                case DemonKingAiMove.Uppercut:
                case DemonKingAiMove.HeelDrop: {
                    var technique = candidate.Move == DemonKingAiMove.Uppercut ? source.Uppercut : source.HeelDrop;
                    var takeoff = Mathf.Max(0f, Mathf.Min(t, technique.ObservationDuration) - technique.ObservationTakeoffSeconds);
                    move = toward * technique.ObservationForwardSpeed * takeoff;
                    var initialY = candidate.Move == DemonKingAiMove.Uppercut || actor.Grounded ? technique.ObservationJumpSpeed : actor.Velocity.y;
                    move.y = initialY * takeoff + Physics.gravity.y * .5f * takeoff * takeoff;
                    if (candidate.Move == DemonKingAiMove.HeelDrop && t > technique.ObservationDiveSeconds) {
                        var before = Mathf.Max(0f, technique.ObservationDiveSeconds - technique.ObservationTakeoffSeconds);
                        var after = Mathf.Min(t, technique.ObservationDuration) - technique.ObservationDiveSeconds;
                        move.y = initialY * before + Physics.gravity.y * .5f * before * before - technique.ObservationDiveSpeed * after + Physics.gravity.y * .5f * after * after;
                    }
                    break;
                }
                case DemonKingAiMove.AirAttack:
                    move = DecayMove(toward * tuning.AirAttackSpeed, .1f, tuning.AirAttackSeconds, t);
                    break;
                case DemonKingAiMove.Charge:
                case DemonKingAiMove.Launch:
                    move = Vector3.right * actor.Velocity.x * Mathf.Min(t, .15f);
                    break;
                case DemonKingAiMove.Special:
                    move = toward * Mathf.Min(source.SpecialReach, source.SpecialSpeed * Mathf.Max(0f, t - .2f));
                    break;
                default:
                    var velocity = actor.Velocity;
                    if (candidate.Move == DemonKingAiMove.Walk) velocity.x = direction.x * tuning.WalkSpeed;
                    move = velocity * Mathf.Min(t, .2f);
                    if (!actor.Grounded) move += Physics.gravity * (.5f * t * t);
                    break;
            }
            var center = actor.Body.center + delay + move;
            return clamp ? ClampToArena(center, actor) : center;
        }

        static Vector3 DecayMove(Vector3 velocity, float ratio, float duration, float seconds) {
            var rate = Mathf.Log(1f / Mathf.Max(.0001f, ratio)) / Mathf.Max(.001f, duration);
            return rate < .001f ? velocity * Mathf.Min(seconds, duration) : velocity * ((1f - Mathf.Exp(-rate * Mathf.Min(seconds, duration))) / rate);
        }

        static float AttackBufferSeconds(DemonKingAiCandidate candidate, DemonKingAiFrame frame) {
            return frame.Self.State is Core.DarkSatan.DemonMeleeAttackState
                && candidate.Move is DemonKingAiMove.Melee or DemonKingAiMove.Uppercut or DemonKingAiMove.HeelDrop
                ? Mathf.Max(0f, frame.Self.Demon.MeleeChainDelay - frame.ExecutionDelay) : 0f;
        }

        static Vector3 ClampToArena(Vector3 center, DemonKingAiActor actor) {
            if (!actor.Arena.HasValue) return center;
            var arena = actor.Arena.Value;
            var extents = actor.Body.extents;
            return new Vector3(Mathf.Clamp(center.x, arena.min.x + extents.x, arena.max.x - extents.x),
                Mathf.Clamp(center.y, arena.min.y + extents.y, arena.max.y - extents.y), center.z);
        }

        static bool SegmentIntersects(Bounds bounds, Vector3 start, Vector3 end) {
            if (bounds.Contains(start) || bounds.Contains(end)) return true;
            var delta = end - start;
            return delta.sqrMagnitude > .000001f && bounds.IntersectRay(new Ray(start, delta.normalized), out var distance) && distance <= delta.magnitude;
        }

        static GamePadButton? ButtonFor(DemonKingAiMove move) => move switch {
            DemonKingAiMove.Melee or DemonKingAiMove.Uppercut or DemonKingAiMove.HeelDrop or DemonKingAiMove.AirAttack or DemonKingAiMove.Launch => GamePadButton.East,
            DemonKingAiMove.Charge => GamePadButton.South,
            DemonKingAiMove.Move or DemonKingAiMove.Dive => GamePadButton.West,
            DemonKingAiMove.Guard => GamePadButton.North,
            DemonKingAiMove.Special => GamePadButton.Start,
            _ => null
        };

        static DemonKingAiChoice Idle(string reason) => new(AiAction.None, DemonKingAiMove.Wait, 0f, reason);
    }
}
