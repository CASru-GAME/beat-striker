using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {


    public class ChargeState : StrikerState {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Charge;

        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] EnergyStorage energyStorage;
        [SerializeField] ParticleSystem particleprefab;
        [SerializeField] AudioClip audioClip;

        void OnEnable() {
            particleprefab.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleprefab.Clear();
        }

        void OnDisable() {
            particleprefab.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleprefab.Clear();
        }

        public override void OnEnter(IStrikerContext context) {
            particleprefab. gameObject.SetActive(true);
            particleprefab.Clear();
            particleprefab.Play();
            AudioSource.PlayClipAtPoint(audioClip, context.Rigidbody.position);
            context.PlayAnimation(animationClip, context => {
                energyStorage.StoreEnergy(1);
                context.TryTransition(nextNode);
            });
        }

        public override void OnUpdate(IStrikerStateContext context) {
        }

        public override void OnExit(IStrikerContext context) {
            particleprefab.gameObject.SetActive(false);
            particleprefab.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleprefab.Clear();
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


