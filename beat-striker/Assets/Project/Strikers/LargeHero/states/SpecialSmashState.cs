using Core.Battle;
using UnityEngine;
using Core.Striker;
using UnityEngine.Serialization;

namespace Core.LargeHero {

    public class SpecialSmashState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Special;

        // スマッシュ攻撃で再生するアニメーションクリップ。ヒット演出やダメージのタイミングをアニメと同期させます。
        [SerializeField] private StrikerAnimationClip animationClip;
        // 攻撃終了後に遷移するノード。アニメ終了や演出完了時にこのノードへ移行します。
        [SerializeField] private StrikerNode nextNode;
        // 被害者ロックや連携動作を司るコンテキスト。ダメージ適用や位置保持などを統括します。
        [SerializeField] private SpecialSequenceContext specialSequenceContext;
        // 攻撃時に再生するスラッシュ系のパーティクル。視覚効果としてOnEnterで再生されます。
        [SerializeField] private ParticleSystem slashEffect;

        // ヒット時に生成するエフェクトのプレハブ（旧名: hitEffectPrefab）。ヒット位置でInstantiateして追加演出を表示します。
        [FormerlySerializedAs("hitEffectPrefab")]
        [SerializeField] private ParticleSystem slashEffectPrefab;
        // 被弾時に鳴る効果音。ヒット成功時のフィードバックとして再生されます。
        [SerializeField] private AudioClip hitAudioClip;
        // 空振りやスイング用の効果音。ヒット有無に応じて使い分け視覚と聴覚の演出を整えます。
        [SerializeField] private AudioClip missAudioClip;

        // ヒット時に与えるダメージの基本値。`ApplyHitToLockedVictim`で被害者へこの値が適用されます。
        [SerializeField] private float damage = 30f;
        // ヒットを遅延して適用する秒数。演出のタイミング合わせや成立条件の猶予に使われます。
        [SerializeField] private float hitDelay = 0.15f;

        private bool hitApplied;

        public override void OnEnter(IStrikerContext context) {
            slashEffect.Play();
            var swingAudioClip = missAudioClip ? missAudioClip : hitAudioClip;
            swingAudioClip.PlayAtApp(context.Rigidbody.position);
            context.PlayAnimation(animationClip, OnAnimationEnd);
            specialSequenceContext.KeepAerialFormation(context.Rigidbody);
            hitApplied = false;

            ScheduleStateEvent(hitDelay, stateContext => {
                if (!specialSequenceContext.HasLockedVictim || hitApplied) {
                    return;
                }

                var hitPoint = specialSequenceContext.LockedVictimPosition;
                if (slashEffectPrefab) {
                    Instantiate(slashEffectPrefab, hitPoint, Quaternion.identity);
                }
                if (hitAudioClip) {
                    hitAudioClip.PlayAtApp(hitPoint);
                }
                specialSequenceContext.ApplyHitToLockedVictim(damage, Vector3.zero);
                hitApplied = true;
            });
        }

        public override void OnUpdate(IStrikerStateContext context) {
            specialSequenceContext.KeepAerialFormation(context.Rigidbody);
        }

        private void OnAnimationEnd(IStrikerStateContext context) {
            context.TryTransition(nextNode);
        }
    }
}
