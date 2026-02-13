using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeSatan {

    public class AttackNode : StrikerNode {
        [SerializeField] StrikerState attackState;
        [SerializeField] StrikerState beamState;
        [SerializeField] EnergyStorage energyStorage;

        // このノードに遷移した時に呼ばれる
        public override void OnTryTransition(IStrikerNodeContext context) {
            var energy = energyStorage.RetrieveEnergy();
            if(energy == 0) {
                context.TryTransition(attackState);
            }
            else {
                context.TryTransition(beamState);
            }
        }
        

    }
}
