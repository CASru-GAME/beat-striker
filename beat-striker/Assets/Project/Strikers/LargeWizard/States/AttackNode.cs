using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {

    public class AttackNode : StrikerNode {
        [SerializeField] StrikerState attackState;
        [SerializeField] StrikerState attack1State;
        [SerializeField] StrikerState attack2State;
        [SerializeField] StrikerState attack3State;
        [SerializeField] EnergyStorage energyStorage;

        // このノードに遷移した時に呼ばれる
        public override void OnTryTransition(IStrikerNodeContext context) {
            var energy = energyStorage.RetrieveEnergy();
            if (energy == 0) {
                context.TryTransition(attackState);
            }
            else if (energy == 1) {
                context.TryTransition(attack1State);
            }

            else if (energy == 2) {
                context.TryTransition(attack2State);
            }
            else if (energy >= 3) {
                context.TryTransition(attack3State);
            }


        }
    }
}
