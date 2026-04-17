using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UniVRM10;


[AddComponentMenu(" 🟠AnimationPlayer", 0)]
public class AnimationPlayer : MonoBehaviour {
    [SerializeField] Animator animator;
    [SerializeField] Transform rotationPivot;
    private Coroutine currentAnimationCoroutine;
    private PlayableGraph playableGraph;
    private AnimationMixerPlayable mixer;
    private readonly System.Collections.Generic.List<AnimationClipPlayable> activePlayables = new System.Collections.Generic.List<AnimationClipPlayable>();
    private Vector3 defaultLocalPosition;
    private Quaternion defaultLocalRotation;
    private Vector3 rotationPivotLocalPosition;
    private Vector3 currentPositionOffset;
    private Quaternion currentRotationOffset = Quaternion.identity;

    void Awake() {
        // アニメータが設定されていない場合、自分を含む子オブジェクトからAnimatorを探して設定
        if (animator == null) {
            animator = GetComponentInChildren<Animator>();
            if (animator == null) {
                Debug.LogError($"AnimationPlayer requires an Animator component. Please assign it in the inspector or ensure an Animator is present in children. GameObject: {this.gameObject.name}");
            }
        }

        defaultLocalPosition = animator.transform.localPosition;
        defaultLocalRotation = animator.transform.localRotation;
        rotationPivotLocalPosition = rotationPivot != null ? rotationPivot.localPosition : Vector3.zero;

        playableGraph = PlayableGraph.Create("StrikerAnimationGraph");
        playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        // クロスフェード用に2スロット作成
        mixer = AnimationMixerPlayable.Create(playableGraph, 2);
        var output = AnimationPlayableOutput.Create(playableGraph, "Animation", animator);
        output.SetSourcePlayable(mixer);


    }

    void OnDestroy() {
        if (playableGraph.IsValid()) {
            playableGraph.Destroy();
        }
    }

    public void PlayAnimation(StrikerAnimationClip animation, Action onComplete = null) {
        PlayAnimation(animation, Vector3.zero, Vector3.zero, onComplete);
    }

    public void PlayAnimation(StrikerAnimationClip animation, Vector3 positionOffset, Vector3 rotationOffset, Action onComplete = null) {
        if (animator == null || animation.clip == null) return;

        var blendDuration = Mathf.Max(0f, animation.fadeTime);
        var targetRotationOffset = Quaternion.Euler(rotationOffset);

        if (currentAnimationCoroutine != null) {
            StopCoroutine(currentAnimationCoroutine);
            currentAnimationCoroutine = null;
        }

        currentAnimationCoroutine = StartCoroutine(PlayAnimationCoroutine(
            animation.clip,
            blendDuration,
            animation.speed,
            positionOffset,
            targetRotationOffset,
            onComplete));
    }

    private void ApplyAnimationOffset(Vector3 positionOffset, Quaternion rotationDelta) {
        var targetLocalPosition = defaultLocalPosition + positionOffset;
        var targetLocalRotation = defaultLocalRotation * rotationDelta;

        if (rotationPivot != null) {
            var offsetFromPivot = targetLocalPosition - rotationPivotLocalPosition;
            targetLocalPosition = rotationPivotLocalPosition + rotationDelta * offsetFromPivot;
        }

        animator.transform.SetLocalPositionAndRotation(targetLocalPosition, targetLocalRotation);
    }

    private IEnumerator PlayAnimationCoroutine(
        AnimationClip clip,
        float fadeTime,
        float speed,
        Vector3 targetPositionOffset,
        Quaternion targetRotationOffset,
        Action onComplete) {
        var startPositionOffset = currentPositionOffset;
        var startRotationOffset = currentRotationOffset;

        // 既存のアクティブなプレイアブル群を一つ後ろにシフトして
        // 新しいプレイアブルをスロット0に挿入できるようにする
        int oldCount = activePlayables.Count;
        float[] startWeights = new float[oldCount];
        for (int i = 0; i < oldCount; ++i) startWeights[i] = mixer.GetInputWeight(i);

        // 必要ならミキサーの入力数を拡張
        mixer.SetInputCount(oldCount + 1);

        // 既存接続を後ろへ移動（後ろから行う）
        for (int i = oldCount - 1; i >= 0; --i) {
            mixer.DisconnectInput(i);
            mixer.ConnectInput(i + 1, activePlayables[i], 0);
            mixer.SetInputWeight(i + 1, startWeights[i]);
        }

        // 新しいアニメーションを作成してスロット0に接続
        var newPlayable = AnimationClipPlayable.Create(playableGraph, clip);
        newPlayable.SetSpeed(speed);
        newPlayable.SetTime(0);
        mixer.ConnectInput(0, newPlayable, 0);
        mixer.SetInputWeight(0, 0f);
        activePlayables.Insert(0, newPlayable);

        playableGraph.Play();

        if (fadeTime > 0f && oldCount > 0) {
            // クロスフェード: 新しいアニメーションをフェードインし、
            // 既存のすべてのアニメーションをフェードアウト
            float elapsed = 0f;
            while (elapsed < fadeTime) {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeTime);
                mixer.SetInputWeight(0, Mathf.Lerp(0f, 1f, t));
                for (int j = 0; j < oldCount; ++j) {
                    mixer.SetInputWeight(j + 1, Mathf.Lerp(startWeights[j], 0f, t));
                }

                currentPositionOffset = Vector3.Lerp(startPositionOffset, targetPositionOffset, t);
                currentRotationOffset = Quaternion.Slerp(startRotationOffset, targetRotationOffset, t);
                ApplyAnimationOffset(currentPositionOffset, currentRotationOffset);
                yield return null;
            }

            // フェード完了後、以前のプレイアブルを破棄
            mixer.SetInputWeight(0, 1f);
            for (int j = 0; j < oldCount; ++j) {
                int idx = 1; // 常に1に移動しているので繰り返して破棄
                if (mixer.GetInputCount() > idx) {
                    mixer.DisconnectInput(idx);
                }
                activePlayables[idx].Destroy();
                activePlayables.RemoveAt(idx);
            }
            // ミキサーを必要最小数に戻す
            mixer.SetInputCount(1);
        }
        else {
            // フェードなしの場合は即座に切り替え: 新しいを1.0にして以前を全部破棄
            mixer.SetInputWeight(0, 1f);
            for (int j = 0; j < oldCount; ++j) {
                int idx = 1;
                if (mixer.GetInputCount() > idx) {
                    mixer.DisconnectInput(idx);
                }
                activePlayables[idx].Destroy();
                activePlayables.RemoveAt(idx);
            }
            mixer.SetInputCount(1);
        }

        if (fadeTime <= 0f || oldCount == 0) {
            currentPositionOffset = targetPositionOffset;
            currentRotationOffset = targetRotationOffset;
            ApplyAnimationOffset(currentPositionOffset, currentRotationOffset);
        }

        // 新しい挿入したプレイアブル（先頭）が現在のアニメーション
        var currentPlayable = activePlayables.Count > 0 ? activePlayables[0] : default;

        if (!clip.isLooping) {
            float clipDuration = clip.length / Mathf.Max(0.0001f, Mathf.Abs(speed));
            while (currentPlayable.IsValid() && currentPlayable.GetTime() < clipDuration) {
                yield return null;
            }

            // 最後のフレームで止める
            if (currentPlayable.IsValid()) {
                currentPlayable.SetSpeed(0);
                currentPlayable.SetTime(clip.length);
            }

            currentAnimationCoroutine = null;
            onComplete?.Invoke();
        }
        else {
            currentAnimationCoroutine = null;
        }
    }
}
