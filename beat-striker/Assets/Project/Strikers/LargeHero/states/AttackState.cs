using Core.Battle;
using UnityEngine;
using Core.Striker;
using Unity.VisualScripting;
using R3;
using System;
using System.Collections.Generic;
using Core.Striker.Components;


namespace Core.LargeHero {
    
    public class AttackState : StrikerState {

        // このステートにいる間、再生されるアニメーションクリップ
        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] private StrikerAnimationClip comboAnimationClip2;
        [SerializeField] private StrikerAnimationClip comboAnimationClip3;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] HitBox hitBox;
        IDisposable disposable;

        [SerializeField] ParticleSystem particleprefab;
        [SerializeField] AudioClip audioClip;

        [SerializeField] float damage = 10;
        [SerializeField] float combo1NockbackSpeed = 3;
        [SerializeField] float combo2NockbackSpeed = 4;
        [SerializeField] float combo3NockbackSpeed = 10;
        [SerializeField] int maxComboCount = 3;

        readonly List<Hit> hitsInFrame = new ();
        bool hitInState;
        int comboCount;
        int comboRequestCount;
        bool comboUnlocked;
        public record Hit(Vector3 hitpoint, Hurtbox hurtbox);

        StrikerAnimationClip GetComboClip(int count) {
            if (count == 1) return animationClip;
            if (count == 2) return comboAnimationClip2;
            return comboAnimationClip3;
        }

        float GetComboNockbackSpeed(int count) {
            if (count == 1) return combo1NockbackSpeed;
            if (count == 2) return combo2NockbackSpeed;
            return combo3NockbackSpeed;
        }
        // このステートに遷移した直後に呼ばれる
        public override void OnEnter(IStrikerContext context) {
            // アニメーションの再生を開始する
            comboCount = 1;
            comboRequestCount = 0;
            comboUnlocked = false;

            context.PlayAnimation(GetComboClip(comboCount), OnAnimationEnd);
            disposable = hitBox.OnEnterTrigger.Subscribe(collider => {
        
                Debug.Log("AttackState: OnEnterTrigger");
                if (collider.TryGetComponent<Hurtbox>(out var hurtbox)) {
                    

                    var hitpoint = collider.ClosestPoint(hitBox.transform.position);
                    hitsInFrame.Add(new (hitpoint, hurtbox));
                }
            
            });
            hitsInFrame.Clear();
            hitInState = false;
        }

        public void OnAnimationEnd(IStrikerStateContext context) {
            if (comboRequestCount >= 1 && comboCount < maxComboCount) {
                comboCount++;
                comboRequestCount--;
                hitInState = false;
                hitsInFrame.Clear();
                context.Rigidbody.GetComponent<StrikerHub>().PlayAnimation(GetComboClip(comboCount), OnAnimationEnd);
                return;
            }

            context.TryTransition(nextNode);
        }
        // このステートにいる間、毎フレーム呼ばれる
        public override void OnUpdate(IStrikerStateContext context) {
            if (!hitInState && hitsInFrame.Count >= 1) {
                var closestHit = hitsInFrame.MinBy(e => Vector3.Distance(e.hitpoint, hitBox.transform.position));
            
                Instantiate(particleprefab, closestHit.hitpoint, Quaternion.identity);

                AudioSource.PlayClipAtPoint(audioClip,closestHit.hitpoint);

                var nockbackSpeed = GetComboNockbackSpeed(comboCount);
                var nockBackDirection = Mathf.Sign(closestHit.hitpoint.x - context.Rigidbody.transform.position.x) * Vector2.right;
                closestHit.hurtbox.GiveHit(new HitStatus(damage, nockbackSpeed * nockBackDirection));
            

            hitsInFrame.Clear();
            hitInState = true;
            comboUnlocked = true;
            }
        }

        // 他のステートに遷移する直前に呼ばれる
        public override void OnExit(IStrikerContext context) {
            disposable.Dispose();
            comboRequestCount = 0;
        }

        // 攻撃コマンドが押された時に呼ばれる
        public override void OnAttackRequested(IStrikerStateContext context) {
            if (comboUnlocked == false || comboCount >= maxComboCount) {
                return;
            }

            var remainingCombo = maxComboCount - comboCount;
            if (comboRequestCount < remainingCombo) {
                comboRequestCount++;
            }
        }

        // チャージコマンドが押された時に呼ばれる
        public override void OnChargeRequested(IStrikerStateContext context) {
        }

        // ダッシュコマンドが押された時に呼ばれる
        public override void OnDashRequested(IStrikerStateContext context) {
        }

        // ガードコマンドが押された時に呼ばれる
        public override void OnGuardRequested(IStrikerStateContext context) {
        }

        // 攻撃を受けた時に呼ばれる
        public override void OnHit(IStrikerStateContext context, HitStatus status) {
        }

        // ミスした時に呼ばれる
        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}
