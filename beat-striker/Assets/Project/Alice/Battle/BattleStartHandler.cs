using UnityEngine;
using VContainer.Unity;

namespace Alice {
    public sealed class BattleStartHandler : IStartable, ITickable {
        readonly IBattleFlow battleFlow;
        float elapsed;
        bool started;

        public BattleStartHandler(IBattleFlow battleFlow) {
            this.battleFlow = battleFlow;
        }

        public void Start() {
            elapsed = 0f;
            started = false;
        }

        public void Tick() {
            if (started) return;

            elapsed += Time.deltaTime;
            if (elapsed < 0.5f) return;

            started = true;
            battleFlow.StartBattle();
        }
    }
}
