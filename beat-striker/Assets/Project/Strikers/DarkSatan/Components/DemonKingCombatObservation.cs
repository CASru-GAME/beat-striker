using System.Collections.Generic;
using Alice;
using UnityEngine;

namespace Core.DarkSatan {
    // All character dependencies are authored on the character prefab.
    public sealed class DemonKingCombatObservation : MonoBehaviour {
        [SerializeField] Rigidbody body;
        [SerializeField] GroundChecker ground;
        [SerializeField] DemonMeleeAttackState melee;
        [SerializeField] DemonDirectionalAttackState uppercut, heelDrop;
        [SerializeField] DemonChargeAttackState charge;
        [SerializeField] DashNode dashNode;
        [SerializeField] DashState dash;
        [SerializeField] JumpState jump;
        [SerializeField] AirJumpState airJump;
        [SerializeField] AirSuperJumpState airSuperJump;
        [SerializeField] GuardState guard;
        [SerializeField] SpecialState special;

        public Vector3 PhysicsVelocity => body.linearVelocity;
        public bool Grounded => ground.IsGrounded;
        public DemonMeleeStrike NextStrike => melee.ObservationNextStrike;
        public float MeleeStopDistance => melee.ObservationStepStopDistance;
        public float MeleeChainDelay => melee.ObservationChainDelay;
        public DemonDirectionalAttackState Uppercut => uppercut;
        public DemonDirectionalAttackState HeelDrop => heelDrop;
        public bool HasVolley => charge.HasVolley;
        public int VolleyCount => charge.ObservationVolleyCount;
        public bool VolleyFired => charge.ObservationFired;
        public float SpreadSeconds => charge.ObservationSpreadSeconds;
        public float LaunchSeconds => charge.ObservationLaunchSeconds;
        public float BulletDamage => charge.ObservationBulletDamage;
        public float BulletSpeed => charge.ObservationBulletSpeed;
        public int MaximumBulletsPerVolley => charge.ObservationMaximumBulletsPerVolley;
        public int BulletsPerVolley => charge.ObservationBulletsPerVolley;
        public void CollectReadyProjectiles(List<StrikerProjectileObservation> result) => charge.CollectReadyProjectileObservations(result);
        public float DashSpeed(bool forward) => dash.ObservationNextSpeed(forward);
        public float DashSeconds => dash.ObservationDuration;
        public float DashEndRatio => dash.ObservationEndSpeedRatio;
        public float JumpSpeed => jump.ObservationSpeed;
        public float JumpSeconds => jump.ObservationDuration;
        public float JumpEndRatio => jump.ObservationEndSpeedRatio;
        public float AirJumpSpeed => dashNode.ObservationNextAirJumpIsSuper ? airSuperJump.ObservationSpeed : airJump.ObservationSpeed;
        public float AirJumpSeconds => dashNode.ObservationNextAirJumpIsSuper ? airSuperJump.ObservationDuration : airJump.ObservationDuration;
        public float AirJumpAcceleration => dashNode.ObservationNextAirJumpIsSuper ? airSuperJump.ObservationAcceleration : airJump.ObservationAcceleration;
        public float GuardShift => guard.ObservationShift;
        public float SpecialReach => special.ObservationReach;
        public float SpecialSpeed => special.ObservationSpeed;
        public float SpecialRadius => special.ObservationRadius;
        public float SpecialHeight => special.ObservationHeight;
        public float SpecialDamage => special.ObservationDamage;
    }
}
