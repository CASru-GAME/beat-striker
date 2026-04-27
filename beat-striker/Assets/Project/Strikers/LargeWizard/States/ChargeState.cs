using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeWizard {



    public class ChargeState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Charge;

        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] EnergyStorage energyStorage;
        [SerializeField] AudioClip audioClip;

        public override void OnEnter(IStrikerContext context) {

            context.PlayAnimation(animationClip, context => {
                energyStorage.StoreEnergy(1);
                context.TryTransition(nextNode);

                audioClip.PlayAtApp(context.Rigidbody.transform.position);
            });
        }

        public override void OnUpdate(IStrikerStateContext context) {
        }

        public override void OnExit(IStrikerContext context) {
        }

        public override void OnAttackRequested(IStrikerStateContext context) {
        }

        public override void OnChargeRequested(IStrikerStateContext context) {
        }

        public override void OnDashRequested(IStrikerStateContext context) {
        }

        public override void OnGuardRequested(IStrikerStateContext context) {
        }

        public override void OnHit(IStrikerStateContext context, HitStatus status) {
        }

        public override void OnMiss(IStrikerStateContext context) {
        }

    }
}


