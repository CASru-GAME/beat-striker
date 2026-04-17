using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {

    public class SpecialLandState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Special;

        // 着地時に再生するアニメーションクリップ。フィニッシュ演出の見た目と終了タイミングを制御します。
        [SerializeField] private StrikerAnimationClip animationClip;
        // フィニッシュ後に遷移するノード。アニメ終了でコールされ、次の通常状態や別動作へ繋ぎます。
        [SerializeField] private StrikerNode nextNode;
        // 最終ヒットや被害者の解放を扱うコンテキスト。`ReleaseVictimWithFinalHit`等の処理を提供します。
        [SerializeField] private SpecialSequenceContext specialSequenceContext;
        // 着地の瞬間に再生するパーティクル。フィニッシュの演出を強調する視覚効果として利用されます。
        [SerializeField] private ParticleSystem slashEffect;
        // フィニッシュ時に与えるダメージ量。`ReleaseVictimWithFinalHit`で被害者に適用される最終ダメージ値です。
        [SerializeField] private float finalDamage = 20f;
        // フィニッシュで与える水平方向のノックバック速度。被弾者を左右に吹き飛ばす強さを制御します。
        [SerializeField] private float finalKnockbackSpeedX = 12f;
        // フィニッシュで与える垂直方向のノックバック速度。被弾者の跳ね上がり量や空中軌道に影響します。
        [SerializeField] private float finalKnockbackSpeedY = 4f;

        public override void OnEnter(IStrikerContext context) {
            slashEffect.Play();

            specialSequenceContext.ReleaseVictimWithFinalHit(context.Rigidbody, finalDamage, finalKnockbackSpeedX, finalKnockbackSpeedY);
            context.PlayAnimation(animationClip, OnAnimationEnd);
        }

        public override void OnExit(IStrikerContext context) {
            specialSequenceContext.ForceReleaseVictim();
        }

        private void OnAnimationEnd(IStrikerStateContext context) {
            context.TryTransition(nextNode);
        }
    }
}
