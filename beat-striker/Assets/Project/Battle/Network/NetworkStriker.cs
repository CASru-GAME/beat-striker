using Fusion;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Alice {
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(StrikerHub))]
    public class NetworkStriker : NetworkBehaviour {
        [Networked] public int PlayerId { get; private set; }
        [Networked] public Striker StrikerId { get; private set; }
        [Networked] public float HitPoint { get; private set; }
        [Networked] public float SpecialPoint { get; private set; }
        [Networked] public int StateIndex { get; private set; }
        [Networked] public int FacingSign { get; private set; }

        StrikerHub strikerHub;
        IStrikerHub runtimeHub;
        [Inject] IStrikerRegistry strikerRegistry;
        [Inject] IBattleRuleSetting battleRuleSetting;

        public bool ShouldSimulate => Object.HasStateAuthority || Object.HasInputAuthority;
        public bool UsesNetworkLoop => Object != null && Runner != null && Runner.IsRunning;

        void Awake() {
            strikerHub = GetComponent<StrikerHub>();
            runtimeHub = strikerHub.EnsureAliceRuntimeHub();
        }

        public override void Spawned() {
            var resolver = AppScope.Instance.Container;
            resolver.InjectGameObject(gameObject);

            if (runtimeHub is AliceStrikerHub aliceRuntime) {
                aliceRuntime.InitializeRuntimeDependencies(strikerRegistry);
            }

            runtimeHub.SetPlayerId(PlayerId);
            strikerRegistry.RequestRegister(PlayerId, runtimeHub);
        }

        public override void Despawned(NetworkRunner runner, bool hasState) {
            strikerRegistry.RequestUnregister(PlayerId);
        }

        public void InitializeNetworkState(int playerId, Striker strikerId) {
            if (!Object.HasStateAuthority) {
                return;
            }

            PlayerId = playerId;
            StrikerId = strikerId;
            runtimeHub.SetPlayerId(playerId);
            HitPoint = runtimeHub.HitPoint.CurrentValue;
            SpecialPoint = runtimeHub.SpecialPoint.CurrentValue;
            StateIndex = runtimeHub.GetCurrentStateIndex();
            var lookDirection = runtimeHub.LookDirection.CurrentValue;
            FacingSign = lookDirection.x >= 0f ? 1 : -1;
        }

        public override void FixedUpdateNetwork() {
            if (!ShouldSimulate) {
                ApplyNetworkState();
                return;
            }

            if (Object.HasStateAuthority) {
                if (GetInput<BattleNetworkInput>(out var input) && input.HasCommand != 0) {
                    ApplyBeatCommand(input);
                }
            } else if (Object.HasInputAuthority) {
                if (GetInput<BattleNetworkInput>(out var input) && input.HasCommand != 0) {
                    ApplyBeatCommand(input);
                }
            }

            var delta = Runner.DeltaTime;
            runtimeHub.Tick(delta);
            runtimeHub.TickPhysics(delta);

            if (Object.HasStateAuthority) {
                HitPoint = runtimeHub.HitPoint.CurrentValue;
                SpecialPoint = runtimeHub.SpecialPoint.CurrentValue;
                StateIndex = runtimeHub.GetCurrentStateIndex();
                var lookDirection = runtimeHub.LookDirection.CurrentValue;
                FacingSign = lookDirection.x >= 0f ? 1 : -1;
            }

            if (Object.HasInputAuthority && !Object.HasStateAuthority) {
                ApplyNetworkState();
            }
        }

        void ApplyNetworkState() {
            var state = new StrikerAuthoritativeState(
                transform.position,
                FacingSign,
                HitPoint,
                SpecialPoint,
                StateIndex);
            runtimeHub.ApplyAuthoritativeState(state, -1f);
        }

        void ApplyBeatCommand(BattleNetworkInput input) {
            if (input.Direction.sqrMagnitude > 0.0001f) {
                runtimeHub.ChangeDirection(input.Direction);
            } else {
                runtimeHub.CancelDirection();
            }

            if (input.ComboCount > 0) {
                var specialPointGain = CalculateSpecialPointGain(input.ComboCount);
                runtimeHub.AddSpecialPoint(specialPointGain);
            }

            switch (input.Button) {
                case GamePadButton.Start:
                    runtimeHub.Special();
                    break;
                case GamePadButton.East:
                    runtimeHub.Attack();
                    break;
                case GamePadButton.South:
                    runtimeHub.Charge();
                    break;
                case GamePadButton.West:
                    runtimeHub.Dash();
                    break;
                case GamePadButton.Left:
                case GamePadButton.Right:
                case GamePadButton.North:
                    runtimeHub.Guard();
                    break;
            }
        }

        float CalculateSpecialPointGain(int comboCount) {
            var combo = Mathf.Max(1, comboCount);
            var combo1Gain = Mathf.Max(0f, battleRuleSetting.Combo1SpecialPointGain.CurrentValue);
            var convergenceRate = Mathf.Max(0f, battleRuleSetting.SpecialPointGainConvergenceRate.CurrentValue);
            var convergenceValue = Mathf.Max(combo1Gain, battleRuleSetting.SpecialPointGainConvergenceValue.CurrentValue);
            var x = combo - 1;
            return convergenceValue - (convergenceValue - combo1Gain) * Mathf.Exp(-convergenceRate * x);
        }
    }
}
