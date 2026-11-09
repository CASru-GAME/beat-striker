using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Alice {
    public sealed class GamePadMappingDialogView : MonoBehaviour {
        [SerializeField] ActionEmitter skipEmitter;
        [SerializeField] TMP_Text deviceLabel;
        [SerializeField] TMP_Text progress;
        [SerializeField] TMP_Text prompt;
        [SerializeField] TMP_Text status;
        [SerializeField] Image[] markers;
        [SerializeField] Color pendingColor;
        [SerializeField] Color activeColor;
        [SerializeField] Color registeredColor;
        [SerializeField, Min(0f)] float neutralDuration = 0.15f;

        public Observable<Unit> SkipRequested => skipEmitter.OnClickEvent
            .Where(input => input.EventData is not CursorPointerEventData)
            .Select(_ => Unit.Default);
        public float NeutralDuration => neutralDuration;

        public void SetDevice(string label) => deviceLabel.text = label;
        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        public void ShowStep(int step, string position) {
            progress.text = $"{step + 1:00} / {markers.Length:00}";
            prompt.text = position;
            for (var i = 0; i < markers.Length; i++) {
                markers[i].color = i < step ? registeredColor : i == step ? activeColor : pendingColor;
            }
        }

        public void SetStatus(string message) {
            if (status.text != message) status.text = message;
        }
    }
}
