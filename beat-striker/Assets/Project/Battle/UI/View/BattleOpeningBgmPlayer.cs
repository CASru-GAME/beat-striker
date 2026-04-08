using System.Collections;
using UnityEngine;

namespace Alice {
    public interface IBattleOpeningBgmPlayer {
        void Play();
        void Stop();
    }

    public class BattleOpeningBgmPlayer : MonoBehaviour, IBattleOpeningBgmPlayer {
        [SerializeField] AudioClip openingBgmPrimary;
        [SerializeField] AudioClip openingBgmSecondary;
        [SerializeField] float volume = 1f;
        [SerializeField] float startDelaySeconds = 0f;
        [SerializeField] float fadeInDuration = 0.5f;
        [SerializeField] float fadeOutDuration = 0.5f;

        sealed class ManagedAudioSource {
            public AudioSource Source;
            public Coroutine PlayRoutine;
            public Coroutine StopRoutine;
            public AudioClip Clip;
        }

        readonly ManagedAudioSource[] managedAudioSources = new ManagedAudioSource[2];

        void Awake() {
            InitializeAudioSources();
        }

        public void Play() {
            InitializeAudioSources();

            if (openingBgmPrimary == null && openingBgmSecondary == null) {
                Stop();
                return;
            }

            for (var i = 0; i < managedAudioSources.Length; i++) {
                var managedAudioSource = managedAudioSources[i];
                StopScheduledPlay(managedAudioSource);
                BeginFadeOut(managedAudioSource);

                managedAudioSource.Clip = i == 0 ? openingBgmPrimary : openingBgmSecondary;
                if (managedAudioSource.Clip == null) {
                    continue;
                }

                managedAudioSource.Source.Stop();
                managedAudioSource.Source.loop = true;
                managedAudioSource.Source.clip = managedAudioSource.Clip;
                managedAudioSource.Source.volume = 0f;
                managedAudioSource.PlayRoutine = StartCoroutine(PlayRoutine(managedAudioSource));
            }
        }

        public void Stop() {
            InitializeAudioSources();

            for (var i = 0; i < managedAudioSources.Length; i++) {
                BeginFadeOut(managedAudioSources[i]);
            }
        }

        void InitializeAudioSources() {
            for (var i = 0; i < managedAudioSources.Length; i++) {
                if (managedAudioSources[i] != null) {
                    continue;
                }

                managedAudioSources[i] = new ManagedAudioSource {
                    Source = CreateAudioSource($"BattleOpeningBgmSource{i}")
                };
            }
        }

        AudioSource CreateAudioSource(string objectName) {
            var audioObject = new GameObject(objectName);
            audioObject.transform.SetParent(transform, false);

            var source = audioObject.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.volume = volume;
            source.spatialBlend = 0f;
            return source;
        }

        void StopScheduledPlay(ManagedAudioSource managedAudioSource) {
            if (managedAudioSource.PlayRoutine != null) {
                StopCoroutine(managedAudioSource.PlayRoutine);
                managedAudioSource.PlayRoutine = null;
            }

            if (managedAudioSource.StopRoutine != null) {
                StopCoroutine(managedAudioSource.StopRoutine);
                managedAudioSource.StopRoutine = null;
            }
        }

        void BeginFadeOut(ManagedAudioSource managedAudioSource) {
            StopScheduledPlay(managedAudioSource);

            if (!managedAudioSource.Source.isPlaying) {
                managedAudioSource.Source.Stop();
                managedAudioSource.Source.volume = volume;
                return;
            }

            managedAudioSource.StopRoutine = StartCoroutine(StopRoutine(managedAudioSource));
        }

        IEnumerator PlayRoutine(ManagedAudioSource managedAudioSource) {
            if (startDelaySeconds > 0f) {
                yield return new WaitForSeconds(startDelaySeconds);
            }

            if (managedAudioSource.Source == null || managedAudioSource.Clip == null) {
                managedAudioSource.PlayRoutine = null;
                yield break;
            }

            managedAudioSource.Source.Play();

            if (fadeInDuration <= 0f) {
                managedAudioSource.Source.volume = volume;
                managedAudioSource.PlayRoutine = null;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < fadeInDuration) {
                elapsed += Time.deltaTime;
                managedAudioSource.Source.volume = Mathf.Lerp(0f, volume, Mathf.Clamp01(elapsed / fadeInDuration));
                yield return null;
            }

            managedAudioSource.Source.volume = volume;
            managedAudioSource.PlayRoutine = null;
        }

        IEnumerator StopRoutine(ManagedAudioSource managedAudioSource) {
            var startVolume = managedAudioSource.Source.volume;

            if (fadeOutDuration <= 0f) {
                managedAudioSource.Source.Stop();
                managedAudioSource.Source.volume = volume;
                managedAudioSource.StopRoutine = null;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < fadeOutDuration) {
                elapsed += Time.deltaTime;
                managedAudioSource.Source.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / fadeOutDuration));
                yield return null;
            }

            managedAudioSource.Source.Stop();
            managedAudioSource.Source.volume = volume;
            managedAudioSource.StopRoutine = null;
        }
    }
}