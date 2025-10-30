using Core.App.Models;
using Core.App.Types;
using Core.Utils;
using UnityEngine;

namespace Core.Battle {
    [RequireComponent(typeof(StrikerView))]
    [RequireComponent(typeof(Life))]
    public class StrikerInstaller: MonoBehaviour {
        [SerializeField] private HitPoint maxHitPoint = new(100);
        [SerializeField] private SpecialPoint maxSpecialPoint = new(100);

        public (IStrikerModel, IRythmTrackModel) Construct(PlayerId playerId, ScoreRule rule, IRythmTrackModel rythmTrackModel, IPlayerRegistry playerRegistry) {
            Debug.Log("StrikerInstaller Construct:" + playerId);
            var view = GetComponent<StrikerView>();
            var life = GetComponent<Life>();
            var strikerModel = new StrikerModel(playerId, maxHitPoint, maxSpecialPoint, rule);
            var presenter = new StrikerPresenter(strikerModel, view, this.GetBus(), life, playerRegistry, rythmTrackModel);
            return (strikerModel, rythmTrackModel);
        }


    }
}