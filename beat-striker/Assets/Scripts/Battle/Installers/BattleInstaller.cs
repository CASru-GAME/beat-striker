

using System;
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
    public class BattleInstaller : MonoBehaviour {
        [SerializeField] float perfectWindow = 0.1f;
        [SerializeField] float goodWindow = 0.2f;
        [SerializeField] float timeOffset = 0f;
        [SerializeField] Transform[] transforms;
        [SerializeField] StrikerPrefab[] strikerPrefabs;
        [SerializeField] int excellentScore = 1000;
        [SerializeField] int goodScore = 500;

        void Awake() {
            var app = GameObject.Find("App").GetComponent<AppFlowScope>();
            var playerRegistry = app.playerRegistry;
            var settingModel = app.battleSettingModel;


            var view = GetComponent<BattleView>();
            var rule = new ScoreRule(excellentScore, goodScore);
            var life = GetComponent<Life>();
            var bus = this.GetBus();
            var battleModel = new BattleModel(playerRegistry.GetAllPlayerIds());
            var rythmTrackModel = new RythmTrackModel(new float[] { },
                perfectWindow,
                goodWindow,
                timeOffset
            );
            var mutator = new BattleFlowPresenter(bus, life, battleModel, rythmTrackModel);
            view.Construct(mutator);

            var playerIds = playerRegistry.GetAllPlayerIds().ToList();
            for (int i = 0; i < transforms.Length; i++) {
                var transform = transforms[i];
                var playerId = playerIds[i];
                var strikerId = settingModel.GetStriker(playerId);
                var strikerPrefab = strikerPrefabs.FirstOrDefault(x => x.strikerId == strikerId).prefab;
                var instance = Instantiate(strikerPrefab, transform);
                instance.Construct(playerId, rule, playerRegistry, rythmTrackModel);
            }
        }
    }
}