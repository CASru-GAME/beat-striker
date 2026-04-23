using UnityEngine;

namespace Alice {
    public class EscapeAiBrain : AiBrain {
        [SerializeField] private OptionalGamePadButton dashButton = OptionalGamePadButton.West;
        [SerializeField] private float wallCheckDistance = 2.0f;
        [SerializeField] private LayerMask wallMask = Physics.DefaultRaycastLayers;
        [SerializeField] private int bounceBeatCount = 3; // 壁にぶつかった後、何ビート分反対方向に走るか
        [SerializeField] private float jumpStrength = 1.0f; // 壁にぶつかったときのジャンプ入力の強さ

        private Vector2 currentDirection;
        private int bounceCounter = 0;

        protected override void OnAiEnabled() {
            base.OnAiEnabled();
            bounceCounter = 0;
            currentDirection = Vector2.zero;
        }

        protected override AiAction OnGoodWindow(AiObservation observation) {
            if (observation.Opponent == null || observation.Self == null) {
                return AiAction.None;
            }

            var selfPos = observation.Self.Position.CurrentValue;
            var enemyPos = observation.Opponent.Position.CurrentValue;

            // 既存のレイキャストの使い方を参考に壁判定
            bool isStuck = false;
            if (currentDirection.sqrMagnitude > 0.01f) {
                // 現在のX軸の移動方向を取り出す
                Vector3 moveDir = new Vector3(currentDirection.x, 0, 0).normalized;
                
                // コライダーの内部からレイがスタートして判定がすり抜けるのを防ぐため、
                // 壁がある方向（進行方向）とは逆方向に少しずらした位置からレイを飛ばす
                float backwardOffset = 0.5f;
                Vector3 castStart = observation.Self.CenterPosition.CurrentValue - moveDir * backwardOffset; 
                
                var resolvedWallMask = wallMask.value == 0 ? (LayerMask)Physics.DefaultRaycastLayers : wallMask;

                if (moveDir.sqrMagnitude > 0.001f) {
                    // 後ろにずらした分だけチェック距離を長くする
                    float totalCheckDistance = wallCheckDistance + backwardOffset;
                    
                    // デバッグ用にRayを可視化（シーンビューで届いているか確認可能）
                    Debug.DrawRay(castStart, moveDir * totalCheckDistance, Color.red, 1.0f);

                    if (Physics.Raycast(castStart, moveDir, totalCheckDistance, resolvedWallMask, QueryTriggerInteraction.Ignore)) {
                        isStuck = true;
                    }
                }
            }

            if (isStuck && bounceCounter <= 0) {
                // 壁にぶつかったら反対方向に逃げつつ、少し上にジャンプする
                currentDirection = new Vector2(-currentDirection.x, jumpStrength).normalized;
                bounceCounter = bounceBeatCount;
            } else {
                if (bounceCounter > 0) {
                    // 反対方向に逃げている最中
                    bounceCounter--;
                } else {
                    // 通常時は敵からとことん遠ざかる方向（反対方向）へ
                    // ダッシュは入力方向に動き、Y方向の入力でジャンプになる仕様に合わせる
                    var toEnemy = enemyPos - selfPos;
                    if (toEnemy.sqrMagnitude > 0.001f) {
                        currentDirection = new Vector2(-toEnemy.x, -toEnemy.y).normalized;
                    } else {
                        currentDirection = new Vector2(1, 0); // 敵と同じ位置にいる場合はとりあえず右へ
                    }
                }
            }

            GamePadButton? btn = dashButton == OptionalGamePadButton.None ? null : (GamePadButton)dashButton;
            return new AiAction(currentDirection, btn);
        }
    }
}
