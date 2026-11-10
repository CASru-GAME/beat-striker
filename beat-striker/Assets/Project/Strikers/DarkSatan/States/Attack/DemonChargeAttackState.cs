using UnityEngine;
using Alice;
using R3;
using System;
using System.Collections.Generic;

namespace Core.DarkSatan {
    public sealed class DemonChargeAttackState : StrikerState, ITutorialActionProgress, ITutorialResourceReset {
        const decimal BulletRecoveryPerBeat = 0.3m;

        [SerializeField] StrikerHub strikerHub;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] DemonVolley volleyPrefab;
        [SerializeField] DemonAttackEffect attackEffect;
        [SerializeField] StrikerAnimationClip chargeAnimation;
        [SerializeField] StrikerAnimationClip launchAnimation;
        [SerializeField] float launchDuration = 0.4f;
        [SerializeField, Min(0f)] float maxLaunchDelay = 0.3f;

        readonly List<DemonVolley> volleys = new();
        decimal nextVolleySize;
        bool requestingCharge;
        bool chargedThisBeat;
        bool fired;
        bool launchedVolley;
        bool launchRequested;
        bool previousGravity;
        float elapsed;
        float nextImpactTime;

        public override StrikerStateCategory Category => StrikerStateCategory.Charge;
        public bool HasVolley => volleys.Count > 0;
        public int ObservationVolleyCount => volleys.Count;
        public bool ObservationFired => fired;
        public float ObservationSpreadSeconds => volleyPrefab.SpreadSeconds;
        public float ObservationLaunchSeconds => launchDuration;
        public float ObservationMaxLaunchDelay => maxLaunchDelay;
        public float ObservationBulletDamage => volleyPrefab.ObservationDamage;
        public float ObservationBulletSpeed => volleyPrefab.ObservationSpeed;
        public int ObservationMaximumBulletsPerVolley => volleyPrefab.BulletCount;
        public int ObservationBulletsPerVolley =>
            Math.Min(volleyPrefab.BulletCount, Math.Max(1, (int)Math.Ceiling(nextVolleySize)));

        void Awake() {
            nextVolleySize = volleyPrefab.BulletCount;
            strikerHub.OnBeatCompleted.Subscribe(_ => CompleteBeat()).AddTo(this);
        }

        void CompleteBeat() {
            if (!chargedThisBeat) {
                nextVolleySize = Math.Min(volleyPrefab.BulletCount, nextVolleySize + BulletRecoveryPerBeat);
            }
            chargedThisBeat = false;
        }

        public void CollectReadyProjectileObservations(List<StrikerProjectileObservation> observations) {
            foreach (var volley in volleys) volley.CollectLaunchObservations(observations);
        }
        public bool IsTutorialActionCommitted => launchedVolley;
        public TutorialReset Resource => TutorialReset.Charge;
        public void ResetTutorialResource() {
            foreach (var volley in volleys.ToArray()) {
                if (!volley) continue;
                volley.gameObject.SetActive(false);
                Destroy(volley.gameObject);
            }
            volleys.Clear();
            launchRequested = fired = launchedVolley = false;
            nextVolleySize = volleyPrefab.BulletCount;
            chargedThisBeat = false;
        }

        public void RequestCharge(IStrikerStateContext context) {
            // Online state correction can enter this state without a charge command.
            requestingCharge = true;
            try {
                context.TryTransition(this, true);
            } finally {
                requestingCharge = false;
            }
        }

        public void RequestLaunch() {
            launchRequested = true;
        }

        public override void OnEnter(IStrikerContext context) {
            var toOpponent = context.GetOpponent().Position.CurrentValue - context.Rigidbody.position;
            if (Vector3.Dot(context.Rigidbody.transform.forward, toOpponent) < 0f) {
                context.Rigidbody.rotation *= Quaternion.Euler(0f, 180f, 0f);
            }
            previousGravity = context.Rigidbody.useGravity;
            context.Rigidbody.useGravity = false;
            var velocity = context.Rigidbody.linearVelocity;
            velocity.y = 0f;
            context.Rigidbody.linearVelocity = velocity;
            elapsed = 0f;
            fired = launchedVolley = false;
            if (!launchRequested) {
                Deploy(context, requestingCharge);
            }
            if (launchRequested) {
                elapsed = volleyPrefab.SpreadSeconds;
            } else {
                context.PlayAnimation(chargeAnimation);
            }
        }

        public override void OnUpdate(IStrikerStateContext context) {
            elapsed += Time.deltaTime;
            if (fired) {
                if (elapsed >= launchDuration) {
                    context.PreventGroup();
                    context.TryTransition(nextNode);
                }
                return;
            }
            if (elapsed < volleyPrefab.SpreadSeconds) return;
            context.Rigidbody.useGravity = previousGravity;
            if (launchRequested) {
                context.PreventGroup();
                Launch(context);
            } else {
                context.PreventGroup();
                context.TryTransition(nextNode);
            }
        }

        public override void OnAttackRequested(IStrikerStateContext context) {
            context.PreventGroup();
            if (fired) return;
            if (!launchRequested) {
                // Launch can execute on a later update without re-entering this state.
                BeginTutorialExecution((context.GetSelf() as IStrikerTutorialSource)?.ActiveTutorialCommand);
            }
            if (elapsed < volleyPrefab.SpreadSeconds) {
                launchRequested = true;
                return;
            }
            Launch(context);
        }

        void Launch(IStrikerStateContext context) {
            fired = true;
            launchRequested = false;
            elapsed = 0f;
            context.Rigidbody.useGravity = false;
            var velocity = context.Rigidbody.linearVelocity;
            velocity.y = 0f;
            context.Rigidbody.linearVelocity = velocity;
            var target = context.GetOpponent().CenterPosition.CurrentValue;
            var ordered = new List<(DemonVolley volley, int index, float distance, int order)>();
            foreach (var volley in volleys) {
                volley.PrepareLaunch();
                for (int i = 0; i < volley.BulletCount; i++) {
                    if (!volley.IsActive(i)) continue;
                    ordered.Add((volley, i, (volley.BulletPosition(i) - target).sqrMagnitude, ordered.Count));
                }
            }
            ordered.Sort((a, b) => {
                int comparison = a.distance.CompareTo(b.distance);
                return comparison != 0 ? comparison : a.order.CompareTo(b.order);
            });
            for (int i = 0; i < ordered.Count; i++) {
                var bullet = ordered[i];
                bullet.volley.SetLaunchDelay(bullet.index, ordered.Count > 1 ? maxLaunchDelay * i / (ordered.Count - 1) : 0f);
            }
            for (int i = 0; i < volleys.Count; i++) volleys[i].Fire(i == 0);
            launchedVolley = ordered.Count > 0;
            volleys.Clear();
            context.PlayAnimation(launchAnimation);
            attackEffect.Play(launchDuration);
        }

        public override void OnChargeRequested(IStrikerStateContext context) {
            context.PreventGroup();
            if (fired || launchRequested) return;
            context.Rigidbody.useGravity = false;
            var velocity = context.Rigidbody.linearVelocity;
            velocity.y = 0f;
            context.Rigidbody.linearVelocity = velocity;
            elapsed = 0f;
            Deploy(context, true);
            context.PlayAnimation(chargeAnimation);
        }

        void Deploy(IStrikerContext context, bool fromChargeCommand) {
            var center = context.GetSelf().CenterPosition.CurrentValue;
            var volley = Instantiate(volleyPrefab, center, Quaternion.identity, context.Rigidbody.transform);
            volley.Begin(context, this, ObservationBulletsPerVolley);
            volleys.Add(volley);
            if (fromChargeCommand) {
                nextVolleySize = nextVolleySize * 2m / 3m;
                chargedThisBeat = true;
            }
            attackEffect.Play(volley.SpreadSeconds);
        }

        public void ForgetVolley(DemonVolley volley) => volleys.Remove(volley);

        public void ReportImpact(IStrikerContext context, Vector3 position, AudioClip sound, float interval, float magnitude) {
            if (Time.time < nextImpactTime) return;
            sound.PlayAtApp(position);
            context.GenerateImpact(new StrikerImpact(magnitude * Vector3.down));
            nextImpactTime = Time.time + interval;
        }

        public override void OnExit(IStrikerContext context) {
            attackEffect.Stop();
            context.Rigidbody.useGravity = previousGravity;
            launchRequested = false;
        }
    }
}
