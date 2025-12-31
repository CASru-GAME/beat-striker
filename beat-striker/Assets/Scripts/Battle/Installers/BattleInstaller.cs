
using System;
using System.Collections.Generic;
using System.Linq;
using Core.App;
using Core.App.Installers;
using Core.App.Types;
using Core.GamePad;
using Core.GamePad.Models;
using Core.GamePad.Types;
using Core.Utils;
using UnityEngine;

namespace Core.Battle {
    [Serializable]
    public struct StrikerPrefab {
        public StrikerId strikerId;
        public GameObject prefab;
    }

    [Serializable]
    public struct TrackAudio {
        public TrackId trackId;
        public AudioClip audioClip;
    }

    [RequireComponent(typeof(Life))]
    [RequireComponent(typeof(BattleView))]
    [RequireComponent(typeof(AudioSource))]
    public class BattleInstaller : MonoBehaviour, IBattleResetter { // IBattleResetter needed? Models call BattleModel.ResetBattle() now. But keeping for compatibility if referenced elsewhere.
        [SerializeField] float perfectWindow = 0.1f;
        [SerializeField] float goodWindow = 0.2f;
        [SerializeField] float timeOffset = 0f;
        [SerializeField] Transform[] playerTransforms;
        [SerializeField] StrikerPrefab[] strikerPrefabs;
        [SerializeField] TrackAudio[] trackAudios;
        [SerializeField] AudioClip beatClip;
        [SerializeField] int excellentScore = 1000;
        [SerializeField] int goodScore = 500;
        [SerializeField] int specialGain = 10;
        [SerializeField] int playerCount = 2;
        public List<IStrikerModelGetter> strikerModels = new();
        public List<IStrikerView> strikerViews = new();
        public IRythmTrackModelGetter rythmTrackModel;
        public IBattleModel battleModel;
        [SerializeField] bool DebugMode = false;
        [SerializeField] float bpm = 110f;

        [SerializeField] private StrikerId defaultStrikerId;
        private IGamePadInputModel gamePadInputModel;
        private IPlayerRegistry playerRegistry;
        private readonly Dictionary<PlayerId, IStrikerView> strikerViewMap = new();
        private CompositeDisposable gamePadSubscriptions;

        // private BattleEvents battleEvents; // Removed

        void Awake() {
            var app = GameObject.Find("App").GetComponent<AppFlowScope>();
            var settingModel = app.battleSettingModel;
            playerRegistry = app.playerRegistry;
            gamePadInputModel = app.GetGamePadInputModel();
            var trackId = settingModel.Track;

            var view = GetComponent<BattleView>();
            var audioSource = GetComponent<AudioSource>();
            var beatAudioSource = gameObject.AddComponent<AudioSource>();

            var trackAudio = trackAudios.FirstOrDefault(t => t.trackId.Equals(trackId));

            var rule = new ScoreRule(excellentScore, goodScore, specialGain);
            var life = GetComponent<Life>();

            // BattleModel creation
            var battleModel = new BattleModel(playerCount);
            this.battleModel = battleModel;

            var rythmTrackModel = new RythmTrackModel(Enumerable.Range(1, 1000).Select(x => x * 60f / bpm).ToArray(),
                perfectWindow,
                goodWindow,
                timeOffset
            );
            this.rythmTrackModel = rythmTrackModel;

            // Initialize Model Dependencies
            battleModel.InitializeDependencies(rythmTrackModel, ResetBattle, trackId);

            // battleEvents = new BattleEvents(); // Removed

            view.Construct(audioSource, beatAudioSource, trackAudio.audioClip, beatClip);
            view.SetRythmTrackModel(rythmTrackModel);
            view.SetBattleModel(battleModel); // Inject Model into View

            for (int i = 0; i < playerTransforms.Length; i++) {
                var transform = playerTransforms[i];
                var playerId = new PlayerId(i);
                var strikerId = settingModel.GetStriker(playerId);
                if (strikerId.HasValue == false) {
                    strikerId = defaultStrikerId;
                }
                battleModel.SetStriker(playerId, strikerId);
                Debug.Log($"[BattleInstaller] Set Striker {strikerId} for player {playerId}");

                var foundPrefab = strikerPrefabs.FirstOrDefault(x => x.strikerId == strikerId);
                if (foundPrefab.prefab == null) {
                    Debug.LogError($"[BattleInstaller] Striker prefab not found for ID: {(strikerId.HasValue ? strikerId.Value.value : "null")}");
                    continue;
                }

                var instance = Instantiate(foundPrefab.prefab);
                instance.transform.SetPositionAndRotation(transform.position, transform.rotation);
                transform.SetParent(instance.transform);

                var strikerInstaller = instance.GetComponentInChildren<StrikerInstaller>(true);
                if (strikerInstaller != null) {
                    Debug.Log($"[BattleInstaller] Constructing Striker {strikerId} via StrikerInstaller");
                    // StrikerInstaller.Construct signature changed to take IBattleModel
                    var (strikerModel, _, strikerView) = strikerInstaller.Construct(playerId, rule, rythmTrackModel, playerRegistry, battleModel);
                    this.strikerModels.Add(strikerModel);
                    this.strikerViews.Add(strikerView);
                    strikerViewMap[playerId] = strikerView;
                    strikerView.SavePosition();
                    Debug.Log($"[BattleInstaller] Added StrikerModel for player {playerId}");
                }
                else {
                    Debug.Log($"[BattleInstaller] StrikerInstaller not found. Trying StrikerHub/IStrikerViewWithEvents for {strikerId}");
                    var strikerViewWithEvents = instance.GetComponentInChildren<IStrikerViewWithEvents>(true);
                    if (strikerViewWithEvents != null) {
                        Debug.Log($"[BattleInstaller] Constructing Striker {strikerId} via StrikerHub");
                        var strikerModelGetter = strikerViewWithEvents.Construct(playerId, rule, rythmTrackModel, playerRegistry, battleModel);
                        if (strikerModelGetter is IStrikerModel strikerModel) {
                            this.strikerModels.Add(strikerModel);
                            this.strikerViews.Add(strikerViewWithEvents);
                            strikerViewMap[playerId] = strikerViewWithEvents;
                            strikerViewWithEvents.SavePosition();
                            Debug.Log($"[BattleInstaller] Added StrikerModel (via Hub) for player {playerId}");
                        }
                        else {
                            Debug.LogError($"[BattleInstaller] Constructed model does not implement IStrikerModel for {strikerId}");
                        }
                    }
                    else {
                        Debug.LogError($"[BattleInstaller] Neither StrikerInstaller nor IStrikerViewWithEvents found on prefab for {strikerId}");
                    }
                }
            }

            gamePadSubscriptions = new CompositeDisposable();
            gamePadSubscriptions.Add(gamePadInputModel.SubscribeInputed(OnGamePadInputed));
            gamePadSubscriptions.Add(gamePadInputModel.SubscribeDirectionChanged(OnDirectionChanged));

            // No Presenter
            // life.Link(mutator.OnEnable, mutator.OnDisable);

            // Start Battle via Model
            battleModel.StartBattle();
        }

        void Update() {
            if (battleModel != null) {
                battleModel.OnUpdate(Time.deltaTime);
            }
        }

        public void ResetBattle() {
            if (rythmTrackModel is IRythmTrackModel rythmTrackModelMutable) {
                rythmTrackModelMutable.Reset();
            }

            foreach (var strikerModelGetter in strikerModels) {
                if (strikerModelGetter is IStrikerModel strikerModel) {
                    strikerModel.Reset();
                }
            }

            foreach (var strikerView in strikerViews) {
                if (strikerView == null) continue;
                strikerView.ResetPosition();
                strikerView.OnReset();
            }

            // Restart Battle Flow if needed?
            // Reset is strictly for resetting entities.
            // Flow transition happens in Model via ChangeState.
        }

        void OnDestroy() {
            gamePadSubscriptions?.Dispose();
        }

        private void OnGamePadInputed(GamePadInput input) {
            var playerId = playerRegistry.ToPlayerId(input.gamePadId);
            if (playerId == null) return;
            // Need to route to StrikerModel HandleInput.
            // Find strikerModel for playerId.
            var model = strikerModels.FirstOrDefault(m => m.PlayerId == playerId) as IStrikerModel;
            if (model != null) {
                model.HandleInput(input);
            }
        }

        private void OnDirectionChanged(DirectionChange change) {
            var playerId = playerRegistry.ToPlayerId(change.gamePadId);
            if (playerId == null) return;
            var model = strikerModels.FirstOrDefault(m => m.PlayerId == playerId) as IStrikerModel;
            if (model != null) {
                model.HandleDirection(change.direction);
            }
        }

        public void SyncTime(float time) {
            if (rythmTrackModel is IRythmTrackModel rythmTrackModelMutable) {
                rythmTrackModelMutable.SetTime(time);
            }
        }
    }
}
