using Core;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Alice {
    public class RankingPresenterView : MonoBehaviour {
        readonly Subject<Unit> backToMenuRequested = new();
        readonly ReactiveProperty<bool> battleHistoryScrollUpHovered = new(false);
        readonly ReactiveProperty<bool> battleHistoryScrollDownHovered = new(false);

        [SerializeField] ActionEmitter backToMenuEmitter;
        [SerializeField] Botan battleHistoryScrollUpHoverBotan;
        [SerializeField] Botan battleHistoryScrollDownHoverBotan;
        [SerializeField] ScrollRect battleHistoryScrollRect;
        [SerializeField, Tooltip("上/下 Botan ホバー中のスクロール速度（normalized / 秒）。")]
        float battleHistoryScrollNormalizedSpeed = 1.5f;

        public Observable<Unit> BackToMenuRequested => backToMenuRequested;

        void Update() {
            TickBattleHistoryListScroll();
        }

        void TickBattleHistoryListScroll() {
            var scroll = battleHistoryScrollRect;
            if (!scroll || !scroll.vertical || !scroll.isActiveAndEnabled) {
                return;
            }

            var up = battleHistoryScrollUpHovered.CurrentValue;
            var down = battleHistoryScrollDownHovered.CurrentValue;
            if (!up && !down) {
                return;
            }

            var speed = battleHistoryScrollNormalizedSpeed;
            var deltaNorm = 0f;
            if (up) {
                deltaNorm += speed * Time.deltaTime;
            }

            if (down) {
                deltaNorm -= speed * Time.deltaTime;
            }

            if (Mathf.Approximately(deltaNorm, 0f)) {
                return;
            }

            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + deltaNorm);
        }

        void Awake() {
            backToMenuEmitter.OnClickEvent.Subscribe(_ => backToMenuRequested.OnNext(Unit.Default)).AddTo(this);

            battleHistoryScrollUpHoverBotan.OnHoverEvent.Subscribe(_ => battleHistoryScrollUpHovered.Value = true).AddTo(this);
            battleHistoryScrollUpHoverBotan.OnHoverExitEvent.Subscribe(_ => battleHistoryScrollUpHovered.Value = false).AddTo(this);

            battleHistoryScrollDownHoverBotan.OnHoverEvent.Subscribe(_ => battleHistoryScrollDownHovered.Value = true).AddTo(this);
            battleHistoryScrollDownHoverBotan.OnHoverExitEvent.Subscribe(_ => battleHistoryScrollDownHovered.Value = false).AddTo(this);
        }

        void OnDisable() {
            battleHistoryScrollUpHovered.Value = false;
            battleHistoryScrollDownHovered.Value = false;
        }
    }
}
