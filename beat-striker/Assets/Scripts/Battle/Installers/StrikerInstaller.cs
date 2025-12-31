using Core.App.Interfaces;
using Core.App.Types;
using UnityEngine;

namespace Core.Battle {
    [RequireComponent(typeof(StrikerView))]
    public class StrikerInstaller : MonoBehaviour {
        [SerializeField] private HitPoint maxHitPoint = new(100);
        [SerializeField] private SpecialPoint maxSpecialPoint = new(100);


        public (IStrikerModel, IRythmTrackModel, IStrikerView) Construct(
            PlayerId playerId,
            ScoreRule rule,
            IRythmTrackModel rythmTrackModel,
            IPlayerRegistry playerRegistry,
            IBattleModel battleModel) {

            Debug.Log("StrikerInstaller Construct:" + playerId);
            var view = GetComponent<StrikerView>();
            var strikerModel = new StrikerModel(playerId, maxHitPoint, maxSpecialPoint, rule, rythmTrackModel);

            // Pass model and events directly to view - no presenter needed
            view.Construct(strikerModel, rythmTrackModel, playerRegistry, battleModel);

            return (strikerModel, rythmTrackModel, view);
        }
    }
}
