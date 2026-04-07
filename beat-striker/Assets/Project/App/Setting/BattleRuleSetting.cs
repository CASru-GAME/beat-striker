using R3;
using UnityEngine;

namespace Alice {
    public interface IBattleRuleSetting {
        ReadOnlyReactiveProperty<int> RoundsToWin { get; }
        ReadOnlyReactiveProperty<float> Combo1SpecialPointGain { get; }
        ReadOnlyReactiveProperty<float> SpecialPointGainConvergenceRate { get; }
        ReadOnlyReactiveProperty<float> SpecialPointGainConvergenceValue { get; }
    }

    public class BattleRuleSetting : MonoBehaviour, IBattleRuleSetting {
        [SerializeField] int roundsToWin = 2;
        [SerializeField] float combo1SpecialPointGain = 4f;
        [SerializeField] float specialPointGainConvergenceRate = 0.2f;
        [SerializeField] float specialPointGainConvergenceValue = 10f;

        readonly ReactiveProperty<int> roundsToWinProperty = new();
        readonly ReactiveProperty<float> combo1SpecialPointGainProperty = new();
        readonly ReactiveProperty<float> specialPointGainConvergenceRateProperty = new();
        readonly ReactiveProperty<float> specialPointGainConvergenceValueProperty = new();

        public ReadOnlyReactiveProperty<int> RoundsToWin => roundsToWinProperty;
        public ReadOnlyReactiveProperty<float> Combo1SpecialPointGain => combo1SpecialPointGainProperty;
        public ReadOnlyReactiveProperty<float> SpecialPointGainConvergenceRate => specialPointGainConvergenceRateProperty;
        public ReadOnlyReactiveProperty<float> SpecialPointGainConvergenceValue => specialPointGainConvergenceValueProperty;

        void Awake() {
            roundsToWinProperty.OnNext(roundsToWin);
            combo1SpecialPointGainProperty.OnNext(combo1SpecialPointGain);
            specialPointGainConvergenceRateProperty.OnNext(specialPointGainConvergenceRate);
            specialPointGainConvergenceValueProperty.OnNext(specialPointGainConvergenceValue);
        }
    }
}
