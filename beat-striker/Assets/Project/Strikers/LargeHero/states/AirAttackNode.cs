using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {

    public class AirAttackNode : StrikerNode {
        [SerializeField] StrikerState airAttackState;
        [SerializeField] EnergyStorage energyStorage;
        [SerializeField] StrikerState beamState;

        // このノードに遷移した時に呼ばれる
        public override void OnTryTransition(IStrikerNodeContext context) {
            var energy = energyStorage.RetrieveEnergy();
            if (energy == 0) {
                context.TryTransition(airAttackState);
            } else {
                context.TryTransition(beamState);
            }
        }
    }
}
