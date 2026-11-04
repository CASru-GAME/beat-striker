using Core.Battle;
using UnityEngine;
using Core.Striker;

namespace Core.LargeHero {



    public class ChargeState : StrikerState, Alice.ITutorialActionProgress {
        public override Alice.StrikerStateCategory Category => Alice.StrikerStateCategory.Charge;
        public bool IsTutorialActionCommitted { get; private set; }

        [SerializeField] private StrikerAnimationClip animationClip;
        [SerializeField] StrikerNode nextNode;
        [SerializeField] EnergyStorage energyStorage;
        [SerializeField] ParticleSystem particleprefab;
        [SerializeField] AudioClip audioClip;
        // AnimationPlayer compares speed-scaled playable time with length / speed.
        public float ObservationDuration => Mathf.Max(animationClip.fadeTime,
            animationClip.clip.length / Mathf.Max(.0001f, animationClip.speed * animationClip.speed));

        void OnEnable() {
            particleprefab.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleprefab.Clear();
        }

        void OnDisable() {
            particleprefab.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleprefab.Clear();
        }

        public override void OnEnter(IStrikerContext context) {
            IsTutorialActionCommitted = false;
            particleprefab. gameObject.SetActive(true);
            particleprefab.Clear();
            particleprefab.Play();
            audioClip.PlayAtApp(context.Rigidbody.position);
            context.PlayAnimation(animationClip, context => {
                energyStorage.StoreEnergy(1);
                IsTutorialActionCommitted = true;
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


    }
}


