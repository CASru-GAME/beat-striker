

using UnityEngine;

namespace Core.Battle {
    public class BattleView: MonoBehaviour {
        private IBattleStateMutator mutator;

        void Awake() {

        }

        public void Construct(IBattleStateMutator mutator) {
            this.mutator = mutator;
        }

        void Update() {
            mutator?.OnUpdate(Time.deltaTime);
        }

    }
}