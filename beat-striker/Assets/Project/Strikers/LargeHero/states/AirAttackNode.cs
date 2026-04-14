using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {

    public class AirAttackNode : StrikerNode {
        [SerializeField] StrikerState airAttackState;
        [SerializeField] EnergyStorage energyStorage;
        [SerializeField] StrikerState beamState;
        [SerializeField] StrikerState rushState;

        public override void OnTryTransition(IStrikerNodeContext context) {
            if (context.LocalInputDirection.x != 0) {
                context.TryTransition(rushState);
                return;
            }

            var energy = energyStorage.RetrieveEnergy();
            if (energy == 0) {
                context.TryTransition(airAttackState);
            } else {
                context.TryTransition(beamState);
            }
        }
    }
}
