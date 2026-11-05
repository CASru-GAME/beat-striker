using System.Collections.Generic;
using UnityEngine;

namespace Alice.WizardAi {
    internal enum WizardAiMove { Wait, Walk, Dash, Jump, Shield, LaunchShield, Charge, Melee, Fire, Ice, Rock, Special, Recover }
    internal sealed record WizardAiChoice(AiAction Action, WizardAiMove Move, float Score, string Reason);
    internal sealed record WizardAiCandidate(WizardAiMove Move, Vector2 Direction, string Reason);

    internal sealed class WizardAiDecision {
        readonly List<WizardAiCandidate> candidates = new(16);
        readonly List<Bounds> shields = new(4);
        readonly HashSet<int> consumedShields = new();

        public WizardAiChoice Choose(WizardAiFrame frame, WizardAiTuning tuning, WizardAiMatchup matchup, WizardAiMemory memory, AiDifficulty difficulty) {
            var self = frame.Self;
            if (self.Striker.HitPoint.CurrentValue <= 0f || frame.Opponent.Striker.HitPoint.CurrentValue <= 0f) return Idle("Round ended");
            if (self.Category == StrikerStateCategory.Special)
                return new(new AiAction(Vector2.zero, GamePadButton.North), WizardAiMove.Wait, 0f, "Keep rhythm during special");
            if (self.State is Core.LargeWizard.StunState)
                return new(new AiAction(Vector2.zero, GamePadButton.North), WizardAiMove.Recover, 0f, "Break hit stun");

            BuildCandidates(frame);
            var (selected, score) = difficulty.SelectCandidate(
                candidates,
                static candidate => candidate.Move != WizardAiMove.Wait,
                static candidate => ButtonFor(candidate.Move),
                (candidate, anticipationWeight) => Score(candidate, frame, tuning, matchup, memory, anticipationWeight));
            if (selected == null) return Idle("Observe");
            return new(new AiAction(selected.Direction, ButtonFor(selected.Move)), selected.Move, score, selected.Reason);
        }

        void BuildCandidates(WizardAiFrame frame) {
            candidates.Clear();
            var toward = frame.Opponent.Position.x >= frame.Self.Position.x ? Vector2.right : Vector2.left;
            var energy = frame.Self.Wizard.Energy;
            var attack = energy switch { 0 => WizardAiMove.Melee, 1 => WizardAiMove.Fire, 2 => WizardAiMove.Ice, _ => WizardAiMove.Rock };
            Add(attack, Vector2.zero, energy switch { 0 => "Punish in melee range", 1 => "Fire through an open lane", 2 => "Ice beneath the opponent", _ => "Drop a rock above the shields" });
            if (energy < 3) Add(WizardAiMove.Charge, Vector2.zero, "Build energy for the next spell");
            Add(WizardAiMove.Shield, Vector2.zero, "Stack protection and push back pressure");
            if (frame.SelfShields.Count > 0) Add(WizardAiMove.LaunchShield, toward, "Launch a shield to interrupt pursuit");
            Add(WizardAiMove.Dash, -toward, "Open space outside the attack lane");
            Add(WizardAiMove.Dash, toward, "Cross pressure and leave the corner");
            Add(WizardAiMove.Jump, (-toward + Vector2.up).normalized, "Evade spear and follow-up diagonally");
            Add(WizardAiMove.Jump, (toward + Vector2.up).normalized, "Cross over the shield or pursuit");
            Add(WizardAiMove.Jump, Vector2.up, "Rise above the ground attack");
            Add(WizardAiMove.Walk, -toward, "Keep casting distance");
            Add(WizardAiMove.Walk, toward, "Reposition into effective range");
            var maximum = frame.Self.Striker.MaxSpecialPoint.CurrentValue;
            if (maximum > 0f && frame.Self.Striker.SpecialPoint.CurrentValue >= maximum - .001f)
                Add(WizardAiMove.Special, Vector2.zero, "Release special into the firing lane");
            Add(WizardAiMove.Wait, Vector2.zero, "Finish the pending charge or spell");
        }

        void Add(WizardAiMove move, Vector2 direction, string reason) => candidates.Add(new(move, direction, reason));

        float Score(WizardAiCandidate candidate, WizardAiFrame frame, WizardAiTuning tuning, WizardAiMatchup matchup,
            WizardAiMemory memory, float anticipationWeight) {
            var horizon = Mathf.Clamp(frame.BeatSeconds, .3f, 1.2f);
            var end = PredictSelf(candidate, frame, tuning, horizon);
            var enemy = PredictOpponent(frame, horizon + frame.ExecutionDelay, memory);
            var gap = Mathf.Abs(enemy.x - end.x);
            var startGap = Mathf.Abs(frame.Opponent.Body.center.x - frame.Self.Body.center.x);
            var preferred = matchup.PreferredDistance + memory.AbHabit * 2f;
            var positionGain = Mathf.Abs(startGap - preferred) - Mathf.Abs(gap - preferred);
            var score = Mathf.Clamp(positionGain, -5f, 5f) * tuning.PositionWeight;
            var hpRatio = frame.Self.Striker.HitPoint.CurrentValue / Mathf.Max(1f, frame.Self.Striker.MaxHitPoint.CurrentValue);
            var risk = ThreatRisk(candidate, frame, tuning, horizon, matchup.ProjectileCaution);
            risk += AnticipationRisk(candidate, frame, tuning, matchup, memory, horizon) * anticipationWeight;
            score -= risk * tuning.RiskWeight * (hpRatio < .3f ? 1.35f : 1f);
            score -= BoundaryCost(candidate, frame, tuning, horizon);

            var pending = frame.Self.Wizard.PendingReleaseSeconds(frame.Self.State) - frame.Self.StateSeconds - frame.ExecutionDelay;
            var hasPending = frame.Self.State is Core.LargeWizard.ChargeState or Core.LargeWizard.Attack1State or Core.LargeWizard.Attack2State or Core.LargeWizard.Attack3State;
            // Exiting a state clears its scheduled spawn. Include execution delay, not just the observation time.
            if (hasPending && pending > -.025f && candidate.Move is not (WizardAiMove.Wait or WizardAiMove.Special)) score -= 27f;

            switch (candidate.Move) {
                case WizardAiMove.Melee:
                case WizardAiMove.Fire:
                case WizardAiMove.Ice:
                case WizardAiMove.Rock: {
                    var chance = HitChance(candidate.Move, frame, tuning, memory, 0f);
                    var damage = Damage(candidate.Move, tuning);
                    score += chance * Mathf.Min(damage, frame.Opponent.Striker.HitPoint.CurrentValue) * tuning.DamageWeight * matchup.Pressure;
                    score -= (1f - chance) * (9f + memory.WhiffPressure * 2f);
                    if (damage >= frame.Opponent.Striker.HitPoint.CurrentValue) score += chance * 14f;
                    break;
                }
                case WizardAiMove.Charge: {
                    var energy = frame.Self.Wizard.Energy;
                    // Compare the value after one or two more charges. This makes rock reachable against shield stacks.
                    var one = energy == 0 ? WizardAiMove.Fire : energy == 1 ? WizardAiMove.Ice : WizardAiMove.Rock;
                    var plan = HitChance(one, frame, tuning, memory, frame.BeatSeconds) * Damage(one, tuning) / 1.7f;
                    if (energy < 2) {
                        var two = energy == 0 ? WizardAiMove.Ice : WizardAiMove.Rock;
                        plan = Mathf.Max(plan, HitChance(two, frame, tuning, memory, frame.BeatSeconds * 2f) * Damage(two, tuning) / 2.5f);
                    }
                    score += plan + (energy == 0 ? 8f : 1f);
                    if (frame.OpponentShields.Count > 0 && energy >= 1) score += memory.ShieldHabit * 6f;
                    if (energy == 0 && startGap < matchup.OpponentReach && frame.SelfShields.Count == 0) score -= 9f;
                    break;
                }
                case WizardAiMove.Shield:
                    score += frame.SelfShields.Count == 0 ? 6f : frame.SelfShields.Count == 1 ? 2f : -3f;
                    score -= Mathf.Min(memory.GuardStreak, 5) * 1.5f * (1f - memory.ShieldHabit * .8f);
                    if (startGap < 5f && Mathf.Abs(enemy.y - end.y) < 2f) score += 4f + memory.AttackHabit * 4f;
                    // Sustain a shield stock and the beat combo until the beam can break a renewing shield wall.
                    if (frame.OpponentShields.Count > 0) score += memory.ShieldHabit * (5f + Mathf.Max(0, 3 - frame.SelfShields.Count) * 3f);
                    break;
                case WizardAiMove.LaunchShield:
                    score += HitChance(WizardAiMove.LaunchShield, frame, tuning, memory, 0f) * 10f - 4f;
                    if (frame.SelfShields.Count == 1 && startGap < matchup.OpponentReach) score -= 10f;
                    break;
                case WizardAiMove.Special:
                    var specialChance = HitChance(WizardAiMove.Special, frame, tuning, memory, 0f);
                    score += tuning.SpecialScore * specialChance - (1f - specialChance) * 40f;
                    // Special has no locomotion parent, so ordinary hit/command handling is suspended during it.
                    break;
                case WizardAiMove.Dash:
                case WizardAiMove.Jump:
                    score -= 5f;
                    if (candidate.Move == WizardAiMove.Jump) score -= Mathf.Max(0f, end.y - enemy.y - 3f) * 1.5f;
                    break;
                case WizardAiMove.Walk:
                    score -= 3f + memory.ShieldHabit * 8f;
                    break;
                case WizardAiMove.Wait:
                    score -= 8f + Mathf.Min(8, memory.StaleBeats);
                    if (hasPending && pending > -.025f) score += 25f;
                    break;
            }
            return score;
        }

        static float Damage(WizardAiMove move, WizardAiTuning tuning) => move switch {
            WizardAiMove.Fire => tuning.FireDamage,
            WizardAiMove.Ice => tuning.IceDamage,
            WizardAiMove.Rock => tuning.RockDamage,
            _ => 5f
        };

        static float HitChance(WizardAiMove move, WizardAiFrame frame, WizardAiTuning tuning, WizardAiMemory memory, float preparation) {
            var source = frame.Self.Wizard;
            var enemy = frame.Opponent;
            var origin = source.FireOrigin + frame.Self.Velocity * (preparation + frame.ExecutionDelay);
            var look = frame.Self.Striker.LookDirection.CurrentValue.normalized;
            if (move == WizardAiMove.Ice) {
                var spawn = preparation + frame.ExecutionDelay + source.IceReleaseSeconds;
                var target = PredictOpponent(frame, spawn, memory);
                var hitAt = spawn + tuning.IceGrowthSeconds;
                var atHit = PredictOpponent(frame, hitAt, memory);
                var floor = enemy.Arena?.min.y ?? enemy.Body.min.y;
                var above = atHit.y - enemy.Body.extents.y - floor;
                var chance = Mathf.Clamp01((tuning.IceHalfWidth + enemy.Body.extents.x - Mathf.Abs(atHit.x - target.x)) / .8f)
                    * Mathf.Clamp01((tuning.IceHeight - above) / .7f);
                // The ice remains after growing. Repeated rushes can return through the same ground column.
                if (enemy.State is Core.LargeHero.RushState && enemy.Grounded)
                    chance = Mathf.Max(chance, memory.AttackHabit * .65f);
                // Ice begins underneath the victim; only a shield intersecting that column can absorb it.
                var iceColumn = new Bounds(new Vector3(target.x, floor + tuning.IceHeight * .5f, target.z),
                    new Vector3(tuning.IceHalfWidth * 2f, tuning.IceHeight, 2f));
                foreach (var shield in frame.OpponentShields) {
                    var block = shield.Bounds;
                    block.center += target - enemy.Body.center;
                    if (block.Intersects(iceColumn)) { chance *= .12f; break; }
                }
                return chance * Vulnerability(enemy, hitAt);
            }
            if (move == WizardAiMove.Rock) {
                var spawn = preparation + frame.ExecutionDelay + source.RockReleaseSeconds;
                var target = PredictOpponent(frame, spawn, memory);
                var fall = Mathf.Sqrt(2f * Mathf.Max(.1f, source.RockSpawnHeight - enemy.Body.extents.y - tuning.RockRadius) / Mathf.Max(.01f, -Physics.gravity.y));
                var hitAt = spawn + fall;
                var atHit = PredictOpponent(frame, hitAt, memory);
                var chance = Mathf.Clamp01((tuning.RockRadius + enemy.Body.extents.x - Mathf.Abs(atHit.x - target.x)) / 1.3f);
                var above = target + Vector3.up * source.RockSpawnHeight;
                chance *= ShieldTransmission(above, atHit, tuning.RockRadius, frame, hitAt);
                return chance * Vulnerability(enemy, hitAt) * (1f - memory.AttackHabit * .18f);
            }
            if (move == WizardAiMove.Melee) {
                var delta = enemy.Body.center - frame.Self.Body.center;
                var chance = Mathf.Clamp01((tuning.MeleeReach + enemy.Body.extents.x - Mathf.Abs(delta.x)) / .8f)
                    * Mathf.Clamp01((1.5f - Mathf.Abs(delta.y)) / .8f);
                return chance * ShieldTransmission(frame.Self.Body.center, enemy.Body.center, .1f, frame, 0f) * Vulnerability(enemy, frame.ExecutionDelay);
            }
            if (move == WizardAiMove.Special) {
                var target = PredictOpponent(frame, frame.ExecutionDelay + source.SpecialDelay, memory);
                var delta = target - source.SpecialOrigin;
                var along = Vector3.Dot(delta, look);
                // The continuous beam can remove a shield and strike again. It is not stopped like a fireball.
                return Mathf.Clamp01((source.SpecialRange - along) / 3f) * Mathf.Clamp01((along + 1f) / 2f)
                    * Mathf.Clamp01((3.5f - Mathf.Abs(delta.y)) / 1.5f);
            }
            var speed = move == WizardAiMove.LaunchShield ? source.ShieldSpeed : tuning.FireSpeed;
            var release = move == WizardAiMove.Fire ? source.FireReleaseSeconds : 0f;
            var flight = Vector3.Distance(origin, enemy.Body.center) / Mathf.Max(.1f, speed);
            var impact = preparation + frame.ExecutionDelay + release + flight;
            var aim = PredictOpponent(frame, impact, memory) - origin;
            var forward = Vector3.Dot(aim, look);
            if (forward < 0f) return 0f;
            var miss = (aim - look * forward).magnitude;
            var radius = move == WizardAiMove.LaunchShield ? tuning.ShieldRadius : move == WizardAiMove.Special ? 2f : tuning.FireRadius;
            var chanceRay = Mathf.Clamp01((radius + enemy.Body.extents.y - miss) / 1f);
            chanceRay *= ShieldTransmission(origin, origin + look * forward, radius, frame, impact);
            return chanceRay * Vulnerability(enemy, impact) * (1f - memory.WhiffPressure * .05f);
        }

        static float ShieldTransmission(Vector3 origin, Vector3 target, float radius, WizardAiFrame frame, float future) {
            var translation = frame.Opponent.Velocity * Mathf.Min(future, .15f);
            var body = frame.Opponent.Body;
            body.center = target;
            body.Expand(radius * 2f);
            var ray = new Ray(origin, (target - origin).normalized);
            body.IntersectRay(ray, out var bodyDistance);
            foreach (var shield in frame.OpponentShields) {
                var bounds = shield.Bounds;
                bounds.center += translation;
                bounds.Expand(radius * 2f);
                if (bounds.IntersectRay(ray, out var shieldDistance) && shieldDistance < bodyDistance - .05f) return .06f;
            }
            if (frame.Opponent.Striker.Striker.CurrentValue == Striker.Hero && frame.Opponent.Category == StrikerStateCategory.Guard)
                return .2f;
            return 1f;
        }

        static float Vulnerability(WizardAiActor actor, float future) {
            if (actor.Category == StrikerStateCategory.Special) return .05f;
            if (actor.Category == StrikerStateCategory.Guard && actor.Striker.Striker.CurrentValue is Striker.Warrior or Striker.DemonKing) {
                var guardSeconds = (actor.Striker as IStrikerCombatObservationSource)?.ActiveGuardSeconds ?? 1f;
                return actor.StateSeconds + future < guardSeconds ? .05f : 1f;
            }
            return 1f;
        }

        float ThreatRisk(WizardAiCandidate candidate, WizardAiFrame frame, WizardAiTuning tuning, float horizon, float caution) {
            if (candidate.Move == WizardAiMove.Special) return .03f;
            PrepareShields(candidate, frame, tuning);
            consumedShields.Clear();
            var risk = 0f;
            foreach (var threat in frame.Threats) {
                var start = Mathf.Max(frame.ExecutionDelay, threat.StartsAt);
                var end = Mathf.Min(threat.EndsAt, frame.ExecutionDelay + horizon);
                if (start >= end) continue;
                var hitAt = -1f;
                var body = new Bounds(Vector3.zero, frame.Self.Body.size + threat.Bounds.size + Vector3.one * tuning.PredictionMargin * 2f);
                for (var i = 0; i < 16; i++) {
                    var a = Mathf.Lerp(start, end, i / 16f);
                    var b = Mathf.Lerp(start, end, (i + 1) / 16f);
                    var relativeA = PredictSelf(candidate, frame, tuning, a - frame.ExecutionDelay) - ThreatPosition(threat, a);
                    var relativeB = PredictSelf(candidate, frame, tuning, b - frame.ExecutionDelay) - ThreatPosition(threat, b);
                    if (!SegmentIntersects(body, relativeA, relativeB)) continue;
                    hitAt = a;
                    break;
                }
                if (hitAt < 0f) continue;
                var blocked = false;
                // Persistent beams and rushes can consume a shield and still hit. Never treat a shield as invulnerability.
                if (threat.Severity < 2f) {
                    for (var s = 0; s < shields.Count; s++) {
                        if (consumedShields.Contains(s)) continue;
                        var newShield = candidate.Move == WizardAiMove.Shield && s == shields.Count - 1;
                        if (newShield && hitAt < frame.ExecutionDelay + tuning.ShieldGenerationSeconds) continue;
                        var block = shields[s];
                        block.center += PredictSelf(candidate, frame, tuning, hitAt - frame.ExecutionDelay) - frame.Self.Body.center;
                        block.Expand(threat.Bounds.size);
                        var from = ThreatPosition(threat, Mathf.Max(0f, hitAt - .25f));
                        var to = ThreatPosition(threat, hitAt + .05f);
                        if (!SegmentIntersects(block, from, to)) continue;
                        // The shield must lie before the body along the incoming trajectory.
                        var self = PredictSelf(candidate, frame, tuning, hitAt - frame.ExecutionDelay);
                        if (Vector3.Distance(from, block.ClosestPoint(from)) >= Vector3.Distance(from, self)) continue;
                        consumedShields.Add(s);
                        blocked = true;
                        break;
                    }
                }
                risk += threat.Severity * (threat.Projectile ? caution : 1f) * (blocked ? .12f : 1f);
            }
            return Mathf.Min(4f, risk);
        }

        void PrepareShields(WizardAiCandidate candidate, WizardAiFrame frame, WizardAiTuning tuning) {
            shields.Clear();
            foreach (var shield in frame.SelfShields) shields.Add(shield.Bounds);
            if (candidate.Move == WizardAiMove.LaunchShield && shields.Count > 0) shields.RemoveAt(shields.Count - 1);
            if (candidate.Move != WizardAiMove.Shield) return;
            if (shields.Count >= 3) shields.RemoveAt(shields.Count - 1);
            var center = frame.Self.Position + frame.Self.Striker.LookDirection.CurrentValue * tuning.ShieldForwardOffset + Vector3.up * tuning.ShieldHeight;
            shields.Add(new Bounds(center, Vector3.one * tuning.ShieldRadius * 2f));
        }

        static Vector3 ThreatPosition(WizardAiThreat threat, float t) {
            var moving = threat.Projectile ? Mathf.Max(0f, t - threat.StartsAt) : t;
            return threat.Bounds.center + threat.Velocity * moving + threat.Acceleration * (moving * moving * .5f);
        }

        static float AnticipationRisk(WizardAiCandidate candidate, WizardAiFrame frame, WizardAiTuning tuning,
            WizardAiMatchup matchup, WizardAiMemory memory, float horizon) {
            if (candidate.Move == WizardAiMove.Special) return 0f;
            var enemy = frame.Opponent;
            var risk = 0f;
            var spearRisk = 0f;
            if (enemy.Wizard != null && enemy.Wizard.Energy == 2) {
                // Ice can spawn on the next command without a visible wind-up. Charged energy is already observable.
                var spawnTime = enemy.Wizard.IceReleaseSeconds;
                var spawn = PredictSelf(candidate, frame, tuning, spawnTime);
                var grown = PredictSelf(candidate, frame, tuning, spawnTime + tuning.IceGrowthSeconds);
                var floor = frame.Self.Arena?.min.y ?? frame.Self.Body.min.y;
                var overlap = Mathf.Clamp01((tuning.IceHalfWidth + frame.Self.Body.extents.x + tuning.PredictionMargin - Mathf.Abs(grown.x - spawn.x)) / .7f);
                var grounded = Mathf.Clamp01((tuning.IceHeight + tuning.PredictionMargin - (grown.y - frame.Self.Body.extents.y - floor)) / .8f);
                risk = overlap * grounded * 1.3f;
            }
            for (var i = 1; i <= 8; i++) {
                var t = horizon * i / 8f;
                var self = PredictSelf(candidate, frame, tuning, t);
                var opponent = PredictOpponent(frame, t + frame.ExecutionDelay, memory);
                var vertical = Mathf.Clamp01((2.8f - Mathf.Abs(self.y - opponent.y)) / 1.5f);
                var reach = matchup.OpponentReach;
                var aggression = .12f + memory.AttackHabit * .25f;
                if (enemy.Striker.Striker.CurrentValue == Striker.Warrior) {
                    reach = tuning.WarriorLungeReach;
                    aggression += memory.AbHabit * .4f;
                    if (enemy.State is Core.LargeSatan.ChargeState charge && charge.ObservedEmitState != null) {
                        var direction = charge.ObservedEmitState.ObserveThrowDirection(Vector2.zero, enemy.Striker.LookDirection.CurrentValue);
                        var spear = charge.ObservedPoleArm.transform.position + direction * charge.ObservedEmitState.ObserveTravelDistance(t);
                        var lane = new Bounds(Vector3.zero, frame.Self.Body.size + Vector3.one * .8f);
                        if (SegmentIntersects(lane, charge.ObservedPoleArm.transform.position - self, spear - self)) spearRisk += (.9f + memory.AbHabit * .5f) / 8f;
                    }
                    if (enemy.State is Core.LargeSatan.EmitState) {
                        reach = tuning.WarriorWarpReach;
                        aggression = .7f + memory.AbHabit * .65f;
                    }
                }
                if (enemy.Category == StrikerStateCategory.Special) aggression = enemy.Striker.Striker.CurrentValue == Striker.DemonKing ? 1.6f : .7f;
                if (enemy.Category == StrikerStateCategory.Guard) aggression *= .25f;
                var proximity = Mathf.Clamp01((reach - Mathf.Abs(self.x - opponent.x)) / 2f) * vertical;
                // Repeated melee inputs can start a new lunge on this beat, even when the old hitbox has ended.
                if (enemy.Striker.Striker.CurrentValue is Striker.Hero or Striker.DemonKing) {
                    var lunge = enemy.Striker.Striker.CurrentValue == Striker.Hero ? 11f : 8f;
                    var closing = lunge * Mathf.Clamp01(t / Mathf.Max(.08f, frame.BeatSeconds));
                    var approachGap = Mathf.Max(0f, Mathf.Abs(self.x - enemy.Body.center.x) - closing);
                    var nextAttack = Mathf.Clamp01((3f - approachGap) / 2f) * Mathf.Clamp01((2.8f - Mathf.Abs(self.y - enemy.Body.center.y)) / 1.5f);
                    proximity = Mathf.Max(proximity, nextAttack);
                    aggression = Mathf.Max(aggression, .6f + memory.AttackHabit * 1.4f);
                }
                var protection = frame.SelfShields.Count > 0 ? .65f : 1f;
                if (candidate.Move == WizardAiMove.Shield && t >= tuning.ShieldGenerationSeconds) protection *= .55f;
                // Shields only cover the facing side. A warp through or behind them bypasses this protection.
                if (Vector3.Dot(opponent - self, frame.Self.Striker.LookDirection.CurrentValue) < 0f) protection = 1f;
                var approachRisk = proximity * aggression * protection;
                if (enemy.Striker.Striker.CurrentValue is Striker.Hero or Striker.DemonKing) risk = Mathf.Max(risk, approachRisk);
                else risk += approachRisk / 8f;

                if (enemy.Striker.Striker.CurrentValue == Striker.Wizard && enemy.Wizard != null) {
                    var targetTime = enemy.State is Core.LargeWizard.Attack3State ? enemy.Wizard.RockReleaseSeconds : 0f;
                    var stationary = Mathf.Clamp01(1f - Mathf.Abs(self.x - frame.Self.Body.center.x) / 2f);
                    if (enemy.State is Core.LargeWizard.Attack2State && !frame.Self.Grounded) stationary *= .15f;
                    if (enemy.State is Core.LargeWizard.Attack2State or Core.LargeWizard.Attack3State
                        && enemy.StateSeconds + frame.ExecutionDelay <= targetTime + .1f) risk += stationary * .7f / 8f;
                }
            }
            return risk + spearRisk;
        }

        static Vector3 PredictOpponent(WizardAiFrame frame, float seconds, WizardAiMemory memory) {
            var enemy = frame.Opponent;
            if (enemy.State is Core.LargeSatan.EmitState emit) {
                var spear = emit.ObservedPoleArm.ObserveProjectile();
                // Any following A/B input warps to the spear. Treat that visible destination as a second approach.
                var destination = emit.ObservedPoleArm.transform.position + (spear?.Velocity ?? Vector3.zero) * Mathf.Min(seconds, .15f);
                return ClampToArena(destination + enemy.Body.center - enemy.Position, enemy);
            }
            var duration = enemy.State is Core.LargeHero.RushState or Core.LargeHero.SpecialApproachState ? .6f
                : enemy.Category is StrikerStateCategory.Attack or StrikerStateCategory.Dash ? .3f
                : enemy.Category is StrikerStateCategory.Guard or StrikerStateCategory.Charge or StrikerStateCategory.Special ? .12f : .5f;
            var velocity = enemy.Velocity;
            if (enemy.Category == StrikerStateCategory.Idle) velocity = Vector3.Lerp(velocity, memory.OpponentDrift, .35f);
            var movement = velocity * Mathf.Min(seconds, duration);
            if (!enemy.Grounded && velocity.y < 0f) movement.y += Physics.gravity.y * seconds * seconds * .5f;
            return ClampToArena(enemy.Body.center + movement, enemy);
        }

        static Vector3 PredictSelf(WizardAiCandidate candidate, WizardAiFrame frame, WizardAiTuning tuning, float seconds, bool clamp = true) {
            var velocity = frame.Self.Velocity;
            if (candidate.Move == WizardAiMove.Dash) velocity = candidate.Direction * frame.Self.Wizard.DashSpeed;
            else if (candidate.Move == WizardAiMove.Jump) velocity = candidate.Direction * frame.Self.Wizard.JumpSpeed;
            else if (candidate.Move == WizardAiMove.Walk) velocity.x = candidate.Direction.x * tuning.WalkSpeed;
            var center = frame.Self.Body.center + frame.Self.Velocity * frame.ExecutionDelay + velocity * seconds;
            if (!frame.Self.Grounded || candidate.Move == WizardAiMove.Jump) center += Physics.gravity * (seconds * seconds * .5f);
            return clamp ? ClampToArena(center, frame.Self) : center;
        }

        static Vector3 ClampToArena(Vector3 center, WizardAiActor actor) {
            if (!actor.Arena.HasValue) return center;
            var arena = actor.Arena.Value;
            var extents = actor.Body.extents;
            return new Vector3(Mathf.Clamp(center.x, arena.min.x + extents.x, arena.max.x - extents.x),
                Mathf.Clamp(center.y, arena.min.y + extents.y, arena.max.y - extents.y), center.z);
        }

        static float BoundaryCost(WizardAiCandidate candidate, WizardAiFrame frame, WizardAiTuning tuning, float horizon) {
            if (!frame.Self.Arena.HasValue) return 0f;
            var raw = PredictSelf(candidate, frame, tuning, horizon, false);
            var safe = ClampToArena(raw, frame.Self);
            var cost = Mathf.Abs(raw.x - safe.x) * 3f + Mathf.Max(0f, raw.y - safe.y) * 2f;
            if (candidate.Move is WizardAiMove.Dash or WizardAiMove.Jump) {
                var arena = frame.Self.Arena.Value;
                var clearance = Mathf.Min(safe.x - arena.min.x, arena.max.x - safe.x) - frame.Self.Body.extents.x;
                cost += Mathf.Max(0f, 1f - clearance) * 6f;
            }
            return cost;
        }

        static bool SegmentIntersects(Bounds bounds, Vector3 start, Vector3 end) {
            if (bounds.Contains(start) || bounds.Contains(end)) return true;
            var delta = end - start;
            return delta.sqrMagnitude > .000001f && bounds.IntersectRay(new Ray(start, delta.normalized), out var distance) && distance <= delta.magnitude;
        }

        static GamePadButton? ButtonFor(WizardAiMove move) => move switch {
            WizardAiMove.Melee or WizardAiMove.Fire or WizardAiMove.Ice or WizardAiMove.Rock => GamePadButton.East,
            WizardAiMove.Charge => GamePadButton.South,
            WizardAiMove.Dash or WizardAiMove.Jump => GamePadButton.West,
            WizardAiMove.Shield or WizardAiMove.LaunchShield => GamePadButton.North,
            WizardAiMove.Special => GamePadButton.Start,
            _ => null
        };

        static WizardAiChoice Idle(string reason) => new(AiAction.None, WizardAiMove.Wait, 0f, reason);
    }
}
