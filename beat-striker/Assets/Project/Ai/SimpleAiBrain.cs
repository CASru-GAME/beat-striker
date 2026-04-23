using UnityEngine;

namespace Alice {
    public class SimpleAiBrain : AiBrain {
        [SerializeField] private float attackRange = 1.0f;
        [SerializeField] private OptionalGamePadButton attackButton = OptionalGamePadButton.East;
        [SerializeField] private OptionalGamePadButton dashButton = OptionalGamePadButton.West;

        protected override AiAction OnGoodWindow(AiObservation observation) {
            if (observation.Opponent == null || observation.Self == null) {
                return AiAction.None;
            }

            var selfPos = observation.Self.Position.CurrentValue;
            var enemyPos = observation.Opponent.Position.CurrentValue;
            
            var distanceSq = (enemyPos - selfPos).sqrMagnitude;

            if (distanceSq <= attackRange * attackRange) {
                // 攻撃範囲内の場合は攻撃ボタンを押す
                return new AiAction(Vector2.zero, attackButton == OptionalGamePadButton.None ? null : (GamePadButton)attackButton);
            } else {
                // 範囲外の場合は敵に向かってダッシュ
                // ダッシュは入力方向に動き、Y方向の入力でジャンプになる仕様に合わせる
                var toEnemy = enemyPos - selfPos;
                var direction = toEnemy.sqrMagnitude > 0.001f ? new Vector2(toEnemy.x, toEnemy.y).normalized : Vector2.zero;
                
                GamePadButton? btn = dashButton == OptionalGamePadButton.None ? null : (GamePadButton)dashButton;
                return new AiAction(direction, btn);
            }
        }
    }
}
