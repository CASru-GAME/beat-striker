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

        // このノードに遷移した時に呼ばれる
        public override void OnTryTransition(IStrikerNodeContext context) {
            // 横入力 > 0 なら突進
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
            if (isSpecialGaugeFull && context.ConsumeSpecialPoint(self.MaxSpecialPoint.CurrentValue)) {
                context.TryTransition(specialLaunchState);
                return;
            }

            if (energy == 0) {
                // チャージ0 → 斬撃
                context.TryTransition(attackState);
            } else if (energy == 1) {
                // チャージ1 → ビーム
                context.TryTransition(beamState);
            } else {
                // チャージ2以上 → 斬撃（必殺はゲージ満タン発動に変更）
                context.TryTransition(attackState);
            }
        }
        

    }
}
