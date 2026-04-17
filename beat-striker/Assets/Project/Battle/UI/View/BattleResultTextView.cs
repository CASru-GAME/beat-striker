using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Alice {
    public class BattleResultTextView : MonoBehaviour {
        [SerializeField] TextMeshProUGUI battleFinishText;
        [SerializeField] AudioClip finishSound;
        [SerializeField] AudioClip finishVoiceSound;
        [SerializeField] float finishDisplayDuration = 0.4f;
        [SerializeField] float outroDisplayDuration = 1f;

        TaskCompletionSource<bool> battleFinishCompletionSource;
        TaskCompletionSource<bool> outroCompletionSource;

        void Awake() {
            battleFinishText.gameObject.SetActive(false);
        }

        public Task PresentBattleFinishAsync() {
            battleFinishCompletionSource?.TrySetCanceled();
            battleFinishCompletionSource = new TaskCompletionSource<bool>();
            battleFinishText.text = "Finish";
            battleFinishText.gameObject.SetActive(true);
            PlaySound(finishSound);
            PlaySound(finishVoiceSound);
            battleFinishText.transform.localScale = Vector3.one * 10f;

            LeanTween.scale(battleFinishText.gameObject, Vector3.one, 0.8f)
                .setEase(LeanTweenType.easeOutQuad);

            LeanTween.delayedCall(finishDisplayDuration, () => {
                battleFinishText.gameObject.SetActive(false);
                battleFinishCompletionSource?.TrySetResult(true);
            });

            return battleFinishCompletionSource.Task;
        }

        public Task PresentOutroAsync() {
            outroCompletionSource?.TrySetCanceled();
            outroCompletionSource = new TaskCompletionSource<bool>();
            battleFinishText.text = "Game Set";
            battleFinishText.gameObject.SetActive(true);
            PlaySound(finishSound);
            PlaySound(finishVoiceSound);

            var color = battleFinishText.color;
            color.a = 1f;
            battleFinishText.color = color;

            LeanTween.value(battleFinishText.gameObject, 1f, 0f, outroDisplayDuration)
                .setOnUpdate((float alpha) => {
                    var next = battleFinishText.color;
                    next.a = alpha;
                    battleFinishText.color = next;
                })
                .setOnComplete(() => {
                    battleFinishText.gameObject.SetActive(false);
                    outroCompletionSource?.TrySetResult(true);
                });

            return outroCompletionSource.Task;
        }

        void PlaySound(AudioClip clip) {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, Vector3.zero);
        }
    }
}