using Core.Battle;
using UnityEngine;
using Core.Striker;
using Core.Striker.Components;
using UnityEngine.Serialization;

namespace Core.LargeHero {

    public class SpecialLaunchState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Special;

        // このステートで再生するアニメーションクリップ。アニメ終了のコールバックで遷移判定やヒット演出の終端を決定し、見た目と入力タイミングを同期します。
        [SerializeField] private StrikerAnimationClip animationClip;
        // ヒット成功時に遷移するノード。被害者をロックして連続演出した後、このノードに移る想定の遷移先です。
        [SerializeField] private StrikerNode nextNode;
        // ヒットが一切発生しなかった場合に遷移するフォールバックノード。空振り時の後処理や別行動への繋ぎに使われます。
        [SerializeField] private StrikerNode noHitFallbackNode;
        // 被害者のロックや移動、形成維持など連携動作を管理するコンテキスト。複数ステートで共有して動作を統一します。
        [SerializeField] private SpecialSequenceContext specialSequenceContext;
        // 攻撃時に再生する主要なパーティクルエフェクト。見た目の手ごたえを出すためOnEnterで再生されます。
        [SerializeField] private ParticleSystem slashEffect;
        // 追加の斬撃エフェクト。位置やタイミングを分けて別途再生することで表現を重ねられます。
        [SerializeField] private ParticleSystem slashEffect2;

        // ヒット時に生成するエフェクトのプレハブ（旧名: hitEffectPrefab）。ヒット位置にInstantiateして視覚フィードバックを提供します。
        [FormerlySerializedAs("hitEffectPrefab")]
        [SerializeField] private ParticleSystem slashEffectPrefab;
        // ヒット成功時に再生する効果音。ヒット位置で`PlayClipAtPoint`され、被弾の存在感を強めます。
        [SerializeField] private AudioClip hitAudioClip;
        // 空振りやスイング用の効果音。指定されていると攻撃開始時に先行再生されて打撃感を補強します。
        [SerializeField] private AudioClip missAudioClip;

        // 命中時に与えるダメージ量。`ApplyHitToLockedVictim`で被害者に適用される重要な数値です。
        [SerializeField] private float damage = 20f;
        // ヒット判定の中心オフセット（キャラ基準）。前方や高さ方向へ判定をずらすための相対座標です。
        [SerializeField] private Vector3 launchHitBoxOffset = new(1.35f, 1.0f, 0f);
        // OverlapBoxの半径（半分のサイズ）ベクトル。判定領域の幅・高さ・奥行きを決め、当たり判定の形状を調整します。
        [SerializeField] private Vector3 launchHitBoxHalfExtents = new(1.15f, 1.0f, 1.2f);
        // 対象に含めるレイヤーマスク。特定のレイヤーだけを攻撃対象にするなど判定範囲を制御します。
        [SerializeField] private LayerMask victimLayerMask = ~0;
        // トリガーコライダとの衝突判定モード。`Collide`/`Ignore`を切り替えてトリガーを検出するか制御します。
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;

        private readonly Collider[] overlapResults = new Collider[32];
        private bool hitInState;

        public record Hit(Vector3 hitPoint, Hurtbox hurtbox);

        public override void OnEnter(IStrikerContext context) {
            specialSequenceContext.ForceReleaseVictim();
            slashEffect.Play();
            slashEffect2.Play();
            var swingAudioClip = missAudioClip ? missAudioClip : hitAudioClip;
            AudioSource.PlayClipAtPoint(swingAudioClip, context.Rigidbody.position);
            context.PlayAnimation(animationClip, OnAnimationEnd);
            hitInState = false;
        }

        public override void OnUpdate(IStrikerStateContext context) {
            if (!hitInState) {
                if (TryGetLaunchHit(context, out var hit)) {
                    specialSequenceContext.LockVictim(hit.hurtbox, context.Rigidbody);
                    specialSequenceContext.ApplyHitToLockedVictim(damage, Vector3.zero);
                    if (slashEffectPrefab) {
                        Instantiate(slashEffectPrefab, hit.hitPoint, Quaternion.identity);
                    }
                    if (hitAudioClip) {
                        AudioSource.PlayClipAtPoint(hitAudioClip, hit.hitPoint);
                    }

                    hitInState = specialSequenceContext.HasLockedVictim;
                }

                return;
            }

            specialSequenceContext.KeepAerialFormation(context.Rigidbody);
        }

        public override void OnExit(IStrikerContext context) {}

        private void OnAnimationEnd(IStrikerStateContext context) {
            if (hitInState && specialSequenceContext.HasLockedVictim) {
                context.TryTransition(nextNode);
                return;
            }

            context.TryTransition(noHitFallbackNode);
        }

        private bool TryGetLaunchHit(IStrikerStateContext context, out Hit hit) {
            var selfTransform = context.Rigidbody.transform;
            var lookDirectionX = Mathf.Sign(selfTransform.forward.x);
            if (lookDirectionX == 0f) {
                lookDirectionX = 1f;
            }

            var hitBoxCenter = selfTransform.position + new Vector3(
                launchHitBoxOffset.x * lookDirectionX,
                launchHitBoxOffset.y,
                launchHitBoxOffset.z
            );

            var overlapCount = Physics.OverlapBoxNonAlloc(
                hitBoxCenter,
                launchHitBoxHalfExtents,
                overlapResults,
                Quaternion.identity,
                victimLayerMask,
                triggerInteraction
            );

            var minDistance = float.MaxValue;
            Hit closestHit = default;

            for (var i = 0; i < overlapCount; i++) {
                var collider = overlapResults[i];
                if (!collider) {
                    continue;
                }

                if (!collider.TryGetComponent<Hurtbox>(out var hurtbox)) {
                    hurtbox = collider.GetComponent<Hurtbox>();
                    if (!hurtbox) {
                        continue;
                    }
                }

                if (hurtbox.transform.root == selfTransform.root) {
                    continue;
                }

                var hurtboxRigidbody = hurtbox.GetComponentInParent<Rigidbody>();
                if (hurtboxRigidbody == context.Rigidbody) {
                    continue;
                }

                var hitPoint = collider.ClosestPoint(hitBoxCenter);
                var distance = Vector3.Distance(hitPoint, selfTransform.position);
                if (distance < minDistance) {
                    minDistance = distance;
                    closestHit = new(hitPoint, hurtbox);
                }
            }

            if (minDistance == float.MaxValue) {
                hit = default;
                return false;
            }

            hit = closestHit;
            return true;
        }
    }
}
