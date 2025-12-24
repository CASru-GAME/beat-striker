using UnityEngine;

namespace Core.Striker.Components {

    [AddComponentMenu(" StrikerComponents/Attack State Holder", 0)]
    public class AttackNode : StrikerNode {
        [SerializeField] private StrikerState fallbackState;
        [SerializeField] private StrikerState[] attackStates;
        [SerializeField] private StrikerCharger charger;

        public override void OnTryTransition(IStrikerNodeContext hub) {
            int chargeCount = charger.Count;
            if(chargeCount <= 0) {
                hub.ChangeState(fallbackState);
                return;
            }
            charger.ChargeEnd();
            int index = Mathf.Clamp(chargeCount - 1, 0, attackStates.Length - 1);
            hub.ChangeState(attackStates[index]);
        }
    }
}
