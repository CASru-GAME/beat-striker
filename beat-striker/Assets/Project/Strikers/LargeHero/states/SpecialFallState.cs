using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {

    public class SpecialFallState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Special;

        // 落下中に再生するアニメーションクリップ。着地までの見た目を制御し、アニメと判定の同期に利用されます。
        [SerializeField] private StrikerAnimationClip animationClip;
        // 地面接地判定を行うコンポーネント。接地を検出したら`TryTransition(landNode)`で着地遷移します。
        [SerializeField] private GroundChecker groundChecker;
        // 着地検出時に遷移するノード。着地時の追加攻撃や次のステート処理へ繋ぐ遷移先です。
        [SerializeField] private StrikerNode landNode;
        // 落下中の被害者追従や位置固定を管理するコンテキスト。`MoveFallTogether`で連動移動を行います。
        [SerializeField] private SpecialSequenceContext specialSequenceContext;
        // 落下開始時に再生するスラッシュ系のパーティクル。視覚的に落下攻撃を強調するために使われます。
        [SerializeField] private ParticleSystem slashEffect;
        // 落下時に適用する速度（`MoveFallTogether`の速度）。演出や物理感を調整するための係数です。
        [SerializeField] private float fallSpeed = 15f;

        public override void OnEnter(IStrikerContext context) {
            context.PlayAnimation(animationClip);
            slashEffect.Play();
        }

        public override void OnUpdate(IStrikerStateContext context) {
            specialSequenceContext.MoveFallTogether(context.Rigidbody, fallSpeed);

            if (groundChecker.IsGrounded) {
                context.TryTransition(landNode);
            }
        }
    }
}
