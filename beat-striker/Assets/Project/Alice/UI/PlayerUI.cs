using UnityEngine;
using VContainer;
using R3;

namespace Alice {
    public class PlayerUI : MonoBehaviour {
        [SerializeField] int playerId;
        [SerializeField] Transform playerPosition;
        [SerializeField] AliceHpBarUI hpBarUI;
        [SerializeField] AliceRingUI ringUI;

        IBeatjudge beatJudge;
        IBattleFlow battleFlow;
        IStrikerRegistry strikerRegistry;
        BeatConfig beatConfig;
        AudioSource audioSource;
        CompositeDisposable disposables = new();

        bool battleStarted;
        bool initialized;

        [Inject]
        public void Construct(IBeatjudge beatJudge, IBattleFlow battleFlow, IStrikerRegistry strikerRegistry, BeatConfig beatConfig, AudioSource audioSource) {
            this.beatJudge = beatJudge;
            this.battleFlow = battleFlow;
            this.strikerRegistry = strikerRegistry;
            this.beatConfig = beatConfig;
            this.audioSource = audioSource;
        }

        void Start() {
            battleFlow.RoundStarted
                .Subscribe(_ => {
                    battleStarted = true;
                    if (initialized) {
                        ringUI.ActivateBattleView();
                    }
                })
                .AddTo(disposables);

            StartCoroutine(InitializeWhenReady());
        }

        System.Collections.IEnumerator InitializeWhenReady() {
            AliceStrikerHub strikerHub = null;
            while (!strikerRegistry.Get(playerId).TryGetValue(out strikerHub)) {
                yield return null;
            }

            var beatPlayer = beatJudge.GetBeatPlayer(playerId);
            hpBarUI.Construct(strikerHub);
            ringUI.Construct(playerId, playerPosition, beatConfig, audioSource, beatPlayer);

            initialized = true;
            if (battleStarted || audioSource.isPlaying) {
                ringUI.ActivateBattleView();
            }
        }

        void OnDestroy() {
            disposables.Dispose();
        }
    }
}
