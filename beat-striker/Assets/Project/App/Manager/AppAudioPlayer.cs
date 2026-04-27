using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

namespace Alice {
    public interface IAppAudioPlayer {
        void Initialize(IAudioSetting audioSetting);
        void Play(AudioClip clip);
        void Play(AudioClip clip, Vector3 worldPosition);
    }

    [DisallowMultipleComponent]
    public class AppAudioPlayer : MonoBehaviour, IAppAudioPlayer {
        const string POOL_ROOT_NAME = "App Audio Pool";

        [Header("Pool")]
        [Min(1)]
        [SerializeField] int initialPoolSize = 8;
        [Min(1)]
        [SerializeField] int maxPoolSize = 32;

        [Header("Audio Source")]
        [Range(0f, 1f)]
        [SerializeField] float spatialBlend = 1f;

        IObjectPool<AudioSource> pool;
        Transform poolRoot;
        IAudioSetting audioSetting;

        void Awake() {
            EnsurePool();
        }

        public void Initialize(IAudioSetting audioSetting) {
            this.audioSetting = audioSetting;
            EnsurePool();
        }

        public void Play(AudioClip clip) {
            Play(clip, transform.position);
        }

        public void Play(AudioClip clip, Vector3 worldPosition) {
            if (!clip) {
                return;
            }

            EnsurePool();

            var audioSource = pool.Get();
            audioSource.transform.position = worldPosition;
            audioSource.clip = clip;
            audioSource.volume = ResolveSeVolume();
            audioSource.spatialBlend = spatialBlend;
            audioSource.Play();

            StartCoroutine(ReleaseWhenFinished(audioSource, clip.length));
        }

        void EnsurePool() {
            if (pool != null) {
                return;
            }

            if (maxPoolSize < initialPoolSize) {
                maxPoolSize = initialPoolSize;
            }

            var poolRootObject = new GameObject(POOL_ROOT_NAME);
            poolRootObject.transform.SetParent(transform, false);
            poolRoot = poolRootObject.transform;

            pool = new ObjectPool<AudioSource>(
                CreateAudioSource,
                OnGet,
                OnRelease,
                OnDestroyAudioSource,
                collectionCheck: false,
                defaultCapacity: initialPoolSize,
                maxSize: maxPoolSize
            );

            for (var i = 0; i < initialPoolSize; i++) {
                var audioSource = pool.Get();
                pool.Release(audioSource);
            }
        }

        AudioSource CreateAudioSource() {
            var audioSourceObject = new GameObject("Pooled Audio Source");
            audioSourceObject.transform.SetParent(poolRoot, false);

            var audioSource = audioSourceObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = spatialBlend;
            audioSourceObject.SetActive(false);
            return audioSource;
        }

        void OnGet(AudioSource audioSource) {
            audioSource.gameObject.SetActive(true);
        }

        void OnRelease(AudioSource audioSource) {
            if (!audioSource) {
                return;
            }

            audioSource.Stop();
            audioSource.clip = null;
            audioSource.transform.SetParent(poolRoot, true);
            audioSource.gameObject.SetActive(false);
        }

        void OnDestroyAudioSource(AudioSource audioSource) {
            if (audioSource) {
                Destroy(audioSource.gameObject);
            }
        }

        IEnumerator ReleaseWhenFinished(AudioSource audioSource, float clipLength) {
            yield return new WaitForSeconds(clipLength);

            if (audioSource) {
                pool.Release(audioSource);
            }
        }

        float ResolveSeVolume() {
            if (audioSetting == null) {
                return 1f;
            }

            var volumeBalance = audioSetting.VolumeBalance.CurrentValue;
            return Mathf.Clamp01(volumeBalance.MasterVolume * volumeBalance.SeVolume);
        }
    }
}
