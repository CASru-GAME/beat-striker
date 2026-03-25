using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UniVRM10;

namespace Alice {

    [AddComponentMenu(" 🟠AnimationPlayer", 0)]
    public class AnimationPlayer : MonoBehaviour {
        [SerializeField] Animator animator;
        private Coroutine currentAnimationCoroutine;
        private PlayableGraph playableGraph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable currentClipPlayable;
        private AnimationClipPlayable previousClipPlayable;

        void Awake() {
            // アニメータが設定されていない場合、自分を含む子オブジェクトからAnimatorを探して設定
            if (animator == null) {
                animator = GetComponentInChildren<Animator>();
                if (animator == null) {
                    Debug.LogError($"AnimationPlayer requires an Animator component. Please assign it in the inspector or ensure an Animator is present in children. GameObject: {this.gameObject.name}");
                }
            }

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
            if (animator == null || animation.clip == null) return;

            if (currentAnimationCoroutine != null) {
                StopCoroutine(currentAnimationCoroutine);
            }

            currentAnimationCoroutine = StartCoroutine(PlayAnimationCoroutine(animation.clip, animation.fadeTime, animation.speed, () => {
                onComplete?.Invoke();
            }));
        }

        private IEnumerator PlayAnimationCoroutine(AnimationClip clip, float fadeTime, float speed, Action onComplete) {
            // 前のアニメーションをクリーンアップ
            if (previousClipPlayable.IsValid()) {
                mixer.DisconnectInput(1);
                previousClipPlayable.Destroy();
            }

            // 現在のアニメーションを前のスロットに移動
            if (currentClipPlayable.IsValid()) {
                previousClipPlayable = currentClipPlayable;
                mixer.DisconnectInput(0);
                mixer.ConnectInput(1, previousClipPlayable, 0);
                mixer.SetInputWeight(1, mixer.GetInputWeight(0));
            }

            // 新しいアニメーションを作成してスロット0に接続
            currentClipPlayable = AnimationClipPlayable.Create(playableGraph, clip);
            currentClipPlayable.SetSpeed(speed);
            currentClipPlayable.SetTime(0);
            mixer.ConnectInput(0, currentClipPlayable, 0);

            playableGraph.Play();

            if (fadeTime > 0f && previousClipPlayable.IsValid()) {
                // クロスフェード: 両方のウェイトを同時に変化
                float elapsed = 0f;
                float startWeightPrev = mixer.GetInputWeight(1);

                while (elapsed < fadeTime) {
                    elapsed += Time.deltaTime;
                    float t = elapsed / fadeTime;
                    mixer.SetInputWeight(0, Mathf.Lerp(0f, 1f, t));      // 新しいアニメーションをフェードイン
                    mixer.SetInputWeight(1, Mathf.Lerp(startWeightPrev, 0f, t)); // 古いアニメーションをフェードアウト
                    yield return null;
                }

                // フェード完了後、前のアニメーションを破棄
                mixer.SetInputWeight(0, 1f);
                mixer.SetInputWeight(1, 0f);
                mixer.DisconnectInput(1);
                previousClipPlayable.Destroy();
                previousClipPlayable = default;
            }
            else {
                // フェードなしの場合は即座に切り替え
                mixer.SetInputWeight(0, 1f);
                if (previousClipPlayable.IsValid()) {
                    mixer.SetInputWeight(1, 0f);
                    mixer.DisconnectInput(1);
                    previousClipPlayable.Destroy();
                    previousClipPlayable = default;
                }
            }

            // ループしないアニメーションの場合は終了を待機して最後のフレームで止める
            if (!clip.isLooping) {
                float clipDuration = clip.length / speed;
                while (currentClipPlayable.IsValid() && currentClipPlayable.GetTime() < clipDuration) {
                    yield return null;
                }

                // 最後のフレームで止める
                if (currentClipPlayable.IsValid()) {
                    currentClipPlayable.SetSpeed(0);
                    currentClipPlayable.SetTime(clip.length);
                }

                currentAnimationCoroutine = null;
                onComplete?.Invoke();
            }
            else {
                // ループアニメーションの場合はコルーチン終了
                currentAnimationCoroutine = null;
            }
        }
    }
}
