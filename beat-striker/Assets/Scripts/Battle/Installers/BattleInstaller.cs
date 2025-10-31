

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
        public StrikerInstaller prefab;
    }

    [RequireComponent(typeof(Life))]
    [RequireComponent(typeof(BattleView))]
    public class BattleInstaller : MonoBehaviour, IBattleResetter {
        [SerializeField] float perfectWindow = 0.1f;
        [SerializeField] float goodWindow = 0.2f;
        [SerializeField] float timeOffset = 0f;
        [SerializeField] Transform[] playerTransforms;
        [SerializeField] StrikerPrefab[] strikerPrefabs;
        [SerializeField] int excellentScore = 1000;
        [SerializeField] int goodScore = 500;
        [SerializeField] int specialGain = 10;
        [SerializeField] int playerCount = 2;
        public List<IStrikerModelGetter> strikerModels = new();
        public List<IStrikerView> strikerViews = new();
        public IRythmTrackModelGetter rythmTrackModel;
        [SerializeField] bool DebugMode = false;

        void Awake() {
            var app = GameObject.Find("App").GetComponent<AppFlowScope>();
            var settingModel = app.battleSettingModel;
            var playerRegistry = app.playerRegistry;


            var view = GetComponent<BattleView>();
            var rule = new ScoreRule(excellentScore, goodScore, specialGain);
            var life = GetComponent<Life>();
            var bus = this.GetBus();
            var battleModel = new BattleModel(playerCount);
            var rythmTrackModel = new RythmTrackModel(Enumerable.Range(1, 100).Select(x => (float)x).ToArray(),
                perfectWindow,
                goodWindow,
                timeOffset
            );
            this.rythmTrackModel = rythmTrackModel;
            var mutator = new BattleFlowPresenter(bus, life, battleModel, rythmTrackModel,this);
            view.Construct(mutator);

            for (int i = 0; i < playerTransforms.Length; i++) {
                var transform = playerTransforms[i];
                var playerId = new PlayerId(i);
                var strikerId = settingModel.GetStriker(playerId);
                var strikerPrefab = strikerPrefabs.FirstOrDefault(x => x.strikerId == strikerId).prefab;
                var instance = Instantiate(strikerPrefab);
                instance.transform.SetPositionAndRotation(transform.position, transform.rotation);
                transform.SetParent(instance.transform);
                var (IStrikerModel, IRythmTrackModel, IStrikerView) = instance.Construct(playerId, rule, rythmTrackModel, playerRegistry);
                this.strikerModels.Add(IStrikerModel);
                this.strikerViews.Add(IStrikerView);
                IStrikerView.SavePosition();
            }

            if(DebugMode) {
                mutator.DebugMode();
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
            }
        }
    }
}