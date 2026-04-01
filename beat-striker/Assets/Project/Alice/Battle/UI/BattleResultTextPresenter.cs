using R3;
using TMPro;
using UnityEngine;

namespace Alice {
    public class BattleResultTextPresenter : MonoBehaviour {
        [SerializeField] TextMeshProUGUI battleFinishText;
        [SerializeField] AudioClip finishSound;
        [SerializeField] float soundVolume = 1.0f;
        [SerializeField] float finishDisplayDuration = 1.5f;
        [SerializeField] float outroDisplayDuration = 1f;

        readonly Subject<Unit> finishHiddenSubject = new();
        readonly Subject<Unit> outroFinishedSubject = new();

        public Observable<Unit> FinishHidden => finishHiddenSubject;
        public Observable<Unit> OutroFinished => outroFinishedSubject;

        void Awake() {
            battleFinishText.gameObject.SetActive(false);
        }

        public void PresentBattleFinish() {
            battleFinishText.text = "Finish";
            battleFinishText.gameObject.SetActive(true);
            PlaySound(finishSound);
            battleFinishText.transform.localScale = Vector3.one * 10f;

            LeanTween.scale(battleFinishText.gameObject, Vector3.one, 0.8f)
                .setEase(LeanTweenType.easeOutQuad);

            LeanTween.delayedCall(finishDisplayDuration, () => {
                battleFinishText.gameObject.SetActive(false);
                finishHiddenSubject.OnNext(Unit.Default);
            });
        }

        public void PresentOutro() {
            battleFinishText.text = "Game Set";
            battleFinishText.gameObject.SetActive(true);
            PlaySound(finishSound);

            LeanTween.delayedCall(outroDisplayDuration, () => {
                battleFinishText.gameObject.SetActive(false);
                outroFinishedSubject.OnNext(Unit.Default);
            });
        }

        void PlaySound(AudioClip clip) {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, Vector3.zero, soundVolume);
        }
    }
}