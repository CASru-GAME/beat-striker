using UnityEngine;

namespace Core.Striker.Components {

    [AddComponentMenu(" StrikerComponents/Dash State Waypoint", 0)]
    public class DashNode : StrikerNode {
        [SerializeField] private StrikerState fallbackState;
        [SerializeField] private StrikerState[] dashStates;
        [SerializeField] private StrikerCharger charger;

        public override void OnTryTransition(IStrikerNodeContext hub) {

        }
    }
}