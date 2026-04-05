using Core;
using UnityEngine;

namespace Alice {
    public class BattleSuspendMenuView : MonoBehaviour {
        [SerializeField] GameObject root;
        [SerializeField] Botan suspendButton;
        [SerializeField] Botan resumeButton;
        [SerializeField] float showStartScale = 0.9f;
        [SerializeField] float showScaleDuration = 0.18f;
        [SerializeField] float hideEndScale = 0.9f;
        [SerializeField] float hideScaleDuration = 0.14f;

        public GameObject Root => root;
        public Botan SuspendButton => suspendButton;
        public Botan ResumeButton => resumeButton;
        public float ShowStartScale => showStartScale;
        public float ShowScaleDuration => showScaleDuration;
        public float HideEndScale => hideEndScale;
        public float HideScaleDuration => hideScaleDuration;
    }
}
