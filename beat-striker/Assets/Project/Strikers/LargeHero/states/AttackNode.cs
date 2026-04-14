using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {

    public class AttackNode : StrikerNode {
        [SerializeField] StrikerState attackState;
        [SerializeField] EnergyStorage energyStorage;
        [SerializeField] StrikerState beamState;
        [SerializeField] StrikerState rushState;
        [SerializeField] StrikerState specialLaunchState;
        [Header("Debug/Test")]
        [SerializeField] bool useChargeForSpecialInTest;
        [SerializeField] int requiredChargeForSpecial = 2;

        public override void OnTryTransition(IStrikerNodeContext context) {
            if (context.LocalInputDirection.x != 0) {
                context.TryTransition(rushState);
                return;
            }

            var energy = energyStorage.RetrieveEnergy();
            if (useChargeForSpecialInTest && energy >= requiredChargeForSpecial) {
                context.TryTransition(specialLaunchState);
                return;
            }

            var self = context.GetSelf();
            var isSpecialGaugeFull = self.SpecialPoint.CurrentValue >= self.MaxSpecialPoint.CurrentValue;
            if (isSpecialGaugeFull) {
                context.TryTransition(specialLaunchState);
                return;
            }

            if (energy == 0) {
                context.TryTransition(attackState);
            } else if (energy == 1) {
                context.TryTransition(beamState);
            } else {
                context.TryTransition(attackState);
            }
        }
        

    }
}
