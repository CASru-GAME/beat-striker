using UnityEngine;

namespace Alice {
    public interface IAppBGMPlayer {
        void Play(AudioClip clip);
        void Stop();
        void Resume();
    }

    public class AppBGMPlayer : MonoBehaviour, IAppBGMPlayer {
        [SerializeField] AudioSource audioSource;
        bool isPausedByStop;

        public void Play(AudioClip clip) {
            if (clip == null) {
                Stop();
                return;
            }

            if (audioSource.clip == clip) {
                if (isPausedByStop) {
                    audioSource.UnPause();
                    isPausedByStop = false;
                    return;
                }

                if (audioSource.isPlaying) {
                    return;
                }
            }

            audioSource.loop = true;
            audioSource.clip = clip;
            audioSource.Play();
            isPausedByStop = false;
        }

        public void Stop() {
            if (audioSource.clip == null) {
                return;
            }

            audioSource.Pause();
            isPausedByStop = true;
        }

        public void Resume() {
            if (!isPausedByStop) {
                return;
            }

            audioSource.UnPause();
            isPausedByStop = false;
        }

    }
}
