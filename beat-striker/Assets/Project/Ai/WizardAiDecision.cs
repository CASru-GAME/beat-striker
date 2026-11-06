using System.Collections.Generic;
using UnityEngine;

namespace Alice.WizardAi {
    internal enum WizardAiMove { Wait, Walk, Dash, Jump, Shield, LaunchShield, Charge, Melee, Fire, Ice, Rock, Special, Recover }
    internal sealed record WizardAiChoice(AiAction Action, WizardAiMove Move, float Score, string Reason);
    internal sealed record WizardAiCandidate(WizardAiMove Move, Vector2 Direction, string Reason);

    internal sealed class WizardAiDecision {
        bool advancedTactics;
        int opponentReply;
        float predictionWeight = 1f;
        float followUpWeight = 1f;
        float threatWeight = 1f;

        readonly List<WizardAiThreat> forecast = new(8);
        bool hasMeleeForecast, hasRushForecast;
        readonly List<WizardAiCandidate> candidates = new(16);
        readonly List<Bounds> shields = new(4);
        readonly HashSet<int> consumedShields = new();

        public WizardAiChoice Choose(WizardAiFrame frame, WizardAiTuning tuning, WizardAiMatchup matchup, WizardAiMemory memory, AiDifficulty difficulty) {
            advancedTactics = difficulty.UsesAdvancedTactics;
            opponentReply = 0;
            predictionWeight = difficulty.PredictionWeight;
            followUpWeight = difficulty.FollowUpWeight;
            threatWeight = difficulty.ThreatWeight;
            var self = frame.Self;
            if (self.Striker.HitPoint.CurrentValue <= 0f || frame.Opponent.Striker.HitPoint.CurrentValue <= 0f) return Idle("Round ended");
            if (difficulty.MissesBeat()) return Idle("Missed the beat");
            if (self.Category == StrikerStateCategory.Special)
                return new(new AiAction(Vector2.zero, GamePadButton.North), WizardAiMove.Wait, 0f, "Keep rhythm during special");
            if (self.State is Core.LargeWizard.StunState)
                return new(new AiAction(Vector2.zero, GamePadButton.North), WizardAiMove.Recover, 0f, "Break hit stun");

            BuildCandidates(frame);
            var (selected, score) = difficulty.SelectCandidate(
                candidates,
                static candidate => candidate.Move != WizardAiMove.Wait,
                static candidate => ButtonFor(candidate.Move),
                candidate => IsDefensive(candidate, frame),
                (candidate, anticipationWeight) => ScoreReplies(candidate, frame, tuning, matchup, memory, anticipationWeight));
            if (selected == null) return Idle("Observe");
            return new(new AiAction(selected.Direction, ButtonFor(selected.Move)), selected.Move, score, selected.Reason);
        }

        static bool IsDefensive(WizardAiCandidate candidate, WizardAiFrame frame) {
            var toward = frame.Opponent.Body.center.x - frame.Self.Body.center.x;
            return candidate.Move == WizardAiMove.Shield
                || candidate.Move is WizardAiMove.Dash or WizardAiMove.Jump or WizardAiMove.Walk
                    && (candidate.Direction.x * toward <= 0f
                        || candidate.Direction.y > 0f && frame.Opponent.Body.center.y < frame.Self.Body.center.y + 2f);
        }

        int EnergyAtInput(WizardAiFrame frame) {
            var energy = frame.Self.Wizard.Energy;
            if (advancedTactics && frame.Self.State is Core.LargeWizard.ChargeState { IsTutorialActionCommitted: false }
                && frame.Self.StateSeconds + frame.ExecutionDelay >= frame.Self.Wizard.ChargeSeconds) energy = Mathf.Min(3, energy + 1);
            return energy;
        }

        void BuildCandidates(WizardAiFrame frame) {
            candidates.Clear();
            var toward = frame.Opponent.Position.x >= frame.Self.Position.x ? Vector2.right : Vector2.left;
            var energy = EnergyAtInput(frame);
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

        // Evaluate visible motion and both horizontal replies. No opponent input is read.
        float ScoreReplies(WizardAiCandidate candidate, WizardAiFrame frame, WizardAiTuning tuning,
            WizardAiMatchup matchup, WizardAiMemory memory, float anticipationWeight) {
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
        float CounterPressure(WizardAiFrame frame, float chance, float impact) {
            if (!advancedTactics || chance < .7f || impact > .25f
                || frame.Opponent.Striker.Striker.CurrentValue == Striker.Hero
                || frame.Opponent.Category is StrikerStateCategory.Special or StrikerStateCategory.Guard
                || frame.Opponent.State is Core.DarkSatan.Attack2State or Core.LargeSatan.Attack2State) return 1f;
            return Mathf.Lerp(1f, .3f, chance * Mathf.Clamp01((.3f - impact) / .25f));
        }

        Vector3 ReplyPosition(WizardAiFrame frame, Vector3 predicted, float seconds, WizardAiMemory memory) {
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

        float Score(WizardAiCandidate candidate, WizardAiFrame frame, WizardAiTuning tuning, WizardAiMatchup matchup,
            WizardAiMemory memory, float anticipationWeight) {
            var horizon = Mathf.Clamp(frame.BeatSeconds, .3f, 1.2f);
            var end = PredictSelf(candidate, frame, tuning, horizon);
            var enemy = PredictOpponent(frame, horizon + frame.ExecutionDelay, memory);
            var gap = Mathf.Abs(enemy.x - end.x);
            var startGap = Mathf.Abs(frame.Opponent.Body.center.x - frame.Self.Body.center.x);
            var preferred = matchup.PreferredDistance + (memory.AbHabit * predictionWeight) * 2f;
            var positionGain = Mathf.Abs(startGap - preferred) - Mathf.Abs(gap - preferred);
            var score = Mathf.Clamp(positionGain, -5f, 5f) * tuning.PositionWeight;
            var hpRatio = frame.Self.Striker.HitPoint.CurrentValue / Mathf.Max(1f, frame.Self.Striker.MaxHitPoint.CurrentValue);
            BuildSpellForecast(candidate, frame, tuning, anticipationWeight);
            BuildPressureForecast(candidate, frame, tuning, memory, anticipationWeight);
            var risk = ThreatRisk(candidate, frame, tuning, horizon, matchup.ProjectileCaution);
            var anticipated = AnticipationRisk(candidate, frame, tuning, matchup, memory, horizon);
            if (advancedTactics && candidate.Move is WizardAiMove.Melee or WizardAiMove.Fire or WizardAiMove.LaunchShield) {
                var impact = candidate.Move == WizardAiMove.Melee ? .05f : candidate.Move == WizardAiMove.Fire
                    ? frame.Self.Wizard.FireReleaseSeconds + startGap / Mathf.Max(.1f, frame.Self.Wizard.FirePrefab.ObservationSpeed)
                    : startGap / Mathf.Max(.1f, frame.Self.Wizard.ShieldSpeed);
                anticipated *= CounterPressure(frame, HitChance(candidate.Move, frame, tuning, memory, 0f), impact);
            }
            risk += anticipated * anticipationWeight;
            score -= risk * threatWeight * tuning.RiskWeight * (hpRatio < .3f ? 1.35f : 1f);
            score -= BoundaryCost(candidate, frame, tuning, horizon);

            var pending = frame.Self.Wizard.PendingReleaseSeconds(frame.Self.State) - frame.Self.StateSeconds - frame.ExecutionDelay;
            var hasPending = frame.Self.State is Core.LargeWizard.ChargeState or Core.LargeWizard.Attack1State or Core.LargeWizard.Attack2State or Core.LargeWizard.Attack3State;
            if (advancedTactics && frame.Self.State is ITutorialActionProgress { IsTutorialActionCommitted: true }) hasPending = false;
            // Exiting a state clears its scheduled spawn. Include execution delay, not just the observation time.
            if (hasPending && pending > -.025f && candidate.Move is not (WizardAiMove.Wait or WizardAiMove.Special)) score -= 27f;

            switch (candidate.Move) {
                case WizardAiMove.Melee:
                case WizardAiMove.Fire:
                case WizardAiMove.Ice:
                case WizardAiMove.Rock: {
                    var chance = HitChance(candidate.Move, frame, tuning, memory, 0f);
                    var damage = SpellDamage(candidate.Move, frame, tuning);
                    score += chance * Mathf.Min(damage, frame.Opponent.Striker.HitPoint.CurrentValue) * tuning.DamageWeight * matchup.Pressure;
                    score -= (1f - chance) * (9f + memory.WhiffPressure * 2f);
                    if (damage >= frame.Opponent.Striker.HitPoint.CurrentValue) score += chance * 14f;
                    break;
                }
                case WizardAiMove.Charge: {
                    var energy = EnergyAtInput(frame);
                    // Compare the value after one or two more charges. This makes rock reachable against shield stacks.
                    var chargeBeats = advancedTactics ? Mathf.Max(1f, Mathf.Ceil(frame.Self.Wizard.ChargeSeconds / frame.BeatSeconds)) : 1f;
                    var one = energy == 0 ? WizardAiMove.Fire : energy == 1 ? WizardAiMove.Ice : WizardAiMove.Rock;
                    var plan = HitChance(one, frame, tuning, memory, frame.BeatSeconds * chargeBeats) * SpellDamage(one, frame, tuning) / (1f + .7f * chargeBeats);
                    if (energy < 2) {
                        var two = energy == 0 ? WizardAiMove.Ice : WizardAiMove.Rock;
                        plan = Mathf.Max(plan, HitChance(two, frame, tuning, memory, frame.BeatSeconds * chargeBeats * 2f) * SpellDamage(two, frame, tuning) / (1f + 1.5f * chargeBeats));
                    }
                    score += plan + (energy == 0 ? 8f : 1f);
                    if (frame.OpponentShields.Count > 0 && energy >= 1) score += (memory.ShieldHabit * predictionWeight) * 6f;
                    if (energy == 0 && startGap < matchup.OpponentReach && frame.SelfShields.Count == 0) score -= 9f;
                    break;
                }
                case WizardAiMove.Shield:
                    score += frame.SelfShields.Count == 0 ? 6f : frame.SelfShields.Count == 1 ? 2f : -3f;
                    score -= Mathf.Min(memory.GuardStreak, 5) * 1.5f * (1f - (memory.ShieldHabit * predictionWeight) * .8f);
                    if (startGap < 5f && Mathf.Abs(enemy.y - end.y) < 2f) score += 4f + (memory.AttackHabit * predictionWeight) * 4f;
                    // Sustain a shield stock and the beat combo until the beam can break a renewing shield wall.
                    if (frame.OpponentShields.Count > 0) score += (memory.ShieldHabit * predictionWeight) * (5f + Mathf.Max(0, 3 - frame.SelfShields.Count) * 3f);
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
                    score -= 3f + (memory.ShieldHabit * predictionWeight) * 8f;
                    break;
                case WizardAiMove.Wait:
                    score -= 8f + Mathf.Min(8, memory.StaleBeats);
                    if (hasPending && pending > -.025f) score += 25f;
                    break;
            }
            score -= followUpWeight * memory.ResponsePenalty(candidate.Move, candidate.Direction, frame);
            // Reward a safe position that can convert into an attack on the next beat.
            if (followUpWeight > 0f) score += followUpWeight * FollowUpValue(candidate, frame, tuning, memory) / (1f + risk * 2f);
            return score;
        }

        float FollowUpValue(WizardAiCandidate candidate, WizardAiFrame frame, WizardAiTuning tuning, WizardAiMemory memory) {
            if (candidate.Move is not (WizardAiMove.Dash or WizardAiMove.Jump or WizardAiMove.Walk)
                || frame.Self.Wizard.Energy >= 2) return 0f;
            var at = frame.BeatSeconds;
            var self = PredictSelf(candidate, frame, tuning, at);
            var target = PredictOpponent(frame, at + frame.ExecutionDelay, memory);
            var origin = frame.Self.Wizard.FireOrigin + self - frame.Self.Body.center;
            var delta = target - origin;
            var radius = frame.Self.Wizard.FirePrefab.ObservationRadius;
            var inLane = Mathf.Clamp01((radius + frame.Opponent.Body.extents.y - Mathf.Abs(delta.y)) / 1f);
            var spacing = Mathf.Clamp01((Mathf.Abs(delta.x) - tuning.MeleeReach) / 3f);
            if (Vector3.Dot(delta, frame.Self.Striker.LookDirection.CurrentValue) < 0f) return 0f;
            var currentOrigin = frame.Self.Wizard.FireOrigin;
            var currentLane = Mathf.Clamp01(radius + frame.Opponent.Body.extents.y - Mathf.Abs(target.y - currentOrigin.y));
            var currentSpacing = Mathf.Clamp01((Mathf.Abs(target.x - currentOrigin.x) - tuning.MeleeReach) / 3f);
            var currentValue = currentLane * currentSpacing * ShieldTransmission(currentOrigin, target, radius, frame, at);
            if (Vector3.Dot(target - currentOrigin, frame.Self.Striker.LookDirection.CurrentValue) < 0f) currentValue = 0f;
            return Mathf.Max(0f, inLane * spacing * ShieldTransmission(origin, target, radius, frame, at) - currentValue) * 12f;
        }

        float SpellDamage(WizardAiMove move, WizardAiFrame frame, WizardAiTuning tuning) {
            if (!advancedTactics) return Damage(move, tuning);
            var source = frame.Self.Wizard;
            return move switch {
                WizardAiMove.Fire => source.FirePrefab.ObservationDamage,
                WizardAiMove.Ice => source.IcePrefab.ObservationDamage,
                WizardAiMove.Rock => source.RockPrefab.ObservationDamage,
                WizardAiMove.LaunchShield => source.ShieldDamage,
                _ => 5f
            };
        }

        static float Damage(WizardAiMove move, WizardAiTuning tuning) => move switch {
            WizardAiMove.Fire => tuning.FireDamage,
            WizardAiMove.Ice => tuning.IceDamage,
            WizardAiMove.Rock => tuning.RockDamage,
            _ => 5f
        };

        float HitChance(WizardAiMove move, WizardAiFrame frame, WizardAiTuning tuning, WizardAiMemory memory, float preparation) {
            var source = frame.Self.Wizard;
            var enemy = frame.Opponent;
            var origin = source.FireOrigin + frame.Self.Velocity * (preparation + frame.ExecutionDelay);
            var look = frame.Self.Striker.LookDirection.CurrentValue.normalized;
            if (move == WizardAiMove.Ice) {
                var ice = source.IcePrefab;
                var spawn = preparation + frame.ExecutionDelay + source.IceReleaseSeconds;
                var target = PredictOpponent(frame, spawn, memory) + enemy.Position - enemy.Body.center;
                target.y = enemy.Arena?.min.y ?? enemy.Body.min.y;
                var column = ice.ObserveSpawnBounds(target);
                var chance = 0f;
                // A dodge can expire before the ice. Judge the whole lifetime, including re-entry after landing.
                for (var i = 0; i <= 16; i++) {
                    var hitAt = spawn + Mathf.Lerp(ice.ObservationActivationDelay, ice.ObservationLifetime, i / 16f);
                    var atHit = PredictOpponent(frame, hitAt, memory);
                    var delta = atHit - column.center;
                    var overlap = Mathf.Clamp01((column.extents.x + enemy.Body.extents.x - Mathf.Abs(delta.x)) / .8f)
                        * Mathf.Clamp01((column.extents.y + enemy.Body.extents.y - Mathf.Abs(delta.y)) / .7f);
                    chance = Mathf.Max(chance, overlap * Vulnerability(enemy, hitAt));
                }
                foreach (var shield in frame.OpponentShields) {
                    if (shield.Bounds.Intersects(column)) { chance *= .12f; break; }
                }
                return chance;
            }
            if (move == WizardAiMove.Rock) {
                var spawn = preparation + frame.ExecutionDelay + source.RockReleaseSeconds;
                var target = PredictOpponent(frame, spawn, memory);
                var rockBounds = source.RockPrefab.ObserveSpawnBounds(target + enemy.Position - enemy.Body.center + Vector3.up * source.RockSpawnHeight);
                var fallHeight = advancedTactics ? rockBounds.min.y - target.y - enemy.Body.extents.y
                    : source.RockSpawnHeight - enemy.Body.extents.y - tuning.RockRadius;
                var fall = Mathf.Sqrt(2f * Mathf.Max(.1f, fallHeight) / Mathf.Max(.01f, -Physics.gravity.y));
                var hitAt = spawn + fall;
                var atHit = PredictOpponent(frame, hitAt, memory);
                if (advancedTactics && fall > source.RockPrefab.ObservationLifetime) return 0f;
                var rockRadius = advancedTactics ? rockBounds.extents.x : tuning.RockRadius;
                var chance = Mathf.Clamp01((rockRadius + enemy.Body.extents.x - Mathf.Abs(atHit.x - target.x)) / 1.3f);
                var above = target + Vector3.up * source.RockSpawnHeight;
                chance *= ShieldTransmission(above, atHit, rockRadius, frame, hitAt);
                return chance * Vulnerability(enemy, hitAt) * (1f - (memory.AttackHabit * predictionWeight) * .18f);
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
            var speed = move == WizardAiMove.LaunchShield ? source.ShieldSpeed : advancedTactics ? source.FirePrefab.ObservationSpeed : tuning.FireSpeed;
            var release = move == WizardAiMove.Fire ? source.FireReleaseSeconds : 0f;
            var flight = Vector3.Distance(origin, enemy.Body.center) / Mathf.Max(.1f, speed);
            var impact = preparation + frame.ExecutionDelay + release + flight;
            var aim = PredictOpponent(frame, impact, memory) - origin;
            var forward = Vector3.Dot(aim, look);
            if (forward < 0f) return 0f;
            var miss = (aim - look * forward).magnitude;
            if (advancedTactics && move == WizardAiMove.Fire && flight > source.FireLifetime) return 0f;
            var radius = move == WizardAiMove.LaunchShield ? tuning.ShieldRadius : advancedTactics ? source.FirePrefab.ObservationRadius : tuning.FireRadius;
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

        float Vulnerability(WizardAiActor actor, float future) {
            future *= predictionWeight;
            if (actor.Category == StrikerStateCategory.Special) return .05f;
            if (actor.Category == StrikerStateCategory.Guard && actor.Striker.Striker.CurrentValue is Striker.Warrior or Striker.DemonKing) {
                var guardSeconds = (actor.Striker as IStrikerCombatObservationSource)?.ActiveGuardSeconds ?? 1f;
                return actor.StateSeconds + future < guardSeconds ? .05f : 1f;
            }
            return 1f;
        }

        void BuildSpellForecast(WizardAiCandidate candidate, WizardAiFrame frame, WizardAiTuning tuning, float anticipationWeight) {
            forecast.Clear();
            var wizard = frame.Opponent.Wizard;
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
                forecast.Add(new WizardAiThreat(ice.ObserveSpawnBounds(target), Vector3.zero, Vector3.zero,
                    spawnTime + ice.ObservationActivationDelay, spawnTime + ice.ObservationLifetime,
                    ice.ObservationDamage / 10f, true, true, Confidence: weight));
            } else if (spell == 3) {
                var rock = wizard.RockPrefab;
                target += Vector3.up * wizard.RockSpawnHeight;
                forecast.Add(new WizardAiThreat(rock.ObserveSpawnBounds(target), Vector3.zero, Physics.gravity,
                    spawnTime, spawnTime + rock.ObservationLifetime, rock.ObservationDamage / 10f, true, Confidence: weight));
            } else {
                var fire = wizard.FirePrefab;
                var origin = wizard.FireOrigin + enemy.Velocity * Mathf.Min(spawnTime, .12f);
                forecast.Add(new WizardAiThreat(new Bounds(origin, Vector3.one * fire.ObservationRadius * 2f),
                    enemy.Striker.LookDirection.CurrentValue.normalized * fire.ObservationSpeed, Vector3.zero,
                    spawnTime, spawnTime + wizard.FireLifetime, fire.ObservationDamage / 10f, true, Confidence: weight));
            }
        }

        void BuildPressureForecast(WizardAiCandidate candidate, WizardAiFrame frame, WizardAiTuning tuning,
            WizardAiMemory memory, float anticipationWeight) {
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
            var wizard = enemy.Wizard;
            var firstCommand = enemy.State is Core.LargeHero.GuardState guard
                ? Mathf.Ceil(Mathf.Max(0f, guard.ObservationDuration - enemy.StateSeconds - frame.ExecutionDelay) / frame.BeatSeconds) : 0f;
            var guardDuration = frame.BeatSeconds;
            var commands = Mathf.Clamp(Mathf.CeilToInt(guardDuration / frame.BeatSeconds) + 1, 2, 5);
            var launchIndex = -1;
            var nearestLaunch = float.PositiveInfinity;
            if (wizard != null) {
                for (var i = 0; i < frame.OpponentShields.Count; i++) {
                    var bounds = frame.OpponentShields[i].Bounds;
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
                var origin = pressureOrigin ?? (PredictOpponent(frame, commandAt, memory));
                var target = PredictSelf(candidate, frame, tuning, frame.BeatSeconds * beat);
                var toward = target.x >= origin.x ? Vector3.right : Vector3.left;
                if (hero != null && !hero.UsesAirInputs(enemy.State)
                    && enemy.State is not (Core.LargeHero.Slash1State or Core.LargeHero.Slash2State or Core.LargeHero.Slash3State)) {
                    var offset = hero.RushOrigin - enemy.Body.center;
                    offset.x *= toward.x * Mathf.Sign(enemy.Striker.LookDirection.CurrentValue.x);
                    var bounds = hero.RushBounds(origin + offset, toward);
                    var lifetime = hero.RushProjectileLifetimeSeconds ?? frame.BeatSeconds * 2f;
                    forecast.Add(new WizardAiThreat(bounds, toward * hero.RushProjectileSpeed, Vector3.zero,
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
                    forecast.Add(new WizardAiThreat(bounds, facing * (attack.ObservationSpeed * Mathf.Exp(-decay * hit.x)), Vector3.zero,
                        commandAt + hit.x, commandAt + hit.y, attack.ObservationDamage / 10f, false,
                        Confidence: weight * Mathf.Lerp(.25f, .95f, memory.MeleeHabit), MotionDecay: decay, AnchoredAtStart: true));
                    hasMeleeForecast = true;
                    var step = decay > .0001f ? (1f - Mathf.Exp(-decay * frame.BeatSeconds)) / decay : frame.BeatSeconds;
                    pressureOrigin = ClampToArena(origin + facing * (attack.ObservationSpeed * step), enemy);
                    pressureFacing = facing;
                    nextMelee = attack.ObservationNextAttack ?? memory.MeleeOpener;
                } else if (wizard != null && launchIndex >= 0 && (beat == 0 || frame.OpponentShields.Count >= 3)) {
                    // Stacking three shields can replenish and launch on every beat, even with neutral input.
                    var bounds = frame.OpponentShields[launchIndex].Bounds;
                    bounds.center += origin - enemy.Body.center;
                    forecast.Add(new WizardAiThreat(bounds, enemy.Striker.LookDirection.CurrentValue.normalized * wizard.ShieldSpeed,
                        Vector3.zero, commandAt, commandAt + wizard.ShieldLifetime, Mathf.Max(.25f, wizard.ShieldDamage / 10f), true,
                        Confidence: weight * Mathf.Lerp(.2f, .95f, Mathf.Max(memory.ShieldLaunchHabit, memory.ShieldHabit * .65f))));
                }
            }
        }

        float ThreatRisk(WizardAiCandidate candidate, WizardAiFrame frame, WizardAiTuning tuning, float horizon, float caution) {
            if (candidate.Move == WizardAiMove.Special) return .03f;
            PrepareShields(candidate, frame, tuning);
            consumedShields.Clear();
            var risk = 0f;
            for (var threatIndex = 0; threatIndex < frame.Threats.Count + forecast.Count; threatIndex++) {
                var threat = threatIndex < frame.Threats.Count ? frame.Threats[threatIndex] : forecast[threatIndex - frame.Threats.Count];
                var start = Mathf.Max(frame.ExecutionDelay, threat.StartsAt);
                var exposureHorizon = horizon + frame.BeatSeconds * followUpWeight * .5f;
                if (threat.GroundHazard) exposureHorizon = Mathf.Max(exposureHorizon, frame.BeatSeconds * (1f + followUpWeight));
                var end = Mathf.Min(threat.EndsAt, frame.ExecutionDelay + exposureHorizon);
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
                if (!threat.GroundHazard && threat.Severity < 2f) {
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
                risk += threat.Severity * threat.Confidence * (threat.Projectile ? caution : 1f) * (blocked ? .12f : 1f);
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
            var moving = threat.Projectile || threat.AnchoredAtStart ? Mathf.Max(0f, t - threat.StartsAt) : t;
            var travel = threat.MotionDecay > .0001f ? (1f - Mathf.Exp(-threat.MotionDecay * moving)) / threat.MotionDecay : moving;
            return threat.Bounds.center + threat.Velocity * travel + threat.Acceleration * (moving * moving * .5f);
        }

        float AnticipationRisk(WizardAiCandidate candidate, WizardAiFrame frame, WizardAiTuning tuning,
            WizardAiMatchup matchup, WizardAiMemory memory, float horizon) {
            if (hasMeleeForecast || hasRushForecast) return 0f;
            if (candidate.Move == WizardAiMove.Special) return 0f;
            var enemy = frame.Opponent;
            if (enemy.State is Core.LargeHero.StunState or Core.LargeWizard.StunState or Core.LargeWizard.StunCancelState
                or Core.LargeSatan.StunState or Core.LargeSatan.StunCancelState) return 0f;
            var recoveryDelay = 0f;
            if (enemy.State is Core.DarkSatan.StunState stun) {
                if (!stun.ObservationAutoRecovers) return 0f;
                recoveryDelay = Mathf.Max(0f, stun.ObservationDuration - enemy.StateSeconds - frame.ExecutionDelay);
            } else if (enemy.State is Core.DarkSatan.StunCancelState cancel) {
                recoveryDelay = Mathf.Max(0f, cancel.ObservationDuration - enemy.StateSeconds - frame.ExecutionDelay);
            }
            var risk = 0f;
            var spearRisk = 0f;
            for (var i = 1; i <= 8; i++) {
                var t = horizon * i / 8f;
                if (t < recoveryDelay) continue;
                var self = PredictSelf(candidate, frame, tuning, t);
                var opponent = PredictOpponent(frame, t + frame.ExecutionDelay, memory);
                var vertical = Mathf.Clamp01((2.8f - Mathf.Abs(self.y - opponent.y)) / 1.5f);
                var reach = matchup.OpponentReach;
                var aggression = .12f + (memory.AttackHabit * predictionWeight) * .25f;
                if (enemy.Striker.Striker.CurrentValue == Striker.Warrior) {
                    reach = tuning.WarriorLungeReach;
                    aggression += (memory.AbHabit * predictionWeight) * .4f;
                    if (enemy.State is Core.LargeSatan.ChargeState charge && charge.ObservedEmitState != null) {
                        var direction = charge.ObservedEmitState.ObserveThrowDirection(Vector2.zero, enemy.Striker.LookDirection.CurrentValue);
                        var spear = charge.ObservedPoleArm.transform.position + direction * charge.ObservedEmitState.ObserveTravelDistance(t);
                        var lane = new Bounds(Vector3.zero, frame.Self.Body.size + Vector3.one * .8f);
                        if (SegmentIntersects(lane, charge.ObservedPoleArm.transform.position - self, spear - self)) spearRisk += (.9f + (memory.AbHabit * predictionWeight) * .5f) / 8f;
                    }
                    if (enemy.State is Core.LargeSatan.EmitState) {
                        reach = tuning.WarriorWarpReach;
                        aggression = .7f + (memory.AbHabit * predictionWeight) * .65f;
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
                    aggression = Mathf.Max(aggression, .6f + (memory.AttackHabit * predictionWeight) * 1.4f);
                }
                var protection = frame.SelfShields.Count > 0 ? .65f : 1f;
                if (candidate.Move == WizardAiMove.Shield && t >= tuning.ShieldGenerationSeconds) protection *= .55f;
                // Shields only cover the facing side. A warp through or behind them bypasses this protection.
                if (Vector3.Dot(opponent - self, frame.Self.Striker.LookDirection.CurrentValue) < 0f) protection = 1f;
                var approachRisk = proximity * aggression * protection;
                if (enemy.Striker.Striker.CurrentValue is Striker.Hero or Striker.DemonKing) risk = Mathf.Max(risk, approachRisk);
                else risk += approachRisk / 8f;

            }
            return risk + spearRisk;
        }

        Vector3 PredictOpponent(WizardAiFrame frame, float seconds, WizardAiMemory memory) {
            var enemy = frame.Opponent;
            seconds *= predictionWeight;
            if (advancedTactics && enemy.State is Core.LargeSatan.EmitState warpState && enemy.Striker is IStrikerContext context) {
                var destination = warpState.ObserveWarpPosition(context, Mathf.Min(seconds, frame.ExecutionDelay));
                if (destination.HasValue) return ReplyPosition(frame, ClampToArena(destination.Value + enemy.Body.center - enemy.Position, enemy), seconds, memory);
            }
            if (!advancedTactics && predictionWeight > .5f && enemy.State is Core.LargeSatan.EmitState emit) {
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
            return ReplyPosition(frame, ClampToArena(enemy.Body.center + movement, enemy), seconds, memory);
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
