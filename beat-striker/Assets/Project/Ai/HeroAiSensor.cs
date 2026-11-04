using System.Collections.Generic;
using UnityEngine;

namespace Alice.HeroAi {
    internal sealed record HeroAiActor(
        IObservableStriker Striker,
        IStrikerState State,
        StrikerStateCategory Category,
        float StateSeconds,
        int StateVersion,
        Vector3 Position,
        Vector3 Velocity,
        Bounds Body,
        Bounds? Arena,
        bool Grounded,
        Core.LargeWizard.WizardCombatObservation Wizard,
        Core.LargeHero.HeroCombatObservation Hero
    );

    internal sealed record HeroAiThreat(
        Bounds Bounds, Vector3 Velocity, Vector3 Acceleration,
        float StartsAt, float EndsAt, float Severity, bool Projectile
    );

    internal sealed record HeroAiShield(Bounds Bounds);

    internal sealed record HeroAiFrame(
        HeroAiActor Self, HeroAiActor Opponent,
        float ExecutionDelay, float BeatSeconds, IReadOnlyList<HeroAiThreat> Threats,
        IReadOnlyList<HeroAiShield> SelfShields, IReadOnlyList<HeroAiShield> OpponentShields
    );

    internal sealed class HeroAiSensor {
        readonly List<Core.LargeWizard.Guard> guards = new();
        readonly List<HeroAiShield> selfShields = new();
        readonly List<HeroAiShield> opponentShields = new();
        readonly Collider[] nearby = new Collider[256];
        readonly HashSet<int> observedColliders = new();
        readonly List<HeroAiThreat> threats = new();
        readonly List<StrikerProjectileObservation> projectiles = new();
        readonly List<Core.DarkSatan.DemonVolley> demonVolleys = new();
        readonly List<Core.LargeSatan.WarriorGateVolley> gateVolleys = new();
        readonly List<Core.LargeWizard.Special> wizardSpecials = new();
        readonly List<beam11> wizardBeams = new();
        readonly HashSet<beam11> observedBeams = new();
        Transform opponentRoot;
        AttackPlayer[] attacks = System.Array.Empty<AttackPlayer>();
        Core.DarkSatan.DemonMeleeLimb[] limbs = System.Array.Empty<Core.DarkSatan.DemonMeleeLimb>();

        public HeroAiFrame Observe(AiObservation observation, float beatSeconds, HeroAiTuning tuning) {
            var self = ObserveActor(observation.Self, tuning.GroundClearance);
            var opponent = ObserveActor(observation.Opponent, tuning.GroundClearance);
            var source = observation.Opponent as IStrikerCombatObservationSource;
            var root = source?.ObservationRoot;
            if (opponentRoot != root) {
                opponentRoot = root;
                // These are optional attack sources within the explicitly supplied opponent.
                // Cache authored sources; dynamically deployed volleys are collected separately.
                attacks = root != null ? root.GetComponentsInChildren<AttackPlayer>(true) : System.Array.Empty<AttackPlayer>();
                limbs = root != null ? root.GetComponentsInChildren<Core.DarkSatan.DemonMeleeLimb>(true) : System.Array.Empty<Core.DarkSatan.DemonMeleeLimb>();
            }

            threats.Clear();
            projectiles.Clear();
            observedBeams.Clear();
            foreach (var attack in attacks) {
                if (!attack.isActiveAndEnabled || attack.ObservationRemainingSeconds <= 0f) continue;
                threats.Add(new HeroAiThreat(attack.ObservationBounds, opponent.Velocity, Vector3.zero,
                    attack.ObservationStartDelay, attack.ObservationRemainingSeconds, 1f, false));
            }
            foreach (var limb in limbs) {
                var strike = limb.ObserveAttack();
                if (strike == null) continue;
                threats.Add(new HeroAiThreat(new Bounds(strike.Position, Vector3.one * strike.Radius * 2f),
                    opponent.Velocity, Vector3.zero, strike.DelaySeconds, strike.LifetimeSeconds, 1.2f, false));
            }

            if (opponent.State is Core.LargeSatan.EmitState emit) {
                var spear = emit.ObservedPoleArm.ObserveProjectile();
                if (spear != null) projectiles.Add(spear);
            }
            if (opponent.State is Core.DarkSatan.SpecialState special) {
                var rush = special.ObserveRush(opponent.Position);
                if (rush != null) {
                    threats.Add(new HeroAiThreat(new Bounds(rush.Position, Vector3.one * rush.Radius * 2f),
                        rush.Velocity, Vector3.zero, rush.DelaySeconds, rush.LifetimeSeconds, 4f, true));
                }
            }
            if (opponent.State is Core.LargeWizard.SpecialState wizardSpecial && wizardSpecial.ObservedSpecial != null) {
                RememberWizardSpecial(wizardSpecial.ObservedSpecial);
            }
            if (root != null) {
                root.GetComponentsInChildren(false, demonVolleys);
                root.GetComponentsInChildren(false, gateVolleys);
                foreach (var volley in demonVolleys) volley.CollectProjectileObservations(projectiles);
                foreach (var volley in gateVolleys) volley.CollectProjectileObservations(projectiles);
            }
            foreach (var projectile in projectiles) {
                threats.Add(new HeroAiThreat(new Bounds(projectile.Position, Vector3.one * projectile.Radius * 2f),
                    projectile.Velocity, Vector3.zero, projectile.DelaySeconds, projectile.LifetimeSeconds, 1f, true));
            }

            ObservePhysicsProjectiles(self.Body.center, tuning.ProjectileObservationRadius);
            ObserveKnownWizardBeams();
            ObserveShields((observation.Self as IStrikerCombatObservationSource)?.ObservationRoot, selfShields);
            ObserveShields(opponentRoot, opponentShields);
            return new HeroAiFrame(self, opponent,
                Mathf.Max(0f, observation.Signal.BeatTime - observation.CurrentPlaybackTime), beatSeconds, threats, selfShields, opponentShields);
        }

        static HeroAiActor ObserveActor(IObservableStriker striker, float groundClearance) {
            var source = striker as IStrikerCombatObservationSource;
            var body = source?.BodyBounds ?? new Bounds(striker.CenterPosition.CurrentValue, new Vector3(1f, 2f, 1f));
            var arena = source?.MovementBounds;
            var velocity = striker.Velocity.CurrentValue;
            // Teleports appear in the public frame velocity. Do not extrapolate them as flight.
            velocity = Vector3.ClampMagnitude(velocity, 55f);
            var grounded = arena.HasValue
                ? body.min.y <= arena.Value.min.y + groundClearance && velocity.y <= .5f
                : Mathf.Abs(velocity.y) < .2f;
            Core.LargeWizard.WizardCombatObservation wizard = null;
            source?.ObservationRoot?.TryGetComponent(out wizard);
            Core.LargeHero.HeroCombatObservation hero = null;
            source?.ObservationRoot?.TryGetComponent(out hero);
            if (hero != null) velocity = Vector3.ClampMagnitude(hero.PhysicsVelocity, 120f);
            return new HeroAiActor(striker, source?.ObservedState, source?.ObservedState?.Category ?? striker.CurrentStateCategory.CurrentValue,
                source?.StateElapsedSeconds ?? 0f, source?.StateVersion ?? 0,
                striker.Position.CurrentValue, velocity, body, arena, hero != null ? hero.Grounded : grounded, wizard, hero);
        }

        void ObserveShields(Transform root, List<HeroAiShield> result) {
            result.Clear();
            if (root == null) return;
            root.GetComponentsInChildren(false, guards);
            foreach (var guard in guards) {
                if (guard.IsObservationProjectile || guard.ObservationOwner != root) continue;
                if (guard.TryGetComponent<Collider>(out var collider) && collider.enabled && collider.bounds.size.sqrMagnitude > .001f)
                    result.Add(new HeroAiShield(collider.bounds));
            }
            if (root.TryGetComponent<Core.LargeHero.HeroCombatObservation>(out var hero) && hero.ShieldActive)
                result.Add(new HeroAiShield(hero.ShieldBounds));
            result.Sort((a, b) => (a.Bounds.center - root.position).sqrMagnitude.CompareTo((b.Bounds.center - root.position).sqrMagnitude));
        }

        void ObservePhysicsProjectiles(Vector3 center, float radius) {
            observedColliders.Clear();
            var count = Physics.OverlapSphereNonAlloc(center, radius, nearby, ~0, QueryTriggerInteraction.Collide);
            // A dense volley must not silently discard the colliders beyond the fixed buffer.
            if (count == nearby.Length) {
                foreach (var collider in Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide)) ObserveCollider(collider);
                return;
            }
            for (var i = 0; i < count; i++) ObserveCollider(nearby[i]);
        }

        void ObserveCollider(Collider collider) {
            if (!observedColliders.Add(collider.GetInstanceID())) return;
            if (collider.TryGetComponent<beam11>(out var beamArea)) {
                if (beamArea.ObservationOwner == opponentRoot && opponentRoot != null) ObserveBeam(beamArea);
                return;
            }
            var body = collider.attachedRigidbody;
            if (body == null) return;

            Transform owner;
            var severity = 1f;
            var acceleration = Vector3.zero;
            // Physics queries observe hazards; component lookups classify optional kinds.
            // Nothing here is used to find or wire a required dependency.
            if (body.TryGetComponent<Core.LargeWizard.Fire>(out var fire)) {
                owner = fire.ObservationOwner;
            } else if (body.TryGetComponent<Core.LargeWizard.Ice>(out var ice)) {
                if (!ice.IsObservationThreat) return;
                owner = ice.ObservationOwner;
                severity = 1.4f;
            } else if (body.TryGetComponent<Core.LargeWizard.Rock>(out var rock)) {
                if (!rock.IsObservationThreat) return;
                owner = rock.ObservationOwner;
                acceleration = Physics.gravity;
                severity = 1.6f;
            } else if (body.TryGetComponent<Core.LargeWizard.beam>(out var beam)) {
                owner = beam.ObservationOwner;
                severity = 1.5f;
            } else if (body.TryGetComponent<Core.LargeWizard.Special>(out var special)) {
                owner = special.ObservationOwner;
                severity = 2f;
                if (owner == opponentRoot && opponentRoot != null) RememberWizardSpecial(special);
            } else if (body.TryGetComponent<Core.LargeWizard.Guard>(out var guard)) {
                owner = guard.ObservationOwner;
                // Scale projectile risk by damage so low-damage shield volleys do not force an endless retreat.
                severity = guard.IsObservationProjectile ? Mathf.Max(.1f, guard.ObservationDamage / 10f) : .25f;
            } else if (body.TryGetComponent<Core.LargeHero.Bullet>(out var heroBullet)) {
                owner = heroBullet.OwnerRoot != null ? heroBullet.OwnerRoot.transform : null;
                severity = Mathf.Max(.3f, heroBullet.ObservationDamage / 10f);
            } else {
                return;
            }
            if (owner != opponentRoot || opponentRoot == null) return;
            threats.Add(new HeroAiThreat(collider.bounds, body.linearVelocity, acceleration, 0f, 2f, severity, true));
        }

        void RememberWizardSpecial(Core.LargeWizard.Special special) {
            if (wizardSpecials.Contains(special)) return;
            wizardSpecials.Add(special);
            foreach (var beam in special.GetComponentsInChildren<beam11>(true)) {
                if (!wizardBeams.Contains(beam)) wizardBeams.Add(beam);
            }
        }

        void ObserveKnownWizardBeams() {
            for (var i = wizardSpecials.Count - 1; i >= 0; i--) {
                var special = wizardSpecials[i];
                if (special == null) { wizardSpecials.RemoveAt(i); continue; }
                if (special.ObservedBeam != null && !wizardBeams.Contains(special.ObservedBeam)) wizardBeams.Add(special.ObservedBeam);
            }
            for (var i = wizardBeams.Count - 1; i >= 0; i--) {
                var beam = wizardBeams[i];
                if (beam == null) { wizardBeams.RemoveAt(i); continue; }
                ObserveBeam(beam);
            }
        }

        void ObserveBeam(beam11 beam) {
            if (!beam.isActiveAndEnabled) return;
            if (!observedBeams.Add(beam)) return;
            var area = beam.ObserveAttack();
            threats.Add(new HeroAiThreat(area.Bounds, Vector3.zero, Vector3.zero, area.StartsAt, area.EndsAt, 2f, true));
        }

        public void Reset() {
            selfShields.Clear();
            opponentShields.Clear();
            guards.Clear();
            opponentRoot = null;
            attacks = System.Array.Empty<AttackPlayer>();
            limbs = System.Array.Empty<Core.DarkSatan.DemonMeleeLimb>();
            threats.Clear();
            projectiles.Clear();
            demonVolleys.Clear();
            gateVolleys.Clear();
            wizardSpecials.Clear();
            wizardBeams.Clear();
            observedBeams.Clear();
        }
    }
}
