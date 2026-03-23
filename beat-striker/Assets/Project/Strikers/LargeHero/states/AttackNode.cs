using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {

    public class AttackNode : StrikerNode {
        [SerializeField] StrikerState attackState;
        [SerializeField] EnergyStorage energyStorage;
        [SerializeField] StrikerState beamState;
        [SerializeField] StrikerState rushState;

        // このノードに遷移した時に呼ばれる
        public override void OnTryTransition(IStrikerNodeContext context) {
            // 横入力 > 0 なら突進
            if (context.InputDirection.x != 0) {
                context.TryTransition(rushState);
                return;
            }

            var energy = energyStorage.RetrieveEnergy();
            Debug.Log($"AttackNode: energy={energy} time={Time.time}");
            if (energy == 0) {
                // チャージ0 → 斬撃
                context.TryTransition(attackState);
            } else {
                // チャージ1 → ビーム
                context.TryTransition(beamState);
            }
        }
        

    }
}
