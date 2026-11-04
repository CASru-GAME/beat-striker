using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Alice {
    // A dedicated, prefab-authored source lets panel changes interrupt speech immediately.
    public sealed class TutorialNarrationView : MonoBehaviour {
        [SerializeField] TutorialVoiceLibrary library;
        [SerializeField] AudioSource narrationSource;

        readonly Dictionary<string, AsyncOperationHandle<AudioClip>> loadedClips = new();
        CancellationTokenSource playback;
        TutorialVoiceInput input;
        string[] currentSegments;

        public Task Completion { get; private set; } = Task.CompletedTask;

        public void SetInput(TutorialVoiceInput input) {
            if (this.input == input) return;
            this.input = input;
            // Restart an in-progress input explanation so speech matches the visible layout.
            // Lines without input names (including Final) keep their existing completion task.
            if (playback != null && !Completion.IsCompleted &&
                Array.Exists(currentSegments, segment => segment.StartsWith("$", StringComparison.Ordinal))) {
                Play(currentSegments);
            }
        }
        public void SetVolume(float volume) => narrationSource.volume = Mathf.Clamp01(volume);

        public void Play(TutorialUiKey key) {
            Play(library.GetSegments(key));
        }

        public void Play(string[] segments) {
            Stop();
            currentSegments = segments;
            playback = new CancellationTokenSource();
            Completion = PlayAsync(segments, input, playback.Token);
        }

        public void Stop() {
            playback?.Cancel();
            playback?.Dispose();
            playback = null;
            narrationSource.Stop();
            narrationSource.clip = null;
        }

        async Task PlayAsync(string[] segments, TutorialVoiceInput voiceInput, CancellationToken cancellation) {
            try {
                var handles = new AsyncOperationHandle<AudioClip>[segments.Length];
                // Start all loads together so inserting a button name does not introduce loading gaps.
                for (var i = 0; i < segments.Length; i++) {
                    var id = voiceInput.Resolve(segments[i]);
                    if (!loadedClips.TryGetValue(id, out var handle)) {
                        handle = Addressables.LoadAssetAsync<AudioClip>(library.GetClip(id));
                        loadedClips.Add(id, handle);
                    }
                    handles[i] = handle;
                }
                foreach (var handle in handles) {
                    while (!handle.IsDone) await Awaitable.NextFrameAsync(cancellation);
                    cancellation.ThrowIfCancellationRequested();
                    if (handle.Status != AsyncOperationStatus.Succeeded) {
                        throw new InvalidOperationException("Unable to load tutorial narration.", handle.OperationException);
                    }
                }
                foreach (var handle in handles) {
                    cancellation.ThrowIfCancellationRequested();
                    narrationSource.clip = handle.Result;
                    narrationSource.Play();
                    do {
                        await Awaitable.NextFrameAsync(cancellation);
                    } while (narrationSource.isPlaying);
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
            }
            catch (Exception exception) {
                Debug.LogException(exception, this);
            }
        }

        void OnDisable() => Stop();

        void OnDestroy() {
            Stop();
            foreach (var handle in loadedClips.Values) {
                if (handle.IsValid()) Addressables.Release(handle);
            }
            loadedClips.Clear();
        }
    }
}
