using System.Collections.Generic;
using UnityEngine;

namespace Alice.HeroAi {
    internal enum HeroAiMove { Wait, Walk, Rush, Slash, Beam, Charge, Move, Guard, Special, Recover }
    internal sealed record HeroAiChoice(AiAction Action, HeroAiMove Move, float Score, string Reason);
    internal sealed record HeroAiCandidate(HeroAiMove Move, Vector2 Direction, string Reason);

    internal sealed class HeroAiDecision {
        bool advancedTactics;
        int opponentReply;
        float predictionWeight = 1f;
        float followUpWeight = 1f;
        float threatWeight = 1f;

        readonly List<HeroAiThreat> forecast = new(8);
        bool hasMeleeForecast, hasRushForecast;
        readonly List<HeroAiCandidate> candidates = new(16);

        public HeroAiChoice Choose(HeroAiFrame frame, HeroAiTuning tuning, HeroAiMatchup matchup, HeroAiMemory memory, AiDifficulty difficulty) {
            advancedTactics = difficulty.UsesAdvancedTactics;
            opponentReply = 0;
            predictionWeight = difficulty.PredictionWeight;
            followUpWeight = difficulty.FollowUpWeight;
            threatWeight = difficulty.ThreatWeight;
            var self = frame.Self;
            if (self.Striker.HitPoint.CurrentValue <= 0f || frame.Opponent.Striker.HitPoint.CurrentValue <= 0f) return Idle("Round ended");
            if (difficulty.MissesBeat()) return Idle("Missed the beat");
            // These authored states have no normal input parent. A rhythm input cannot interrupt them.
            if (self.Category == StrikerStateCategory.Special) return Pulse("Complete the special sequence");
            if (self.State is Core.LargeHero.GuardState && self.StateSeconds + frame.ExecutionDelay < self.Hero.GuardSeconds - .025f)
                return predictionWeight < .5f ? Idle("Finish the shield animation")
                    : new(new AiAction(Vector2.zero, GamePadButton.North), HeroAiMove.Guard, 0f, "Maintain the shield and rhythm");
            if (self.State is Core.LargeSHero.TurnState turn && self.StateSeconds + frame.ExecutionDelay < turn.ObservationDuration + .025f)
                return Idle("Finish turning toward the opponent");
            if (self.State is Core.LargeHero.StunState)
                return new(new AiAction(Vector2.zero, GamePadButton.North), HeroAiMove.Recover, 0f, "Break hit stun");

            BuildCandidates(frame);
            var (selected, score) = difficulty.SelectCandidate(
                candidates,
                static candidate => candidate.Move != HeroAiMove.Wait,
                static candidate => ButtonFor(candidate.Move),
                candidate => IsDefensive(candidate, frame),
                (candidate, anticipationWeight) => ScoreReplies(candidate, frame, tuning, matchup, memory, anticipationWeight));
            if (selected == null) return Idle("Observe");
            return new(new AiAction(selected.Direction, ButtonFor(selected.Move)), selected.Move, score, selected.Reason);
        }

        static bool IsDefensive(HeroAiCandidate candidate, HeroAiFrame frame) {
            var toward = frame.Opponent.Body.center.x - frame.Self.Body.center.x;
            return candidate.Move == HeroAiMove.Guard
                || candidate.Move is HeroAiMove.Move or HeroAiMove.Walk
                    && (candidate.Direction.x * toward <= 0f
                        || candidate.Direction.y > 0f && frame.Opponent.Body.center.y < frame.Self.Body.center.y + 2f);
        }

        void BuildCandidates(HeroAiFrame frame) {
            candidates.Clear();
            var self = frame.Self;
            var source = self.Hero;
            var toward = frame.Opponent.Position.x >= self.Position.x ? Vector2.right : Vector2.left;
            var energy = source.Energy;
            if (advancedTactics && self.State is Core.LargeHero.ChargeState { IsTutorialActionCommitted: false }
                && self.StateSeconds + frame.ExecutionDelay >= source.ChargeSeconds) energy++;
            var combo = self.State is Core.LargeHero.Slash1State or Core.LargeHero.Slash2State or Core.LargeHero.Slash3State;
            var air = source.UsesAirInputs(self.State);
            if (combo || energy == 0 || (!air && energy > 1))
                Add(HeroAiMove.Slash, Vector2.zero, combo ? "Continue the sword combo" : air ? "Cut through the air opening" : "Strike at close range");
            if (!combo) {
                Add(HeroAiMove.Rush, toward, "Rush into the opening");
                Add(HeroAiMove.Rush, -toward, "Cover the retreat with a rush strike");
                // Jump/Fall also dispatch their own AttackNode before their AirGroup; a stored beam is consumed there.
                if ((air ? energy > 0 : energy == 1) && (advancedTactics || !air || self.State is Core.LargeHero.AirDashState))
                    Add(HeroAiMove.Beam, Vector2.zero, "Fire the charged sword wave");
            }
            if (!air && energy == 0) Add(HeroAiMove.Charge, Vector2.zero, "Prepare a ranged sword wave");
            Add(HeroAiMove.Guard, Vector2.zero, "Block the incoming attack");
            Add(HeroAiMove.Move, -toward, "Withdraw from the attack lane");
            Add(HeroAiMove.Move, toward, "Cross pressure and reposition");
            Add(HeroAiMove.Move, (-toward + Vector2.up).normalized, "Evade spear and pursuit diagonally");
            Add(HeroAiMove.Move, (toward + Vector2.up).normalized, "Approach above the shield line");
            Add(HeroAiMove.Move, Vector2.up, "Rise above ground pressure");
            if (air) Add(HeroAiMove.Move, Vector2.down, "Land outside the attack");
            if (!air) {
                Add(HeroAiMove.Walk, toward, "Adjust close-range spacing");
                Add(HeroAiMove.Walk, -toward, "Bait an attack outside its reach");
            }
            var maximum = self.Striker.MaxSpecialPoint.CurrentValue;
            if (maximum > 0f && self.Striker.SpecialPoint.CurrentValue >= maximum - .001f)
                Add(HeroAiMove.Special, Vector2.zero, "Catch the opponent in the aerial special");
            Add(HeroAiMove.Wait, Vector2.zero, "Finish the pending move");
        }

        void Add(HeroAiMove move, Vector2 direction, string reason) => candidates.Add(new(move, direction, reason));

        // Evaluate visible motion and both horizontal replies. No opponent input is read.
        float ScoreReplies(HeroAiCandidate candidate, HeroAiFrame frame, HeroAiTuning tuning,
            HeroAiMatchup matchup, HeroAiMemory memory, float anticipationWeight) {
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
        float CounterPressure(HeroAiFrame frame, float chance, float impact) {
            if (!advancedTactics || chance < .7f || impact > .25f
                || frame.Opponent.Striker.Striker.CurrentValue == Striker.Hero
                || frame.Opponent.Category is StrikerStateCategory.Special or StrikerStateCategory.Guard
                || frame.Opponent.State is Core.DarkSatan.Attack2State or Core.LargeSatan.Attack2State) return 1f;
            return Mathf.Lerp(1f, .3f, chance * Mathf.Clamp01((.3f - impact) / .25f));
        }

        Vector3 ReplyPosition(HeroAiFrame frame, Vector3 predicted, float seconds, HeroAiMemory memory) {
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

        float Score(HeroAiCandidate candidate, HeroAiFrame frame, HeroAiTuning tuning, HeroAiMatchup matchup,
            HeroAiMemory memory, float anticipationWeight) {
            var horizon = Mathf.Clamp(frame.BeatSeconds, .3f, 1.2f);
            var end = PredictSelf(candidate, frame, tuning, horizon);
            var enemy = PredictOpponent(frame, horizon + frame.ExecutionDelay, memory);
            var startGap = Mathf.Abs(frame.Opponent.Body.center.x - frame.Self.Body.center.x);
            var gap = Mathf.Abs(enemy.x - end.x);
            var gain = Mathf.Abs(startGap - matchup.PreferredDistance) - Mathf.Abs(gap - matchup.PreferredDistance);
            var altitudeGap = Mathf.Abs(frame.Opponent.Body.center.y - frame.Self.Body.center.y);
            // Match altitude before chasing an opponent that is warping horizontally far above us.
            var altitudeFirst = altitudeGap > 4f;
            var score = Mathf.Clamp(gain, -6f, 6f) * tuning.PositionWeight * matchup.Pressure * (altitudeFirst ? .15f : 1f);
            var verticalGain = Mathf.Abs(frame.Opponent.Body.center.y - frame.Self.Body.center.y) - Mathf.Abs(enemy.y - end.y);
            score += Mathf.Clamp(verticalGain, -4f, 4f) * (altitudeFirst ? 8f : 2.2f);
            BuildSpellForecast(candidate, frame, tuning, anticipationWeight);
            BuildPressureForecast(candidate, frame, tuning, memory, anticipationWeight);
            var risk = ThreatRisk(candidate, frame, tuning, horizon, matchup.ProjectileCaution);
            var anticipated = AnticipationRisk(candidate, frame, tuning, matchup, memory, horizon);
            // A fast sword wave can stop an approaching body attack before its lunge reaches us.
            // Already emitted hitboxes, persistent Hero rush waves and armoured finishers retain their full danger.
            if (candidate.Move == HeroAiMove.Rush && frame.Opponent.Striker.Striker.CurrentValue != Striker.Hero
                && frame.Opponent.State is not Core.DarkSatan.Attack2State
                && (!advancedTactics || frame.Opponent.State is not Core.LargeSatan.Attack2State)
                && frame.Opponent.Category != StrikerStateCategory.Special
                && startGap < tuning.RushReach + frame.Self.Hero.RushProjectileSpeed * horizon * .35f
                && HitChance(candidate, frame, tuning, memory) > .7f) anticipated *= .3f;
            if (advancedTactics && candidate.Move is HeroAiMove.Slash or HeroAiMove.Beam) {
                var chance = HitChance(candidate, frame, tuning, memory, out var impact);
                anticipated *= CounterPressure(frame, chance, impact);
            }
            risk += anticipated * anticipationWeight;
            if (candidate.Move == HeroAiMove.Guard) risk *= 1f + Mathf.Min(3f, memory.RecentGuardDamage * .15f);
            var hpRatio = frame.Self.Striker.HitPoint.CurrentValue / Mathf.Max(1f, frame.Self.Striker.MaxHitPoint.CurrentValue);
            var maximum = frame.Self.Striker.MaxSpecialPoint.CurrentValue;
            var specialReady = maximum > 0f && frame.Self.Striker.SpecialPoint.CurrentValue >= maximum - .001f;
            score -= risk * threatWeight * tuning.RiskWeight * (hpRatio < .3f ? 1.3f : 1f) * (frame.Opponent.Striker.Striker.CurrentValue == Striker.Hero ? 1.4f : 1f);
            score -= BoundaryCost(candidate, frame, tuning, horizon);

            var pending = frame.Self.State is Core.LargeHero.ChargeState && frame.Self.Hero.Energy == 0 ? frame.Self.Hero.ChargeSeconds
                : frame.Self.State is Core.LargeHero.BeamState ? frame.Self.Hero.BeamReleaseSeconds : 0f;
            if (pending > 0f && frame.Self.StateSeconds + frame.ExecutionDelay < pending + .025f) {
                var continuing = candidate.Move == HeroAiMove.Wait || candidate.Move == HeroAiMove.Charge && frame.Self.State is Core.LargeHero.ChargeState;
                if (continuing) score += 20f; else if (candidate.Move != HeroAiMove.Special) score -= 25f;
            }
            switch (candidate.Move) {
                case HeroAiMove.Slash:
                case HeroAiMove.Rush:
                case HeroAiMove.Beam: {
                    var chance = HitChance(candidate, frame, tuning, memory);
                    var damage = candidate.Move == HeroAiMove.Slash ? frame.Self.Hero.NextMeleeDamage(frame.Self.State)
                        : candidate.Move == HeroAiMove.Rush ? frame.Self.Hero.RushDamage : frame.Self.Hero.BeamDamage;
                    score += chance * Mathf.Min(damage, frame.Opponent.Striker.HitPoint.CurrentValue) * tuning.DamageWeight * matchup.Pressure;
                    score -= (1f - chance) * (7f + memory.WhiffPressure * 1.5f);
                    if (damage >= frame.Opponent.Striker.HitPoint.CurrentValue) score += chance * 14f;
                    // Destroying a shield creates an opening even if it absorbs this hit.
                    if (frame.OpponentShields.Count > 0 && startGap < 8f && candidate.Move is HeroAiMove.Slash or HeroAiMove.Rush)
                        score += 6f * (memory.ShieldHabit * predictionWeight);
                    if (candidate.Move == HeroAiMove.Slash) score -= memory.WhiffPressure * Mathf.Min(5f, memory.StaleBeats * .4f);
                    // At the first close encounter, leave room to observe a possible instant shield deployment.
                    if (candidate.Move == HeroAiMove.Rush && frame.Opponent.Wizard != null && memory.DecisionCount == 1 && startGap < 8f)
                        score -= tuning.RiskWeight * .7f;
                    break;
                }
                case HeroAiMove.Charge:
                    score += startGap > 7f && Mathf.Abs(enemy.y - end.y) < 2.5f ? 10f : -7f;
                    score -= frame.OpponentShields.Count * 3f;
                    score -= (memory.ShieldHabit * predictionWeight) * 25f;
                    break;
                case HeroAiMove.Guard:
                    var canBank = !specialReady && (memory.ShieldHabit * predictionWeight) > .5f;
                    foreach (var shield in frame.OpponentShields) if (shield.Bounds.Intersects(frame.Self.Body)) canBank = false;
                    score -= 2f + memory.RecentGuardDamage * 2f;
                    if (!canBank) score -= Mathf.Min(memory.GuardStreak, 5) * 1.2f + Mathf.Min(memory.StaleBeats, 15) * .8f;
                    if (frame.Opponent.Category is StrikerStateCategory.Attack or StrikerStateCategory.Dash) score += 2f;
                    if (canBank) score += 20f;
                    if (frame.Opponent.Wizard != null && memory.DecisionCount == 1 && startGap < 8f) score -= 10f;
                    break;
                case HeroAiMove.Special: {
                    var chance = SpecialChance(frame, memory);
                    if (chance < .15f) return float.NegativeInfinity;
                    score += tuning.SpecialScore * chance - (1f - chance) * 30f;
                    break;
                }
                case HeroAiMove.Move:
                    score -= 4f;
                    if (end.y > enemy.y + 2f) score -= (end.y - enemy.y - 2f) * 1.1f;
                    if (frame.OpponentShields.Count > 0 && candidate.Direction.y > 0f && gap < startGap) score += 3f + (memory.ShieldHabit * predictionWeight) * 4f;
                    if (frame.OpponentShields.Count > 0 && gap < startGap) score += Mathf.Min(8f, memory.StaleBeats * .7f);
                    break;
                case HeroAiMove.Walk:
                    score -= 4f;
                    break;
                case HeroAiMove.Wait:
                    score -= 7f + Mathf.Min(memory.StaleBeats, 8);
                    break;
            }
            score -= followUpWeight * memory.ResponsePenalty(candidate.Move, candidate.Direction, frame);
            // Reward a safe position that can convert into an attack on the next beat.
            if (followUpWeight > 0f) score += followUpWeight * FollowUpValue(candidate, frame, tuning, memory) / (1f + risk * 2f);
            return score;
        }

        float FollowUpValue(HeroAiCandidate candidate, HeroAiFrame frame, HeroAiTuning tuning, HeroAiMemory memory) {
            if (advancedTactics) return AdvancedFollowUp(candidate, frame, tuning, memory);
            if (candidate.Move is HeroAiMove.Wait or HeroAiMove.Charge or HeroAiMove.Special) return 0f;
            var at = frame.BeatSeconds;
            var self = PredictSelf(candidate, frame, tuning, at);
            var target = PredictOpponent(frame, at + frame.ExecutionDelay, memory);
            var delta = target - self;
            var reach = tuning.RushReach + frame.Self.Hero.RushProjectileSpeed * .2f;
            var opening = Mathf.Clamp01((reach - Mathf.Abs(delta.x)) / 3f)
                * Mathf.Clamp01((2f + frame.Opponent.Body.extents.y - Mathf.Abs(delta.y)) / 1.5f);
            return opening * 14f * ShieldTransmission(self, target, .1f, frame)
                * Vulnerability(frame.Opponent, at + frame.ExecutionDelay + .1f);
        }

        float AdvancedFollowUp(HeroAiCandidate candidate, HeroAiFrame frame, HeroAiTuning tuning, HeroAiMemory memory) {
            if (candidate.Move is HeroAiMove.Wait or HeroAiMove.Special) return 0f;
            var source = frame.Self.Hero;
            var ready = candidate.Move == HeroAiMove.Guard ? source.GuardSeconds
                : candidate.Move == HeroAiMove.Charge ? source.ChargeSeconds : 0f;
            var at = frame.BeatSeconds * Mathf.Max(1f, Mathf.Ceil(ready / frame.BeatSeconds));
            var self = PredictSelf(candidate, frame, tuning, at);
            var origin = (candidate.Move == HeroAiMove.Charge ? source.BeamOrigin : source.RushOrigin) + self - frame.Self.Body.center;
            var speed = candidate.Move == HeroAiMove.Charge ? source.BeamSpeed : source.RushProjectileSpeed;
            var release = candidate.Move == HeroAiMove.Charge ? source.BeamReleaseSeconds : 0f;
            var target = PredictOpponent(frame, at + frame.ExecutionDelay + release, memory);
            var travel = Mathf.Abs(target.x - origin.x) / Mathf.Max(.1f, speed);
            target = PredictOpponent(frame, at + frame.ExecutionDelay + release + travel, memory);
            var look = frame.Self.Striker.LookDirection.CurrentValue;
            if (Vector3.Dot(target - origin, look) <= 0f) return 0f;
            if (candidate.Move != HeroAiMove.Charge && source.RushProjectileLifetimeSeconds.HasValue
                && travel > source.RushProjectileLifetimeSeconds.Value) return 0f;
            var radius = candidate.Move == HeroAiMove.Charge ? tuning.BeamRadius : source.RushBounds(origin, look).extents.y;
            var chance = Mathf.Clamp01((radius + frame.Opponent.Body.extents.y - Mathf.Abs(target.y - origin.y)) / .8f);
            // A slash remains in the melee combo: its next input cannot select a rush wave.
            if (candidate.Move == HeroAiMove.Slash) {
                var melee = source.NextMeleeBounds(frame.Self.State);
                var delta = target - self;
                chance = Mathf.Clamp01((melee.extents.x + frame.Opponent.Body.extents.x - Mathf.Abs(delta.x)) / .8f)
                    * Mathf.Clamp01((melee.extents.y + frame.Opponent.Body.extents.y - Mathf.Abs(delta.y)) / .8f);
            }
            chance *= ShieldTransmission(origin, target, .1f, frame)
                * Vulnerability(frame.Opponent, at + frame.ExecutionDelay + release + travel);
            var damage = candidate.Move == HeroAiMove.Charge ? source.BeamDamage
                : candidate.Move == HeroAiMove.Slash ? source.NextMeleeDamage(frame.Self.State) : source.RushDamage;
            return chance * Mathf.Min(damage, frame.Opponent.Striker.HitPoint.CurrentValue) / (1f + at / frame.BeatSeconds * .5f);
        }

        float HitChance(HeroAiCandidate candidate, HeroAiFrame frame, HeroAiTuning tuning, HeroAiMemory memory)
            => HitChance(candidate, frame, tuning, memory, out _);

        float HitChance(HeroAiCandidate candidate, HeroAiFrame frame, HeroAiTuning tuning, HeroAiMemory memory, out float firstImpact) {
            firstImpact = float.PositiveInfinity;
            var source = frame.Self.Hero;
            if (candidate.Move == HeroAiMove.Beam) {
                var origin = source.BeamOrigin + PredictSelf(candidate, frame, tuning, source.BeamReleaseSeconds) - frame.Self.Body.center;
                var hitAt = source.BeamReleaseSeconds + Mathf.Abs(frame.Opponent.Body.center.x - origin.x) / Mathf.Max(.1f, source.BeamSpeed);
                firstImpact = hitAt;
                var target = PredictOpponent(frame, hitAt + frame.ExecutionDelay, memory);
                if (advancedTactics && Vector3.Dot(target - origin, frame.Self.Striker.LookDirection.CurrentValue) <= 0f) return 0f;
                var vertical = Mathf.Abs(target.y - origin.y);
                return Mathf.Clamp01((tuning.BeamRadius + frame.Opponent.Body.extents.y - vertical) / .8f)
                    * ShieldTransmission(origin, target, tuning.BeamRadius, frame) * Vulnerability(frame.Opponent, hitAt + frame.ExecutionDelay);
            }
            var best = 0f;
            var melee = source.NextMeleeBounds(frame.Self.State);
            var look = frame.Self.Striker.LookDirection.CurrentValue;
            var rushOrigin = source.RushOrigin + PredictSelf(candidate, frame, tuning, 0f) - frame.Self.Body.center;
            var rushBounds = source.RushBounds(rushOrigin, look);
            var rushLifetime = source.RushProjectileLifetimeSeconds;
            var hitWindow = advancedTactics ? source.NextMeleeHitWindow(frame.Self.State) : Vector2.zero;
            var start = advancedTactics && candidate.Move == HeroAiMove.Slash ? hitWindow.x : 0f;
            var end = !advancedTactics ? .42f : candidate.Move == HeroAiMove.Slash ? hitWindow.y
                : Mathf.Min(rushLifetime ?? 1.5f, 1.5f);
            // Sample the authored hit window; a long-lived wave can punish beyond the body lunge.
            var samples = advancedTactics ? 24 : 12;
            for (var i = 0; i <= samples; i++) {
                var t = advancedTactics ? Mathf.Lerp(start, end, (float)i / samples) : i * .035f;
                var target = PredictOpponent(frame, t + frame.ExecutionDelay, memory);
                var self = PredictSelf(candidate, frame, tuning, t);
                Vector3 hitCenter;
                Vector3 hitExtents;
                if (candidate.Move == HeroAiMove.Rush) {
                    if (rushLifetime.HasValue && t > rushLifetime.Value) continue;
                    hitCenter = rushBounds.center + look * (source.RushProjectileSpeed * t);
                    hitExtents = rushBounds.extents;
                } else {
                    hitCenter = melee.center + self - frame.Self.Body.center;
                    hitExtents = melee.extents;
                }
                var delta = target - hitCenter;
                var chance = Mathf.Clamp01((hitExtents.x + frame.Opponent.Body.extents.x - Mathf.Abs(delta.x)) / .7f)
                    * Mathf.Clamp01((hitExtents.y + frame.Opponent.Body.extents.y - Mathf.Abs(delta.y)) / .7f);
                var strikeOrigin = candidate.Move == HeroAiMove.Rush ? rushOrigin : hitCenter - look * hitExtents.x;
                chance *= ShieldTransmission(strikeOrigin, target, .1f, frame);
                chance *= Vulnerability(frame.Opponent, t + frame.ExecutionDelay);
                if (chance >= .7f) firstImpact = Mathf.Min(firstImpact, t);
                best = Mathf.Max(best, chance);
            }
            return best * (1f - memory.WhiffPressure * (advancedTactics ? .015f : .05f));
        }

        float SpecialChance(HeroAiFrame frame, HeroAiMemory memory) {
            // Launch captures character Hurtboxes, so a shield wall can be punished once in approach range.
            if (frame.Opponent.Category == StrikerStateCategory.Special) return 0f;
            var source = frame.Self.Hero;
            var distance = Vector3.Distance(frame.Opponent.Position, frame.Self.Position);
            if (distance <= source.SpecialActivationDistance) {
                var offset = source.SpecialHitOffset;
                offset.x *= Mathf.Sign(frame.Self.Striker.LookDirection.CurrentValue.x);
                var grab = new Bounds(frame.Self.Position + offset, source.SpecialHitHalfExtents * 2f);
                if (!grab.Intersects(frame.Opponent.Body)) return 0f;
            }
            var approachTime = Mathf.Clamp((distance - source.SpecialActivationDistance) / Mathf.Max(.1f, source.SpecialApproachSpeed), 0f, source.SpecialApproachSeconds);
            var target = PredictOpponent(frame, frame.ExecutionDelay + approachTime, memory);
            var predictedDistance = Vector3.Distance(target, frame.Self.Body.center);
            if (predictedDistance > source.SpecialReach - .05f) return 0f;
            var chance = Mathf.Clamp01((source.SpecialReach + .5f - predictedDistance) / 1.5f);
            return chance * Vulnerability(frame.Opponent, frame.ExecutionDelay + approachTime);
        }

        static float ShieldTransmission(Vector3 origin, Vector3 target, float radius, HeroAiFrame frame) {
            var body = frame.Opponent.Body;
            body.center = target;
            body.Expand(radius * 2f);
            var delta = target - origin;
            if (delta.sqrMagnitude < .000001f) return frame.OpponentShields.Count > 0 ? .35f : 1f;
            var ray = new Ray(origin, delta.normalized);
            body.IntersectRay(ray, out var bodyDistance);
            foreach (var shield in frame.OpponentShields) {
                var bounds = shield.Bounds;
                bounds.Expand(radius * 2f);
                if (bounds.IntersectRay(ray, out var shieldDistance) && shieldDistance < bodyDistance - .05f) return .08f;
            }
            return 1f;
        }

        float Vulnerability(HeroAiActor opponent, float future) {
            future *= predictionWeight;
            if (opponent.Category == StrikerStateCategory.Special) return .05f;
            if (opponent.Category == StrikerStateCategory.Guard && opponent.Striker.Striker.CurrentValue is Striker.Warrior or Striker.DemonKing) {
                var guardSeconds = (opponent.Striker as IStrikerCombatObservationSource)?.ActiveGuardSeconds ?? 1f;
                return opponent.StateSeconds + future < guardSeconds ? .05f : 1f;
            }
            return 1f;
        }

        void BuildSpellForecast(HeroAiCandidate candidate, HeroAiFrame frame, HeroAiTuning tuning, float anticipationWeight) {
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
                forecast.Add(new HeroAiThreat(ice.ObserveSpawnBounds(target), Vector3.zero, Vector3.zero,
                    spawnTime + ice.ObservationActivationDelay, spawnTime + ice.ObservationLifetime,
                    ice.ObservationDamage / 10f, true, true, Confidence: weight));
            } else if (spell == 3) {
                var rock = wizard.RockPrefab;
                target += Vector3.up * wizard.RockSpawnHeight;
                forecast.Add(new HeroAiThreat(rock.ObserveSpawnBounds(target), Vector3.zero, Physics.gravity,
                    spawnTime, spawnTime + rock.ObservationLifetime, rock.ObservationDamage / 10f, true, Confidence: weight));
            } else {
                var fire = wizard.FirePrefab;
                var origin = wizard.FireOrigin + enemy.Velocity * Mathf.Min(spawnTime, .12f);
                forecast.Add(new HeroAiThreat(new Bounds(origin, Vector3.one * fire.ObservationRadius * 2f),
                    enemy.Striker.LookDirection.CurrentValue.normalized * fire.ObservationSpeed, Vector3.zero,
                    spawnTime, spawnTime + wizard.FireLifetime, fire.ObservationDamage / 10f, true, Confidence: weight));
            }
        }

        void BuildPressureForecast(HeroAiCandidate candidate, HeroAiFrame frame, HeroAiTuning tuning,
            HeroAiMemory memory, float anticipationWeight) {
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
                    forecast.Add(new HeroAiThreat(bounds, toward * hero.RushProjectileSpeed, Vector3.zero,
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
                    forecast.Add(new HeroAiThreat(bounds, facing * (attack.ObservationSpeed * Mathf.Exp(-decay * hit.x)), Vector3.zero,
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
                    forecast.Add(new HeroAiThreat(bounds, enemy.Striker.LookDirection.CurrentValue.normalized * wizard.ShieldSpeed,
                        Vector3.zero, commandAt, commandAt + wizard.ShieldLifetime, Mathf.Max(.25f, wizard.ShieldDamage / 10f), true,
                        Confidence: weight * Mathf.Lerp(.2f, .95f, Mathf.Max(memory.ShieldLaunchHabit, memory.ShieldHabit * .65f))));
                }
            }
        }

        float ThreatRisk(HeroAiCandidate candidate, HeroAiFrame frame, HeroAiTuning tuning, float horizon, float caution) {
            if (candidate.Move == HeroAiMove.Special) return .03f;
            var risk = 0f;
            for (var threatIndex = 0; threatIndex < frame.Threats.Count + forecast.Count; threatIndex++) {
                var threat = threatIndex < frame.Threats.Count ? frame.Threats[threatIndex] : forecast[threatIndex - frame.Threats.Count];
                var start = Mathf.Max(frame.ExecutionDelay, threat.StartsAt);
                var exposureHorizon = horizon + frame.BeatSeconds * followUpWeight * .5f;
                if (threat.GroundHazard) exposureHorizon = Mathf.Max(exposureHorizon, frame.BeatSeconds * (1f + followUpWeight));
                var end = Mathf.Min(threat.EndsAt, frame.ExecutionDelay + exposureHorizon);
                if (start >= end) continue;
                // A rush wave may destroy a shield, but that does not guarantee interception
                // before body contact (or before another shield consumes the wave).
                var body = new Bounds(Vector3.zero, threat.Bounds.size + frame.Self.Body.size + Vector3.one * tuning.PredictionMargin * 2f);
                for (var i = 0; i < 12; i++) {
                    var a = Mathf.Lerp(start, end, i / 12f);
                    var b = Mathf.Lerp(start, end, (i + 1) / 12f);
                    var self = PredictSelf(candidate, frame, tuning, a - frame.ExecutionDelay);
                    var p0 = self - ThreatPosition(threat, a);
                    var p1 = PredictSelf(candidate, frame, tuning, b - frame.ExecutionDelay) - ThreatPosition(threat, b);
                    if (!SegmentIntersects(body, p0, p1)) continue;
                    var blocked = false;
                    if (candidate.Move == HeroAiMove.Guard && !threat.GroundHazard && threat.Severity < 2f
                        && b <= frame.ExecutionDelay + frame.Self.Hero.GuardSeconds) {
                        var shield = frame.Self.Hero.ShieldBounds;
                        shield.center += self - frame.Self.Body.center;
                        shield.Expand(threat.Bounds.size);
                        var from = ThreatPosition(threat, Mathf.Max(0f, a - .2f));
                        var to = ThreatPosition(threat, b + .05f);
                        blocked = SegmentIntersects(shield, from, to) && Vector3.Distance(from, shield.ClosestPoint(from)) < Vector3.Distance(from, self);
                    }
                    risk += threat.Severity * threat.Confidence * (threat.Projectile ? caution : 1f) * (blocked ? .08f : 1f);
                    break;
                }
            }
            return Mathf.Min(4f, risk);
        }

        static Vector3 ThreatPosition(HeroAiThreat threat, float t) {
            var moving = threat.Projectile || threat.AnchoredAtStart ? Mathf.Max(0f, t - threat.StartsAt) : t;
            var travel = threat.MotionDecay > .0001f ? (1f - Mathf.Exp(-threat.MotionDecay * moving)) / threat.MotionDecay : moving;
            return threat.Bounds.center + threat.Velocity * travel + threat.Acceleration * (.5f * moving * moving);
        }

        float AnticipationRisk(HeroAiCandidate candidate, HeroAiFrame frame, HeroAiTuning tuning,
            HeroAiMatchup matchup, HeroAiMemory memory, float horizon) {
            if (hasMeleeForecast || hasRushForecast) return 0f;
            if (candidate.Move == HeroAiMove.Special) return 0f;
            var enemy = frame.Opponent;
            if (enemy.State is Core.LargeHero.StunState or Core.LargeWizard.StunState or Core.LargeWizard.StunCancelState
                or Core.LargeSatan.StunState or Core.LargeSatan.StunCancelState) return 0f;
            var recoveryDelay = 0f;
            if (enemy.State is Core.DarkSatan.StunState demonStun) {
                if (!demonStun.ObservationAutoRecovers) return 0f;
                recoveryDelay = Mathf.Max(0f, demonStun.ObservationDuration - enemy.StateSeconds - frame.ExecutionDelay);
            } else if (enemy.State is Core.DarkSatan.StunCancelState demonCancel) {
                recoveryDelay = Mathf.Max(0f, demonCancel.ObservationDuration - enemy.StateSeconds - frame.ExecutionDelay);
            }
            var risk = 0f;
            var spearRisk = 0f;
            var verticalReach = enemy.Striker.Striker.CurrentValue == Striker.Hero && (memory.LowRushHabit * predictionWeight) > .15f ? 1.3f : 2.8f;
            for (var i = 1; i <= 8; i++) {
                var t = horizon * i / 8f;
                if (t < recoveryDelay) continue;
                var self = PredictSelf(candidate, frame, tuning, t);
                var opponent = PredictOpponent(frame, t + frame.ExecutionDelay, memory);
                var vertical = Mathf.Clamp01((verticalReach - Mathf.Abs(self.y - opponent.y)) / Mathf.Max(.6f, verticalReach * .5f));
                var reach = matchup.OpponentReach;
                var aggression = .12f + (memory.AttackHabit * predictionWeight) * .4f;
                if (enemy.Striker.Striker.CurrentValue == Striker.Warrior) {
                    reach = tuning.WarriorLungeReach;
                    aggression += (memory.AbHabit * predictionWeight) * .4f;
                    if (enemy.State is Core.LargeSatan.ChargeState charge && charge.ObservedEmitState != null) {
                        var direction = charge.ObservedEmitState.ObserveThrowDirection(Vector2.zero, enemy.Striker.LookDirection.CurrentValue);
                        var spear = charge.ObservedPoleArm.transform.position + direction * charge.ObservedEmitState.ObserveTravelDistance(t);
                        var lane = new Bounds(Vector3.zero, frame.Self.Body.size + Vector3.one * .8f);
                        if (SegmentIntersects(lane, charge.ObservedPoleArm.transform.position - self, spear - self)) spearRisk += (.9f + (memory.AbHabit * predictionWeight) * .5f) / 8f;
                    }
                    if (enemy.State is Core.LargeSatan.EmitState) { reach = tuning.WarriorWarpReach; aggression = .7f + (memory.AbHabit * predictionWeight) * .65f; }
                }
                if (enemy.Category == StrikerStateCategory.Special) aggression = 1.5f;
                if (enemy.Category == StrikerStateCategory.Guard) aggression *= .25f;
                var proximity = Mathf.Clamp01((reach - Mathf.Abs(self.x - opponent.x)) / 2f) * vertical;
                if (enemy.Striker.Striker.CurrentValue is Striker.Hero or Striker.DemonKing) {
                    var lunge = enemy.Striker.Striker.CurrentValue == Striker.Hero ? 11f : 8f;
                    var closing = lunge * Mathf.Clamp01(t / Mathf.Max(.08f, frame.BeatSeconds));
                    var gap = Mathf.Max(0f, Mathf.Abs(self.x - enemy.Body.center.x) - closing);
                    proximity = Mathf.Max(proximity, Mathf.Clamp01((3f - gap) / 2f) * Mathf.Clamp01((verticalReach - Mathf.Abs(self.y - enemy.Body.center.y)) / Mathf.Max(.6f, verticalReach * .5f)));
                    aggression = Mathf.Max(aggression, .5f + (memory.AttackHabit * predictionWeight));
                }
                var protection = 1f;
                if (candidate.Move == HeroAiMove.Guard && t < frame.Self.Hero.GuardSeconds && Vector3.Dot(opponent - self, frame.Self.Striker.LookDirection.CurrentValue) > 0f
                    && !(enemy.Striker.Striker.CurrentValue == Striker.Hero && (memory.LowRushHabit * predictionWeight) > .15f)) protection = .25f;
                var approachRisk = proximity * aggression * protection;
                if (enemy.Striker.Striker.CurrentValue is Striker.Hero or Striker.DemonKing) risk = Mathf.Max(risk, approachRisk);
                else risk += approachRisk / 8f;

            }
            return risk + spearRisk;
        }

        Vector3 PredictOpponent(HeroAiFrame frame, float seconds, HeroAiMemory memory) {
            var enemy = frame.Opponent;
            seconds *= predictionWeight;
            if (advancedTactics && enemy.State is Core.LargeSatan.EmitState warpState && enemy.Striker is IStrikerContext context) {
                var destination = warpState.ObserveWarpPosition(context, Mathf.Min(seconds, frame.ExecutionDelay));
                if (destination.HasValue) return ReplyPosition(frame, ClampToArena(destination.Value + enemy.Body.center - enemy.Position, enemy), seconds, memory);
            }
            if (!advancedTactics && predictionWeight > .5f && enemy.State is Core.LargeSatan.EmitState emit) {
                var spear = emit.ObservedPoleArm.ObserveProjectile();
                return ClampToArena(emit.ObservedPoleArm.transform.position + (spear?.Velocity ?? Vector3.zero) * Mathf.Min(seconds, .15f) + enemy.Body.center - enemy.Position, enemy);
            }
            var duration = enemy.State is Core.LargeHero.RushState or Core.LargeHero.SpecialApproachState ? .6f
                : enemy.Category is StrikerStateCategory.Attack or StrikerStateCategory.Dash ? .3f
                : enemy.Category is StrikerStateCategory.Guard or StrikerStateCategory.Charge or StrikerStateCategory.Special ? .12f : .5f;
            var velocity = enemy.Category == StrikerStateCategory.Idle ? Vector3.Lerp(enemy.Velocity, memory.OpponentDrift, .35f) : enemy.Velocity;
            var move = velocity * Mathf.Min(seconds, duration);
            if (!enemy.Grounded && velocity.y < 0f) move.y += Physics.gravity.y * .5f * seconds * seconds;
            return ReplyPosition(frame, ClampToArena(enemy.Body.center + move, enemy), seconds, memory);
        }

        static Vector3 PredictSelf(HeroAiCandidate candidate, HeroAiFrame frame, HeroAiTuning tuning, float seconds, bool clamp = true) {
            var source = frame.Self.Hero;
            var velocity = frame.Self.Velocity;
            var air = source.UsesAirInputs(frame.Self.State);
            var delay = DragMove(velocity, air ? source.AirDamping : 0f, frame.ExecutionDelay, !frame.Self.Grounded);
            Vector3 move;
            if (candidate.Move == HeroAiMove.Rush) {
                move = Vector3.right * candidate.Direction.x * (source.RushSpeed * Mathf.Min(seconds, source.RushSeconds) + tuning.WalkSpeed * Mathf.Max(0f, seconds - source.RushSeconds));
                if (!frame.Self.Grounded) move += Vector3.up * velocity.y * seconds + Physics.gravity * (.5f * seconds * seconds);
            } else if (candidate.Move == HeroAiMove.Move) {
                move = DragMove(candidate.Direction * (air ? source.AirDashSpeed : source.JumpSpeed), source.AirDamping, seconds, true);
            } else if (candidate.Move == HeroAiMove.Special) {
                var toward = frame.Opponent.Body.center - frame.Self.Body.center;
                move = toward.normalized * Mathf.Min(Mathf.Max(0f, toward.magnitude - source.SpecialActivationDistance), source.SpecialApproachSpeed * Mathf.Min(seconds, source.SpecialApproachSeconds));
            } else {
                if (candidate.Move == HeroAiMove.Guard) velocity.x = 0f;
                if (candidate.Move == HeroAiMove.Walk) velocity.x = candidate.Direction.x * tuning.WalkSpeed;
                var damping = air && candidate.Move is HeroAiMove.Slash or HeroAiMove.Wait ? source.AirDamping : 0f;
                if (candidate.Move == HeroAiMove.Wait && !air) seconds = Mathf.Min(seconds, .15f);
                move = DragMove(velocity, damping, seconds, !frame.Self.Grounded);
            }
            var predicted = frame.Self.Body.center + delay + move;
            return clamp ? ClampToArena(predicted, frame.Self) : predicted;
        }

        static Vector3 DragMove(Vector3 velocity, float damping, float seconds, bool gravity) {
            var acceleration = gravity ? Physics.gravity : Vector3.zero;
            if (damping < .001f) return velocity * seconds + acceleration * (.5f * seconds * seconds);
            var step = Mathf.Max(.001f, Time.fixedDeltaTime);
            var retain = Mathf.Clamp01(1f - damping * step);
            var count = Mathf.FloorToInt(seconds / step);
            var power = Mathf.Pow(retain, count);
            var series = retain * (1f - power) / Mathf.Max(.0001f, 1f - retain);
            var terminal = acceleration * (step * retain / Mathf.Max(.0001f, 1f - retain));
            var move = (velocity * series + terminal * (count - series)) * step;
            var lastVelocity = velocity * power + terminal * (1f - power);
            return move + (lastVelocity + acceleration * step) * retain * (seconds - count * step);
        }

        static Vector3 ClampToArena(Vector3 center, HeroAiActor actor) {
            if (!actor.Arena.HasValue) return center;
            var arena = actor.Arena.Value;
            var extents = actor.Body.extents;
            return new Vector3(Mathf.Clamp(center.x, arena.min.x + extents.x, arena.max.x - extents.x),
                Mathf.Clamp(center.y, arena.min.y + extents.y, arena.max.y - extents.y), center.z);
        }

        static float BoundaryCost(HeroAiCandidate candidate, HeroAiFrame frame, HeroAiTuning tuning, float horizon) {
            if (!frame.Self.Arena.HasValue) return 0f;
            var raw = PredictSelf(candidate, frame, tuning, horizon, false);
            var safe = ClampToArena(raw, frame.Self);
            var cost = Mathf.Abs(raw.x - safe.x) * 4f + Mathf.Max(0f, raw.y - safe.y) * 3f;
            if (candidate.Move is HeroAiMove.Move or HeroAiMove.Rush) {
                var arena = frame.Self.Arena.Value;
                var clearance = Mathf.Min(safe.x - arena.min.x, arena.max.x - safe.x) - frame.Self.Body.extents.x;
                cost += Mathf.Max(0f, .7f - clearance) * 5f;
            }
            return cost;
        }

        static bool SegmentIntersects(Bounds bounds, Vector3 start, Vector3 end) {
            if (bounds.Contains(start) || bounds.Contains(end)) return true;
            var delta = end - start;
            return delta.sqrMagnitude > .000001f && bounds.IntersectRay(new Ray(start, delta.normalized), out var distance) && distance <= delta.magnitude;
        }

        static GamePadButton? ButtonFor(HeroAiMove move) => move switch {
            HeroAiMove.Rush or HeroAiMove.Slash or HeroAiMove.Beam => GamePadButton.East,
            HeroAiMove.Charge => GamePadButton.South,
            HeroAiMove.Move => GamePadButton.West,
            HeroAiMove.Guard => GamePadButton.North,
            HeroAiMove.Special => GamePadButton.Start,
            _ => null
        };

        static HeroAiChoice Pulse(string reason) => new(new AiAction(Vector2.zero, GamePadButton.North), HeroAiMove.Wait, 0f, reason);
        static HeroAiChoice Idle(string reason) => new(AiAction.None, HeroAiMove.Wait, 0f, reason);
    }
}
