

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.App;
using Core.App.Installers;
using Core.App.Types;
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
    public class BattleInstaller : MonoBehaviour, IBattleResetter {
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
        private BattleFlowPresenter mutator;
        [SerializeField] private StrikerId defaultStrikerId;

        void Awake() {
            var app = GameObject.Find("App").GetComponent<AppFlowScope>();
            var settingModel = app.battleSettingModel;
            var playerRegistry = app.playerRegistry;
            var trackId = settingModel.Track;

            var view = GetComponent<BattleView>();
            var audioSource = GetComponent<AudioSource>();
            var beatAudioSource = gameObject.AddComponent<AudioSource>();
            
            var trackAudio = trackAudios.FirstOrDefault(t => t.trackId.Equals(trackId));
            
            var rule = new ScoreRule(excellentScore, goodScore, specialGain);
            var life = GetComponent<Life>();
            var bus = this.GetBus();
            var battleModel = new BattleModel(playerCount);
            this.battleModel = battleModel;
            var rythmTrackModel = new RythmTrackModel(Enumerable.Range(1, 1000).Select(x => x * 60f / bpm).ToArray(),
                perfectWindow,
                goodWindow,
                timeOffset
            );
            this.rythmTrackModel = rythmTrackModel;
            this.mutator = new BattleFlowPresenter(bus, life, battleModel, rythmTrackModel, this, view, trackId);
            view.Construct(audioSource, beatAudioSource, trackAudio.audioClip, beatClip);
            view.SetRythmTrackModel(rythmTrackModel);

            for (int i = 0; i < playerTransforms.Length; i++) {
                var transform = playerTransforms[i];
                var playerId = new PlayerId(i);
                var strikerId = settingModel.GetStriker(playerId);
                if(strikerId.HasValue == false) {
                    strikerId = defaultStrikerId;
                }
                // BattleModelにもStrikerIdを設定
                battleModel.SetStriker(playerId, strikerId);
                
                var strikerPrefab = strikerPrefabs.FirstOrDefault(x => x.strikerId == strikerId).prefab;
                var instance = Instantiate(strikerPrefab);
                instance.transform.SetPositionAndRotation(transform.position, transform.rotation);
                transform.SetParent(instance.transform);
                
                // StrikerInstallerがある場合はそれ経由で構築、それ以外はIStrikerView経由
                var strikerInstaller = instance.GetComponent<StrikerInstaller>();
                if (strikerInstaller != null) {
                    // StrikerInstaller経由で構築
                    var (strikerModel, _, strikerView) = strikerInstaller.Construct(playerId, rule, rythmTrackModel, playerRegistry);
                    this.strikerModels.Add(strikerModel);
                    this.strikerViews.Add(strikerView);
                    strikerView.SavePosition();
                } else {
                    // StrikerHub等：自己完結型のConstruct
                    var strikerView = instance.GetComponent<IStrikerView>();
                    if (strikerView != null) {
                        var strikerModel = strikerView.Construct(playerId, rule, rythmTrackModel, playerRegistry);
                        this.strikerModels.Add(strikerModel);
                        this.strikerViews.Add(strikerView);
                        strikerView.SavePosition();
                    } else {
                        Debug.LogError($"Prefab {strikerPrefab.name} does not implement IStrikerView");
                    }
                }
            }

            if(DebugMode) {
                mutator.DebugMode();
            }

        }

        void Update() {
            if (mutator != null) {
                mutator.OnUpdate(Time.deltaTime);
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
                strikerView.ResetPosition();
                strikerView.OnReset();
            }
        }

        public void SyncTime(float time) {
            if (rythmTrackModel is IRythmTrackModel rythmTrackModelMutable) {
                rythmTrackModelMutable.SetTime(time);
            }
        }
    }
}