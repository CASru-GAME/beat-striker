using Core.App.Models;
using Core.App.Types;
using Core.Utils;
using UnityEngine;

namespace Core.Battle {
    [RequireComponent(typeof(StrikerView))]
    [RequireComponent(typeof(Life))]
    public class StrikerInstaller : MonoBehaviour {
        [SerializeField] private HitPoint maxHitPoint = new(100);
        [SerializeField] private SpecialPoint maxSpecialPoint = new(100);

        public (IStrikerModel, IRythmTrackModel, IStrikerView) Construct(PlayerId playerId, ScoreRule rule, IRythmTrackModel rythmTrackModel, IPlayerRegistry playerRegistry) {
            Debug.Log("StrikerInstaller Construct:" + playerId);
            var view = GetComponent<StrikerView>();
            var life = GetComponent<Life>();
            if (view == null) throw new MissingComponentException($"StrikerView is required on '{name}' ({gameObject.GetType().Name})");
            if (life == null) throw new MissingComponentException($"Life is required on '{name}' ({gameObject.GetType().Name})");
            var strikerModel = new StrikerModel(playerId, maxHitPoint, maxSpecialPoint, rule);
            var presenter = new StrikerPresenter(strikerModel, view, this.GetBus(), life, playerRegistry, rythmTrackModel);
            view.Construct(presenter);
            return (strikerModel, rythmTrackModel, view);
        }



    }
}