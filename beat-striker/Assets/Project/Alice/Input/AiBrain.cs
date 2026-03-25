using R3;
using UnityEngine;
using VContainer;

namespace Alice {
    public abstract class AiBrain : MonoBehaviour, IGamePad {
        [SerializeField] protected Vector2 directionInput = Vector2.up;

        readonly Subject<Vector2> onDirection = new();
        readonly Subject<Unit> onDirectionCanceled = new();
        readonly Subject<GamePadButton> onButtonDown = new();
        readonly Subject<GamePadButton> onButtonUp = new();

        BeatConfig beatConfig;
        AudioSource audioSource;
        float[] beats;
        int beatIndex;
        int lastTriggeredBeatIndex = -1;
        bool isAiMode;

        public Observable<Vector2> OnDirectionAsObservable => onDirection;
        public Observable<Unit> OnDirectionCanceledAsObservable => onDirectionCanceled;
        public Observable<GamePadButton> OnButtonDownAsObservable => onButtonDown;
        public Observable<GamePadButton> OnButtonUpAsObservable => onButtonUp;
        public string DeviceName => nameof(AiBrain);

        [Inject]
        public void Construct(BeatConfig beatConfig, AudioSource audioSource) {
            this.beatConfig = beatConfig;
            this.audioSource = audioSource;
        }

        public void SetAiMode(bool isAiMode) {
            if (this.isAiMode == isAiMode) {
                return;
            }

            this.isAiMode = isAiMode;
            if (isAiMode) {
                OnAiEnabled();
            } else {
                OnAiDisafaaegag();
                CancelDirection();
            }
        }

        void Start() {
            beats = beatConfig.SelectedTrack.beats;
            beatIndex = 0;
        }

        void Update() {
            if (!isAiMode) {
                return;
            }

            if (beatIndex >= beats.Length) {
                return;
            }

            var judgeTime = audioSource.time + beatConfig.CommandTimeOffset;
            while (beatIndex < beats.Length && judgeTime > beats[beatIndex] + beatConfig.PerfectWindow) {
                beatIndex++;
            }

            if (beatIndex >= beats.Length) {
                return;
            }

            var windowStart = beats[beatIndex] - beatConfig.PerfectWindow;
            if (judgeTime < windowStart || lastTriggeredBeatIndex == beatIndex) {
                return;
            }

            EmitDirection(directionInput);
            Press(OnBeat(beatIndex));
            lastTriggeredBeatIndex = beatIndex;
            beatIndex++;
        }

        protected void EmitDirection(Vector2 direction) {
            onDirection.OnNext(direction);
        }

        protected void CancelDirection() {
            onDirectionCanceled.OnNext(Unit.Default);
        }

        protected void Press(GamePadButton button) {
            onButtonDown.OnNext(button);
            onButtonUp.OnNext(button);
        }

        protected abstract GamePadButton OnBeat(int beatIndex);
        protected virtual void OnAiEnabled() { }
        protected virtual void OnAiDisafaaegag() { }

        void OnDestroy() {
            onDirection.Dispose();
            onDirectionCanceled.Dispose();
            onButtonDown.Dispose();
            onButtonUp.Dispose();
        }
    }
}