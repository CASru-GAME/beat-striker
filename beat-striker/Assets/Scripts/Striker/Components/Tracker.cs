using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu(" 🟠Tracker", 0)]
[RequireComponent(typeof(Rigidbody))]
public class Tracker : MonoBehaviour {
    [SerializeField] Transform target;

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

        public TargetState(TargetHandle handle, Transform target, Vector3 relativePosition, Quaternion relativeRotation) {
            Handle = handle;
            Target = target;
            RelativePosition = relativePosition;
            RelativeRotation = relativeRotation;
        }
    }

    // 元のターゲット状態を保存
    private TargetState originalState;

    // 追加順序を管理（後に追加されたものが優先）
    private readonly List<TargetState> targetStates = new();

    void Start() {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (target != null) {
            CaptureRelativeTransform(target);
        }

        // 元の状態を保存
        originalState = new TargetState(null, target, relativePosition, relativeRotation);
    }

    /// <summary>
    /// ターゲットを追加する。引数なしの場合は追従を停止する。
    /// 最後に追加されたターゲットが優先される。
    /// </summary>
    /// <returns>削除用のハンドル</returns>
    public TargetHandle AddTarget(Transform newTarget = null) {
        var handle = new TargetHandle();

        Vector3 pos = default;
        Quaternion rot = default;

        if (newTarget != null) {
            pos = newTarget.InverseTransformPoint(transform.position);
            rot = Quaternion.Inverse(newTarget.rotation) * transform.rotation;
        }

        var state = new TargetState(handle, newTarget, pos, rot);
        targetStates.Add(state);

        // 最新のターゲットをアクティブにする
        ApplyTargetState(state);

        return handle;
    }

    /// <summary>
    /// 指定したハンドルのターゲットを削除する。
    /// 削除後は次に新しいターゲット、なければ元のターゲットに戻る。
    /// </summary>
    public void RemoveTarget(TargetHandle handle) {
        int index = targetStates.FindIndex(s => s.Handle == handle);
        if (index < 0) return;

        targetStates.RemoveAt(index);

        // 残っているターゲットがあれば最新のものを適用、なければ元に戻る
        if (targetStates.Count > 0) {
            ApplyTargetState(targetStates[^1]);
        }
        else {
            ApplyTargetState(originalState);
        }
    }

    private void ApplyTargetState(TargetState state) {
        target = state.Target;
        relativePosition = state.RelativePosition;
        relativeRotation = state.RelativeRotation;
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

    void FixedUpdate() {
        if (target == null) return;

        // 保存した相対位置を、現在のターゲットの向きに合わせてワールド座標に変換
        Vector3 targetWorldPos = target.TransformPoint(relativePosition);

        // 保存した相対回転を、現在のターゲットの回転に適用
        Quaternion targetWorldRot = target.rotation * relativeRotation;

        // 物理移動
        rb.MovePosition(targetWorldPos);
        rb.MoveRotation(targetWorldRot);
    }
}