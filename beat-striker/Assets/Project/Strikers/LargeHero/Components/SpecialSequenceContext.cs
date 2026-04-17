using UnityEngine;

namespace Core.LargeHero {
    public class SpecialSequenceContext : MonoBehaviour {
        [Tooltip("空中編成の中心点。手動で割り当てるか、AutoSetupが生成・解決したTransformがここに設定されます。")]
        [SerializeField] Transform aerialCenterPoint;
        [Tooltip("空中アンカーの自動セットアップ参照。未設定時は同一GameObject上のSpecialAerialAnchorAutoSetupを検索して使用します。")]
        [SerializeField] SpecialAerialAnchorAutoSetup aerialAnchorAutoSetup;
        [Tooltip("自分（攻撃主体）が中心からどれだけ左にずれるかのオフセット。被害者との相対配置を調整します。")]
        [SerializeField] float selfOffsetX = 0.6f;
        [Tooltip("被害者が中心からどれだけ右にずれるかのオフセット。編成時の左右位置バランスを決めます。")]
        [SerializeField] float victimOffsetX = 0.6f;
        [Tooltip("空中編成の中心からの高さオフセット。アンカー高さに更に加算して実際の隊形の高さを調節します。")]
        [SerializeField] float aerialHeightOffset = 0f;
        [Tooltip("ロック開始地点から許容して持ち上がれる最大量。大き過ぎると不自然に上昇するため制限する値です。")]
        [SerializeField] float maxLockRiseFromStart = 2.0f;
        [Tooltip("編成の中心が取り得るY座標の上限。シーン固定上限やステージ高さに合わせて制御します。")]
        [SerializeField] float maxCenterWorldY = 4.0f;
        [Tooltip("中心点ギズモの色。シーンビューで中心を目立たせるための色設定です。")]
        [SerializeField] Color centerGizmoColor = new(0.2f, 0.9f, 1f, 0.95f);
        [Tooltip("自分の位置を示すギズモ色。シーン上で主体の位置を視覚的に区別するための設定です。")]
        [SerializeField] Color selfGizmoColor = new(0.25f, 1f, 0.35f, 0.95f);
        [Tooltip("被害者の位置を示すギズモ色。シーンビューで被害者位置を明示するための色設定です。")]
        [SerializeField] Color victimGizmoColor = new(1f, 0.35f, 0.35f, 0.95f);
        [Tooltip("中心点ギズモの半径。視認性や重なり具合を制御するための描画サイズです。")]
        [SerializeField] float centerGizmoRadius = 0.12f;
        [Tooltip("アクター（自分/被害者）表示用ギズモの半径。編成時の位置目安として使われます。")]
        [SerializeField] float actorGizmoRadius = 0.1f;
        [Tooltip("ギズモで描画する中心から垂直線の長さ。編成の高さ関係を視覚的に把握しやすくします。")]
        [SerializeField] float gizmoVerticalLineLength = 0.8f;

        Hurtbox lockedVictimHurtbox;
        Rigidbody lockedVictimRigidbody;
        RigidbodyConstraints lockedVictimOriginalConstraints;
        bool lockedVictimOriginalUseGravity;
        float lockedLookDirectionX;
        float lockStartY;
        Vector3 lockCenterWorldPosition;
        Vector3 lockedVictimRelativeOffset;
        bool hasLockedVictim;

        public bool HasLockedVictim => hasLockedVictim;
        public Vector3 LockedVictimPosition => lockedVictimRigidbody.position;

        void Awake() {
            ResolveAerialCenterPoint();
        }

#if UNITY_EDITOR
        void OnValidate() {
            if (Application.isPlaying) {
                return;
            }

            ResolveAerialCenterPoint();
        }
#endif

        public void LockVictim(Hurtbox victimHurtbox, Rigidbody selfRigidbody) {
            lockedVictimHurtbox = victimHurtbox;
            lockedVictimRigidbody = victimHurtbox.GetComponentInParent<Rigidbody>();
            lockedVictimOriginalConstraints = lockedVictimRigidbody.constraints;
            lockedVictimOriginalUseGravity = lockedVictimRigidbody.useGravity;
            lockedVictimRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            lockedVictimRigidbody.useGravity = false;
            lockedVictimRigidbody.linearVelocity = Vector3.zero;

            lockedLookDirectionX = Mathf.Sign(lockedVictimRigidbody.position.x - selfRigidbody.position.x);
            if (lockedLookDirectionX == 0f) {
                lockedLookDirectionX = Mathf.Sign(selfRigidbody.transform.forward.x);
            }
            if (lockedLookDirectionX == 0f) {
                lockedLookDirectionX = 1f;
            }

            lockStartY = Mathf.Max(selfRigidbody.position.y, lockedVictimRigidbody.position.y);
            lockCenterWorldPosition = ResolveInitialCenterWorldPosition(selfRigidbody);
            lockedVictimRelativeOffset = lockedVictimRigidbody.position - selfRigidbody.position;

            hasLockedVictim = true;
            MoveToAerialCenter(selfRigidbody);
        }

        public void KeepAerialFormation(Rigidbody selfRigidbody) {
            if (!hasLockedVictim) {
                return;
            }

            MoveToAerialCenter(selfRigidbody);
        }

        public void ApplyHitToLockedVictim(float damage, Vector3 knockbackVelocity) {
            if (!hasLockedVictim) {
                return;
            }

            lockedVictimHurtbox.GiveHit(new HitStatus(damage, knockbackVelocity));
        }

        public void MoveFallTogether(Rigidbody selfRigidbody, float fallSpeed) {
            if (!hasLockedVictim) {
                return;
            }

            var nextSelfPosition = selfRigidbody.position + Vector3.down * (fallSpeed * Time.deltaTime);
            selfRigidbody.MovePosition(nextSelfPosition);
            lockedVictimRigidbody.MovePosition(nextSelfPosition + lockedVictimRelativeOffset);
            selfRigidbody.linearVelocity = Vector3.zero;
            lockedVictimRigidbody.linearVelocity = Vector3.zero;
        }

        public void ReleaseVictimWithFinalHit(Rigidbody selfRigidbody, float damage, float knockbackSpeedX, float knockbackSpeedY) {
            if (!hasLockedVictim) {
                return;
            }

            var direction = Mathf.Sign(lockedVictimRigidbody.position.x - selfRigidbody.position.x);
            if (direction == 0f) {
                direction = Mathf.Sign(selfRigidbody.transform.forward.x);
            }
            if (direction == 0f) {
                direction = 1f;
            }

            var finalKnockback = direction * knockbackSpeedX * Vector3.right + knockbackSpeedY * Vector3.up;
            lockedVictimHurtbox.GiveHit(new HitStatus(damage, finalKnockback));

            lockedVictimRigidbody.constraints = lockedVictimOriginalConstraints;
            lockedVictimRigidbody.useGravity = lockedVictimOriginalUseGravity;
            lockedVictimRigidbody.linearVelocity = finalKnockback;
            ClearLockedVictim();
        }

        public void ForceReleaseVictim() {
            if (!hasLockedVictim) {
                return;
            }

            lockedVictimRigidbody.constraints = lockedVictimOriginalConstraints;
            lockedVictimRigidbody.useGravity = lockedVictimOriginalUseGravity;
            ClearLockedVictim();
        }

        void MoveToAerialCenter(Rigidbody selfRigidbody) {
            var center = lockCenterWorldPosition + Vector3.up * aerialHeightOffset;
            var maxAllowedY = Mathf.Min(lockStartY + maxLockRiseFromStart, maxCenterWorldY);
            center.y = Mathf.Min(center.y, maxAllowedY);
            var lookDirectionX = lockedLookDirectionX;
            if (lookDirectionX == 0f) {
                lookDirectionX = 1f;
            }

            var selfPosition = center + Vector3.left * lookDirectionX * selfOffsetX;
            var victimPosition = center + Vector3.right * lookDirectionX * victimOffsetX;

            selfRigidbody.MovePosition(selfPosition);
            selfRigidbody.linearVelocity = Vector3.zero;
            lockedVictimRigidbody.MovePosition(victimPosition);
            lockedVictimRigidbody.linearVelocity = Vector3.zero;
            lockedVictimRelativeOffset = victimPosition - selfPosition;
        }

        void ClearLockedVictim() {
            lockedVictimHurtbox = null;
            lockedVictimRigidbody = null;
            lockCenterWorldPosition = Vector3.zero;
            lockedVictimRelativeOffset = Vector3.zero;
            hasLockedVictim = false;
        }

        Vector3 ResolveInitialCenterWorldPosition(Rigidbody selfRigidbody) {
            var midpoint = (selfRigidbody.position + lockedVictimRigidbody.position) * 0.5f;
            ResolveAerialCenterPoint();
            if (aerialCenterPoint) {
                midpoint.y = aerialCenterPoint.position.y;
            }

            return midpoint;
        }

        void ResolveAerialCenterPoint() {
            if (aerialCenterPoint) {
                return;
            }

            if (!aerialAnchorAutoSetup) {
                aerialAnchorAutoSetup = GetComponent<SpecialAerialAnchorAutoSetup>();
            }

            if (!aerialAnchorAutoSetup) {
                return;
            }

            aerialCenterPoint = aerialAnchorAutoSetup.ResolveAnchor();
        }

        void OnDrawGizmos() {
            DrawFormationGizmo();
        }

        void OnDrawGizmosSelected() {
            DrawFormationGizmo();
        }

        void DrawFormationGizmo() {
            ResolveAerialCenterPoint();
            if (!aerialCenterPoint) {
                return;
            }

            var center = aerialCenterPoint.position + Vector3.up * aerialHeightOffset;
            var lookDirectionX = Mathf.Sign(transform.forward.x);
            if (lookDirectionX == 0f) {
                lookDirectionX = 1f;
            }

            var expectedSelfPosition = center + Vector3.left * lookDirectionX * selfOffsetX;
            var expectedVictimPosition = center + Vector3.right * lookDirectionX * victimOffsetX;

            Gizmos.color = centerGizmoColor;
            Gizmos.DrawSphere(center, centerGizmoRadius);
            Gizmos.DrawLine(center, center + Vector3.up * gizmoVerticalLineLength);

            Gizmos.color = selfGizmoColor;
            Gizmos.DrawSphere(expectedSelfPosition, actorGizmoRadius);
            Gizmos.DrawLine(center, expectedSelfPosition);

            Gizmos.color = victimGizmoColor;
            Gizmos.DrawSphere(expectedVictimPosition, actorGizmoRadius);
            Gizmos.DrawLine(center, expectedVictimPosition);

            Gizmos.color = Color.white;
            Gizmos.DrawLine(expectedSelfPosition, expectedVictimPosition);
        }
    }
}
