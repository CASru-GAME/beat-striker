using System;
using R3;
using TMPro;
using UnityEngine;

namespace Alice {
    public class ChoiceDialogView : MonoBehaviour {
        [SerializeField] GameObject dialogRoot;
        [SerializeField] TMP_Text heading;
        [SerializeField] TMP_Text description;
        [SerializeField] TMP_InputField input;
        [SerializeField] ActionEmitter[] emitters;
        [SerializeField] TMP_Text[] labels;
        readonly Subject<int> selected = new();
        readonly Subject<Unit> destroyed = new();
        int shownFrame = -1;
        int inputPlayerId = -1;
        string[] options = System.Array.Empty<string>();
        public Observable<int> Selected => selected;
        public Observable<Unit> Destroyed => destroyed;
        public string InputText => input.text;
        void Awake() {
            for (var i = 0; i < emitters.Length; i++) {
                var index = i;
                emitters[i].OnClickEvent.Subscribe(data => {
                    if (Time.frameCount <= shownFrame || inputPlayerId >= 0 && data.EventData.pointerId >= 0 && data.EventData.pointerId != inputPlayerId) return;
                    selected.OnNext(index);
                }).AddTo(this);
            }
            input.onSubmit.AddListener(SubmitName);
        }
        public void Show(string title, string body, string[] options, string initialInput = null, int inputPlayerId = -1) {
            shownFrame = Time.frameCount; this.inputPlayerId = inputPlayerId;
            this.options = options;
            heading.text = title; description.text = body; description.richText = false;
            for (var i = 0; i < emitters.Length; i++) {
                emitters[i].gameObject.SetActive(i < options.Length);
                if (i < options.Length) labels[i].text = options[i];
            }
            input.gameObject.SetActive(initialInput != null);
            if (initialInput != null) { input.SetTextWithoutNotify(initialInput); input.ActivateInputField(); }
            dialogRoot.SetActive(true);
            if (initialInput != null) input.ActivateInputField();
        }
        public void Hide() { input.DeactivateInputField(); dialogRoot.SetActive(false); }
        public void SetSelection(int index) {
            for (var i = 0; i < options.Length; i++) labels[i].text = (i == index ? "> " : "") + options[i];
        }
        void SubmitName(string text) { if (Time.frameCount > shownFrame && input.gameObject.activeSelf) selected.OnNext(0); }
        void OnDestroy() { input.onSubmit.RemoveListener(SubmitName); destroyed.OnNext(Unit.Default); destroyed.Dispose(); selected.Dispose(); }
    }
}
