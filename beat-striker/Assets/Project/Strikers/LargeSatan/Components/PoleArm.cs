using UnityEngine;

[RequireComponent(typeof(Tracker))]
public class PoleArm : MonoBehaviour {
    Tracker tracker;
    [SerializeField] Transform hand;
    IState currentState;

    public void Awake() {
        TryGetComponent(out tracker);
        if (tracker == null) {
            Debug.LogError("Tracker component is required for PoleArm.");
        }
    }

    public void Start() {
        ChangeState(new DefaultState(this));
    }

    private void ChangeState(IState newState) {
        currentState?.OnExit();
        currentState = newState;
        currentState?.OnEnter();
    }

    class DefaultState : IState {
        readonly PoleArm poleArm;
        Tracker.TargetHandle currentTargetHandle;

        public DefaultState(PoleArm poleArm) {
            this.poleArm = poleArm;
        }

        public void OnEnter() {
            currentTargetHandle = poleArm.tracker.AddTarget(poleArm.hand);
        }

        public void OnExit() {
            poleArm.tracker.RemoveTarget(currentTargetHandle);
        }

        public void Request(RequestCode request) {
        }
    }

    class AimState : IState {
        readonly PoleArm poleArm;
        Tracker.TargetHandle currentTargetHandle;

        public AimState(PoleArm poleArm) {
            this.poleArm = poleArm;
        }

        public void OnEnter() {
            currentTargetHandle = poleArm.tracker.AddTarget(poleArm.hand, followPosition: true, followRotation: false);
        }

        public void OnExit() {
            poleArm.tracker.RemoveTarget(currentTargetHandle);
        }

        public void Request(RequestCode request) {
        }
    }

    class EmittionState : IState {
        readonly PoleArm poleArm;
        Quaternion EmitRotation;
        Tracker.TargetHandle currentTargetHandle;

        public EmittionState(PoleArm poleArm, Quaternion finalRotation) {
            this.poleArm = poleArm;
            this.EmitRotation = finalRotation;
        }

        public void OnEnter() {
            currentTargetHandle = poleArm.tracker.AddTarget();
        }

        public void OnExit() {
            poleArm.tracker.RemoveTarget(currentTargetHandle);
        }

        public void Request(RequestCode request) {
        }
    }

    private interface IState {
        void OnEnter();
        void OnExit();
        void Request(RequestCode request);
    }

    private enum RequestCode {
        Emit,
    }
}
