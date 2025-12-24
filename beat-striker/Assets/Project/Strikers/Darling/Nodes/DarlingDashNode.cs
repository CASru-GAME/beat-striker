using UnityEngine;
using Core.Striker.Darling.Components;

namespace Core.Striker.Darling.Nodes {

    [AddComponentMenu(" StrikerComponents/Dash State Waypoint", 0)]
    public class DarlingDashNode : StrikerNode {
        [SerializeField] private StrikerState fallbackState;
        [SerializeField] private StrikerState[] dashStates;
        [SerializeField] private DarlingCharger charger;

        public override void OnTryTransition(IStrikerNodeContext hub) {

        }
    }
}