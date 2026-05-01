using System.Collections;
using TMPro;
using UnityEngine;

namespace Alice {
    public class AttentionTextView : MonoBehaviour {
        [SerializeField] TextMeshProUGUI attentionText;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] string defaultTechniqueText = "SPECIAL";

        [Header("Show Animation")]
        [SerializeField] float showDuration = 0.18f;
        [SerializeField] float showDelay = 0.06f;

        [Header("Character Shrink Animation")]
        [SerializeField] float characterShrinkStartDelay = 0.02f;
        [SerializeField] float characterShrinkDuration = 0.16f;
        [SerializeField] float characterShrinkStagger = 0.03f;
        [SerializeField] float characterStartScaleMultiplier = 4.8f;

        [Header("Hide Animation")]
        [SerializeField] float hideDuration = 0.14f;
        [SerializeField] float hideDelay = 0.06f;
        [SerializeField] float hideScale = 0.65f;

        [Header("Sound")]
        [SerializeField] AudioClip shrinkImpactSound;
        [SerializeField] float soundVolume = 1f;
        [SerializeField] float impactTriggerDelayFromShowStart = 0.08f;

        [SerializeField] ParticleSystem effectParticleSystem;

        Vector3 settledScale;
        Coroutine impactCoroutine;
        Coroutine characterShrinkCoroutine;
        Vector3[][] cachedCharacterVertices;

        public float HideDelay => hideDelay;

        void Awake() {
            settledScale = attentionText.rectTransform.localScale;
            if (settledScale.sqrMagnitude <= 0.000001f) {
                settledScale = Vector3.one;
            }
        }

        public void Show(string techniqueText) {
            var fallbackText = string.IsNullOrWhiteSpace(defaultTechniqueText) ? "SPECIAL" : defaultTechniqueText;
            var displayText = string.IsNullOrWhiteSpace(techniqueText) ? fallbackText : techniqueText;

            if (settledScale.sqrMagnitude <= 0.000001f) {
                settledScale = attentionText.rectTransform.localScale;
                if (settledScale.sqrMagnitude <= 0.000001f) {
                    settledScale = Vector3.one;
                }
            }

            StopCurrentTweens();
            gameObject.SetActive(true);
            attentionText.text = displayText;
            canvasGroup.alpha = 0f;
            attentionText.rectTransform.localScale = settledScale;
            BuildCharacterVertexCache();
            var visibleCharacterCount = CountVisibleCharacters(attentionText.textInfo);
            var sequenceDuration = Mathf.Max(
                0.01f,
                characterShrinkStartDelay + characterShrinkDuration + characterShrinkStagger * Mathf.Max(visibleCharacterCount - 1, 0)
            );
            ApplyUniformCharacterScale(characterStartScaleMultiplier);

            LeanTween.value(gameObject, 0f, 1f, sequenceDuration)
                .setDelay(showDelay)
                .setEase(LeanTweenType.easeOutQuad)
                .setOnUpdate((float alpha) => {
                    canvasGroup.alpha = alpha;
                });

            characterShrinkCoroutine = StartCoroutine(PlayCharacterShrink(showDelay + characterShrinkStartDelay));
            impactCoroutine = StartCoroutine(PlayImpactAfterDelay(showDelay + impactTriggerDelayFromShowStart));
        }

        public void Hide() {
            if (!gameObject.activeSelf) {
                return;
            }

            StopCurrentTweens();
            var currentAlpha = canvasGroup.alpha;
            var targetScale = settledScale * hideScale;

            LeanTween.value(gameObject, currentAlpha, 0f, hideDuration)
                .setEase(LeanTweenType.easeInQuad)
                .setOnUpdate((float alpha) => {
                    canvasGroup.alpha = alpha;
                });

            LeanTween.scale(attentionText.rectTransform, targetScale, hideDuration)
                .setEase(LeanTweenType.easeInBack)
                .setOnComplete(() => {
                    gameObject.SetActive(false);
                });
        }

        public void HideImmediately() {
            StopCurrentTweens();
            canvasGroup.alpha = 0f;
            attentionText.rectTransform.localScale = settledScale;
            gameObject.SetActive(false);
        }

        void StopCurrentTweens() {
            LeanTween.cancel(gameObject);
            LeanTween.cancel(attentionText.rectTransform);

            if (impactCoroutine != null) {
                StopCoroutine(impactCoroutine);
                impactCoroutine = null;
            }

            if (characterShrinkCoroutine != null) {
                StopCoroutine(characterShrinkCoroutine);
                characterShrinkCoroutine = null;
            }

            RestoreCharacterVertices();
        }

        IEnumerator PlayImpactAfterDelay(float delay) {
            if (delay > 0f) {
                yield return Ex.Wait(delay);
            }

            effectParticleSystem.Play();
            shrinkImpactSound.PlayAtApp(Vector3.zero, soundVolume);
            impactCoroutine = null;
        }

        IEnumerator PlayCharacterShrink(float startDelay) {
            if (startDelay > 0f) {
                yield return Ex.Wait(startDelay);
            }

            var textInfo = attentionText.textInfo;
            var visibleCharacterCount = CountVisibleCharacters(textInfo);
            if (visibleCharacterCount == 0) {
                characterShrinkCoroutine = null;
                yield break;
            }

            var totalDuration = characterShrinkDuration + characterShrinkStagger * (visibleCharacterCount - 1);
            var elapsed = 0f;

            while (elapsed < totalDuration) {
                UpdateCharacterShrink(textInfo, elapsed);
                attentionText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);

                elapsed += Time.deltaTime;
                yield return null;
            }

            ApplyUniformCharacterScale(1f);
            characterShrinkCoroutine = null;
        }

        int CountVisibleCharacters(TMP_TextInfo textInfo) {
            var count = 0;
            for (var i = 0; i < textInfo.characterCount; i++) {
                if (textInfo.characterInfo[i].isVisible) {
                    count++;
                }
            }

            return count;
        }

        void BuildCharacterVertexCache() {
            attentionText.ForceMeshUpdate();
            var textInfo = attentionText.textInfo;
            cachedCharacterVertices = new Vector3[textInfo.meshInfo.Length][];

            for (var i = 0; i < textInfo.meshInfo.Length; i++) {
                var sourceVertices = textInfo.meshInfo[i].vertices;
                cachedCharacterVertices[i] = new Vector3[sourceVertices.Length];
                sourceVertices.CopyTo(cachedCharacterVertices[i], 0);
            }
        }

        void UpdateCharacterShrink(TMP_TextInfo textInfo, float elapsed) {
            var visibleIndex = 0;
            for (var i = 0; i < textInfo.characterCount; i++) {
                if (!textInfo.characterInfo[i].isVisible) {
                    continue;
                }

                var charStart = visibleIndex * characterShrinkStagger;
                var t = Mathf.Clamp01((elapsed - charStart) / characterShrinkDuration);
                var eased = EaseOutCubic(t);
                var scale = Mathf.Lerp(characterStartScaleMultiplier, 1f, eased);
                ApplyScaleToCharacter(textInfo.characterInfo[i], scale);
                visibleIndex++;
            }
        }

        void ApplyUniformCharacterScale(float scale) {
            var textInfo = attentionText.textInfo;
            for (var i = 0; i < textInfo.characterCount; i++) {
                if (!textInfo.characterInfo[i].isVisible) {
                    continue;
                }

                ApplyScaleToCharacter(textInfo.characterInfo[i], scale);
            }

            attentionText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }

        void ApplyScaleToCharacter(TMP_CharacterInfo characterInfo, float scale) {
            var materialIndex = characterInfo.materialReferenceIndex;
            var vertexIndex = characterInfo.vertexIndex;
            var sourceVertices = cachedCharacterVertices[materialIndex];
            var destinationVertices = attentionText.textInfo.meshInfo[materialIndex].vertices;

            var center = (sourceVertices[vertexIndex] + sourceVertices[vertexIndex + 2]) * 0.5f;

            for (var i = 0; i < 4; i++) {
                var source = sourceVertices[vertexIndex + i];
                destinationVertices[vertexIndex + i] = center + (source - center) * scale;
            }
        }

        float EaseOutCubic(float t) {
            var inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        void RestoreCharacterVertices() {
            if (cachedCharacterVertices == null || cachedCharacterVertices.Length == 0) {
                return;
            }

            var textInfo = attentionText.textInfo;
            var meshCount = Mathf.Min(textInfo.meshInfo.Length, cachedCharacterVertices.Length);
            for (var i = 0; i < meshCount; i++) {
                var destination = textInfo.meshInfo[i].vertices;
                var source = cachedCharacterVertices[i];
                var copyLength = Mathf.Min(destination.Length, source.Length);
                for (var j = 0; j < copyLength; j++) {
                    destination[j] = source[j];
                }
            }

            attentionText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }
    }
}
