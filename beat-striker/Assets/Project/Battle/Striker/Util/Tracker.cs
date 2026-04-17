using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu(" 🟠Tracker", 0)]
public class Tracker : MonoBehaviour {
    [SerializeField] Transform target;
    [SerializeField] bool enableRigidMove = true;
    [SerializeField, Min(0f)] float targetTransitionDuration = 0.1f;

    private Rigidbody rb;
    private Vector3 relativePosition;
    private Quaternion relativeRotation;

    // ターゲットハンドル（削除用キー）
    public class TargetHandle { }

    // ターゲットの管理用
    private readonly struct TargetState {
        public readonly TargetHandle Handle;
        public readonly Transform Target;
        public readonly Vector3 RelativePosition;
        public readonly Quaternion RelativeRotation;
        public readonly bool FollowPosition;
        public readonly bool FollowRotation;

        public TargetState(TargetHandle handle, Transform target, Vector3 relativePosition, Quaternion relativeRotation, bool followPosition, bool followRotation) {
            Handle = handle;
            Target = target;
            RelativePosition = relativePosition;
            RelativeRotation = relativeRotation;
            FollowPosition = followPosition;
            FollowRotation = followRotation;
        }
    }

    // 元のターゲット状態を保存
    private TargetState originalState;

    private TargetState activeState;
    private bool hasActiveState;
    private bool isTransitioning;
    private float transitionElapsedTime;
    private Vector3 transitionStartPosition;
    private Quaternion transitionStartRotation;
    private Vector3 transitionEndPosition;
    private Quaternion transitionEndRotation;

    // 追加順序を管理（後に追加されたものが優先）
    private readonly List<TargetState> targetStates = new();

    void Awake() {
        Initialize();
    }

    void Start() {
        Initialize();
    }

    void Initialize() {
        if (hasActiveState) {
            return;
        }

        TryGetComponent<Rigidbody>(out rb);
        if (rb != null) {
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        if (target != null) {
            CaptureRelativeTransform(target);
        }


        // 元の状態を保存
        originalState = new TargetState(null, target, relativePosition, relativeRotation, true, true);
        activeState = originalState;
        hasActiveState = true;
    }

    /// <summary>
    /// ターゲットを追加する。引数なしの場合は追従を停止する。
    /// 最後に追加されたターゲットが優先される。
    /// </summary>
    /// <returns>削除用のハンドル</returns>
    public TargetHandle AddTarget(Transform newTarget = null, bool followPosition = true, bool followRotation = true) {
        Initialize();

        var handle = new TargetHandle();

        Vector3 pos = default;
        Quaternion rot = default;

        if (newTarget != null) {
            pos = newTarget.InverseTransformPoint(transform.position);
            rot = Quaternion.Inverse(newTarget.rotation) * transform.rotation;
        }

        var state = new TargetState(handle, newTarget, pos, rot, followPosition, followRotation);
        targetStates.Add(state);

        // 最新のターゲットをアクティブにする
        SetActiveState(state);

        return handle;
    }

    /// <summary>
    /// 相対位置・相対回転を明示指定してターゲットを追加する。
    /// </summary>
    public TargetHandle AddTarget(Transform newTarget, Vector3 customRelativePosition, Quaternion customRelativeRotation, bool followPosition = true, bool followRotation = true) {
        Initialize();

        var handle = new TargetHandle();
        var state = new TargetState(handle, newTarget, customRelativePosition, customRelativeRotation, followPosition, followRotation);
        targetStates.Add(state);
        SetActiveState(state);
        return handle;
    }

    /// <summary>
    /// 指定したハンドルのターゲットを削除する。
    /// 削除後は次に新しいターゲット、なければ元のターゲットに戻る。
    /// </summary>
    public void RemoveTarget(TargetHandle handle) {
        Initialize();

        int index = targetStates.FindIndex(s => s.Handle == handle);
        if (index < 0) return;

        targetStates.RemoveAt(index);

        // 残っているターゲットがあれば最新のものを適用、なければ元に戻る
        var nextState = targetStates.Count > 0 ? targetStates[^1] : originalState;
        SetActiveState(nextState);
    }

    private void SetActiveState(TargetState state) {
        if (hasActiveState && ReferenceEquals(activeState.Handle, state.Handle)) {
            return;
        }

        target = state.Target;
        relativePosition = state.RelativePosition;
        relativeRotation = state.RelativeRotation;

        var startPosition = transform.position;
        var startRotation = transform.rotation;
        var endPosition = CalculateTargetPosition(state, startPosition);
        var endRotation = CalculateTargetRotation(state, startRotation);

        activeState = state;
        hasActiveState = true;

        transitionStartPosition = startPosition;
        transitionStartRotation = startRotation;
        transitionEndPosition = endPosition;
        transitionEndRotation = endRotation;
        transitionElapsedTime = 0f;

        isTransitioning = targetTransitionDuration > 0f && (state.FollowPosition || state.FollowRotation) && state.Target != null;

        if (!isTransitioning) {
            ApplyPose(endPosition, endRotation);
        }
    }

    /// <summary>
    /// ターゲットに対する相対位置・回転を計算して保存する
    /// </summary>
    private void CaptureRelativeTransform(Transform t) {
        if (t == null) return;

        // 「相対的な位置」をターゲットのローカル座標系で保存
        relativePosition = t.InverseTransformPoint(transform.position);

        // 「相対的な回転」を保存
        // ターゲットの回転の逆行列を掛けることで、ターゲットから見た差分を抽出する
        relativeRotation = Quaternion.Inverse(t.rotation) * transform.rotation;
    }

    void Update() {
        Initialize();

        if (isTransitioning) {
            transitionElapsedTime += Time.deltaTime;
            var duration = Mathf.Max(targetTransitionDuration, 0.0001f);
            var t = Mathf.Clamp01(transitionElapsedTime / duration);
            var eased = t * t * (3f - 2f * t);

            // Transition end should track latest target pose to avoid snapping when target moves during blend.
            transitionEndPosition = CalculateTargetPosition(activeState, transitionEndPosition);
            transitionEndRotation = CalculateTargetRotation(activeState, transitionEndRotation);

            ApplyPose(
                Vector3.Lerp(transitionStartPosition, transitionEndPosition, eased),
                Quaternion.Slerp(transitionStartRotation, transitionEndRotation, eased));

            if (t >= 1f) {
                isTransitioning = false;
            }

            return;
        }

        if (activeState.Target == null) {
            return;
        }

        if (!activeState.FollowPosition && !activeState.FollowRotation) {
            return;
        }

        var targetWorldPos = CalculateTargetPosition(activeState, transform.position);
        var targetWorldRot = CalculateTargetRotation(activeState, transform.rotation);

        ApplyPose(targetWorldPos, targetWorldRot);
    }

    private Vector3 CalculateTargetPosition(TargetState state, Vector3 fallbackPosition) {
        if (!state.FollowPosition || state.Target == null) {
            return fallbackPosition;
        }

        return state.Target.TransformPoint(state.RelativePosition);
    }

    private Quaternion CalculateTargetRotation(TargetState state, Quaternion fallbackRotation) {
        if (!state.FollowRotation || state.Target == null) {
            return fallbackRotation;
        }

        return state.Target.rotation * state.RelativeRotation;
    }

    private void ApplyPose(Vector3 targetWorldPos, Quaternion targetWorldRot) {
        if (rb != null && enableRigidMove) {
            rb.MovePosition(targetWorldPos);
            rb.MoveRotation(targetWorldRot);
            return;
        }

        transform.SetPositionAndRotation(targetWorldPos, targetWorldRot);
    }
}