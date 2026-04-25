using Core.Battle;
using UnityEngine;
using Core.Striker;
using R3;
using System;

namespace Core.LargeWizard {

    public class GuardState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Guard;

        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] private StrikerNode nextNode;
        [SerializeField] AudioClip audioClip;          // ガードが発動したときの音
        [SerializeField] AudioClip audioClip2;         // 既存シールドを射出したときの音
        [SerializeField] float launchedShieldSpeed = 14f;
        [SerializeField] float launchedShieldDamage = 3f;
        [SerializeField] float launchedShieldKnockbackSpeed = 20f;
        [SerializeField] float launchedShieldLifetime = 3f;
        [SerializeField] int maxStackedShieldCount = 3;
        [SerializeField] Transform shieldSpawnTransform;
        [SerializeField] Transform secondShieldSpawnTransform;
        [SerializeField] Transform thirdShieldSpawnTransform;

        [SerializeField] Hurtbox shieldPrefab;

        IDisposable disposable;
        Hurtbox shieldInstance;
        bool hasShieldBeenHit;

        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            // アニメーションの再生を開始する
            context.PlayAnimation(animationClip, OnAnimationEnd);

            hasShieldBeenHit = false;
            var ownerStrikerHub = context.Rigidbody.GetComponent<StrikerHub>();
            var hasDirectionInput = context.LocalInputDirection.sqrMagnitude > 0.0001f;

            var existingGuards = context.Rigidbody.GetComponentsInChildren<Core.LargeWizard.Guard>(true);
            if (hasDirectionInput) {
                if (existingGuards.Length > 0) {
                    disposable = Disposable.Create(() => { });
                    var guardToLaunch = TakeNearestGuard(new System.Collections.Generic.List<Core.LargeWizard.Guard>(existingGuards), thirdShieldSpawnTransform.position);
                    guardToLaunch.SetOwnerStrikerHub(ownerStrikerHub);
                    guardToLaunch.LaunchForward(context.Rigidbody.transform.forward, launchedShieldSpeed, launchedShieldDamage, launchedShieldKnockbackSpeed, launchedShieldLifetime);

                    AudioSource.PlayClipAtPoint(audioClip2, context.Rigidbody.transform.position);
                    return;
                }

                SpawnShield(context, ownerStrikerHub, shieldSpawnTransform);
                return;
            }

            ShiftGuardsAndSpawn(context, ownerStrikerHub, existingGuards);
        }

        void ShiftGuardsAndSpawn(IStrikerContext context, StrikerHub ownerStrikerHub, Core.LargeWizard.Guard[] existingGuards) {
            var remainingGuards = new System.Collections.Generic.List<Core.LargeWizard.Guard>(existingGuards);
            var firstGuard = TakeNearestGuard(remainingGuards, shieldSpawnTransform.position);
            var secondGuard = TakeNearestGuard(remainingGuards, secondShieldSpawnTransform.position);
            var thirdGuard = TakeNearestGuard(remainingGuards, thirdShieldSpawnTransform.position);

            if (thirdGuard != null) {
                thirdGuard.SetOwnerStrikerHub(ownerStrikerHub);
                thirdGuard.LaunchForward(context.Rigidbody.transform.forward, launchedShieldSpeed, launchedShieldDamage, launchedShieldKnockbackSpeed, launchedShieldLifetime);
                AudioSource.PlayClipAtPoint(audioClip2, context.Rigidbody.transform.position);
            }

            if (secondGuard != null) {
                secondGuard.SetOwnerStrikerHub(ownerStrikerHub);
                secondGuard.MoveToPositionAndFix(thirdShieldSpawnTransform.position, 0.3f);
            }

            if (firstGuard != null) {
                firstGuard.SetOwnerStrikerHub(ownerStrikerHub);
                firstGuard.MoveToPositionAndFix(secondShieldSpawnTransform.position, 0.3f);
            }

            SpawnShield(context, ownerStrikerHub, shieldSpawnTransform);
        }

        void SpawnShield(IStrikerContext context, StrikerHub ownerStrikerHub, Transform spawnTransform) {
            shieldInstance = Instantiate(shieldPrefab, context.Rigidbody.transform);
            var guard = shieldInstance.GetComponent<Core.LargeWizard.Guard>();
            guard.SetOwnerStrikerHub(ownerStrikerHub);
            guard.SpawnAtPositionThenReturn(spawnTransform.position, 0.3f);

            disposable = shieldInstance.OnHit.Subscribe(hit => {
                if (hasShieldBeenHit) return;

                hasShieldBeenHit = true;
                context.Rigidbody.linearVelocity = 0.5f * hit.KnockbackVelocity;
            });

            AudioSource.PlayClipAtPoint(audioClip, shieldInstance.transform.position);
        }

        Core.LargeWizard.Guard TakeNearestGuard(System.Collections.Generic.List<Core.LargeWizard.Guard> guards, Vector3 targetPosition) {
            if (guards.Count == 0) {
                return null;
            }

            var nearestIndex = 0;
            var nearestDistance = (guards[0].transform.position - targetPosition).sqrMagnitude;
            for (var i = 1; i < guards.Count; i++) {
                var currentDistance = (guards[i].transform.position - targetPosition).sqrMagnitude;
                if (currentDistance < nearestDistance) {
                    nearestDistance = currentDistance;
                    nearestIndex = i;
                }
            }

            var nearestGuard = guards[nearestIndex];
            guards.RemoveAt(nearestIndex);
            return nearestGuard;
        }

        public void OnAnimationEnd(IStrikerStateContext context) {
            context.TryTransition(nextNode);
        }

        // 他のステートに遷移する直前に呼ばれる
        public override void OnExit(IStrikerContext context) {
            disposable.Dispose();
        }

    }
}
