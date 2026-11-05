using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Alice {
    [Flags]
    public enum TutorialControl {
        None = 0, North = 1, South = 2, West = 4, East = 8, Start = 16, Select = 32,
        Up = 64, Down = 128, Left = 256, Right = 512,
    }
    public record TutorialInputCue(TutorialControl Button, Vector2 Direction, bool AlternateDirection = false);
    public record TutorialGuideFrame(TutorialInputCue Cue, float Press, float DirectionAmount, float Ripple);
    [Serializable]
    public record TutorialControlBinding {
        public TutorialControl Control;
        public TutorialControlGlyph Face;
        public TMP_Text Label;
    }
    public sealed class TutorialInputGuideView : MonoBehaviour {
        [SerializeField] GameObject keyboardLayout;
        [SerializeField] GameObject gamepadLayout;
        [SerializeField] TutorialControlBinding[] keyboardControls;
        [SerializeField] TutorialControlBinding[] gamepadControls;
        [SerializeField] TutorialControlGlyph stickBase;
        [SerializeField] TutorialControlGlyph stickCap;
        [SerializeField] RectTransform stickDirection;
        [Header("Sequence Timing")]
        [SerializeField, Min(0.05f)] float beatSeconds = 0.55f;
        [SerializeField, Min(2f)] float repeatPauseBeats = 3f;
        [SerializeField, Range(0f, 0.4f)] float directionLeadBeats = 0.35f;
        [SerializeField, Range(0.01f, 0.2f)] float pressBeats = 0.1f;
        [SerializeField, Range(0.01f, 0.25f)] float holdBeats = 0.2f;
        [SerializeField, Range(0.01f, 0.2f)] float releaseBeats = 0.15f;
        [Header("Motion")]
        [SerializeField, Range(0f, 0.5f)] float pressScaleReduction = 0.25f;
        [SerializeField] float keyTravel = 3f;
        [SerializeField] float stickTravel = 11f;
        [SerializeField] Color idleLabel = new(0.69f, 0.73f, 0.82f, 1f);
        [SerializeField] Color activeLabel = new(1f, 0.77f, 0.77f, 1f);
        TutorialInputCue[] sequence = Array.Empty<TutorialInputCue>();
        bool keyboard, beatSynchronized;
        float startedAt, lastPulseAt = float.NegativeInfinity;
        int pulseIndex = -1;
        public void SetBeatSeconds(float seconds) => beatSeconds = seconds;
        public void SetLayout(bool keyboard) {
            this.keyboard = keyboard;
            keyboardLayout.SetActive(keyboard);
            gamepadLayout.SetActive(!keyboard);
        }
        public void Show(TutorialInputCue cue) => ShowSequence(new[] { cue });
        public void ShowSequence(IReadOnlyList<TutorialInputCue> next) {
            var same = sequence.Length == next.Count;
            for (var i = 0; same && i < sequence.Length; i++) same = sequence[i] == next[i];
            if (!same || !gameObject.activeSelf) {
                startedAt = Time.unscaledTime;
                beatSynchronized = false;
                pulseIndex = -1;
                lastPulseAt = float.NegativeInfinity;
                sequence = new TutorialInputCue[next.Count];
                for (var i = 0; i < next.Count; i++) sequence[i] = next[i];
            }
            gameObject.SetActive(true);
            Render();
        }
        public void Hide() { beatSynchronized = false; gameObject.SetActive(false); }
        public void PulseOnBeat() {
            if (Time.unscaledTime - lastPulseAt < beatSeconds * 0.5f) return;
            beatSynchronized = true;
            lastPulseAt = startedAt = Time.unscaledTime;
            pulseIndex = (pulseIndex + 1) % Mathf.Max(1, sequence.Length);
        }
        void Update() => Render();
        float Length(TutorialInputCue cue) => cue.Button == TutorialControl.None ? 2f : 1f;

        // Timeline playback is independent of the player's progress and character animation.
        TutorialGuideFrame Evaluate(float seconds) {
            var total = 0f;
            foreach (var cue in sequence) total += Length(cue);
            var elapsed = seconds / beatSeconds;
            var time = beatSynchronized ? elapsed : elapsed % (total + repeatPauseBeats);
            var index = beatSynchronized ? pulseIndex : 0;
            if (!beatSynchronized) {
                while (index < sequence.Length && time >= Length(sequence[index])) time -= Length(sequence[index++]);
            }
            if (index < 0 || index >= sequence.Length) return new(new(TutorialControl.None, Vector2.zero), 0f, 0f, 0f);
            var current = sequence[index];
            var direction = current.Direction;
            if (current.AlternateDirection && (int)(elapsed / (total + repeatPauseBeats)) % 2 != 0) direction.x *= -1f;
            var length = Length(current);
            var directionAmount = direction.sqrMagnitude > 0f
                ? Mathf.SmoothStep(0f, 1f, time / 0.2f) *
                    (1f - Mathf.SmoothStep(0f, 1f, (time - length + 0.2f) / 0.2f))
                : 0f;
            var buttonTime = time - (beatSynchronized ? 0f : directionLeadBeats);
            var press = Mathf.SmoothStep(0f, 1f, buttonTime / pressBeats) *
                (1f - Mathf.SmoothStep(0f, 1f, (buttonTime - pressBeats - holdBeats) / releaseBeats));
            return new(current with { Direction = direction }, press, directionAmount,
                Mathf.Clamp01(buttonTime / (pressBeats + holdBeats + releaseBeats)));
        }
        void Render() {
            if (sequence.Length == 0) return;
            var frame = Evaluate(Time.unscaledTime - startedAt);
            var direction = frame.Cue.Direction;
            var directions = TutorialControl.None;
            if (direction.x > 0f) directions |= TutorialControl.Right;
            if (direction.x < 0f) directions |= TutorialControl.Left;
            if (direction.y > 0f) directions |= TutorialControl.Up;
            if (direction.y < 0f) directions |= TutorialControl.Down;
            foreach (var binding in keyboard ? keyboardControls : gamepadControls) {
                var isDirection = (binding.Control & directions) != 0;
                var active = isDirection || (binding.Control & frame.Cue.Button) != 0;
                var amount = active ? (isDirection ? frame.DirectionAmount : frame.Press) : 0f;
                binding.Face.rectTransform.localScale = Vector3.one * (1f - amount * pressScaleReduction);
                binding.Label.rectTransform.anchoredPosition = Vector2.down * amount * keyTravel;
                binding.Label.color = Color.Lerp(idleLabel, activeLabel, amount);
                binding.Face.SetMotion(amount, amount, active && !isDirection ? frame.Ripple : 0f);
            }
            if (keyboard) return;
            stickCap.rectTransform.anchoredPosition = direction.normalized * (stickTravel * frame.DirectionAmount);
            stickCap.SetMotion(frame.DirectionAmount, frame.DirectionAmount, 0f);
            stickBase.SetMotion(0f, 0f, 0f);
            stickDirection.gameObject.SetActive(frame.DirectionAmount > 0f);
            stickDirection.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
        }
    }
}
