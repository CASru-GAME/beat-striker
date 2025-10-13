using System;
using System.Collections.Generic;
using UnityEngine;

public enum Btn {
    North,
    West,
    South,
    East,
    Direction,
    RightShoulder,
    LeftShoulder,
    RightTrigger,
    LeftTrigger,
}

public abstract class Player : MonoBehaviour {
    public PlayerId playerNumber = new(-1);

    public struct ActionResult {
        public bool success;
        public Vector2 direction;
        public ActionResult(bool success, Vector2 direction) {
            this.success = success;
            this.direction = direction;
        }
        public static implicit operator bool(ActionResult result) => result.success;
    }

    public StrikerType striker = StrikerType.Hero;

    protected readonly Dictionary<Btn, bool> isDown = new();
    protected readonly Dictionary<Btn, bool> wasDown = new();
    protected Vector2 direction;
    protected const float REPEAT_INTERVAL = 0.3f;
    protected readonly Dictionary<Btn, float> lastRepeatTime = new();

    protected virtual void Awake() {
        foreach (Btn b in Enum.GetValues(typeof(Btn))) {
            isDown[b] = false;
            wasDown[b] = false;
        }
    }

    protected virtual void Start() {
    }

    protected virtual void LateUpdate() {
        foreach (Btn b in Enum.GetValues(typeof(Btn))) {
            wasDown[b] = isDown[b];
        }
    }

    public virtual ActionResult GetBtnDown(Btn btn) {
        if (isDown.TryGetValue(btn, out bool pressed) && pressed &&
            wasDown.TryGetValue(btn, out bool wasPreviouslyPressed) && !wasPreviouslyPressed) {
            return new ActionResult(true, direction);
        }
        return new ActionResult(false, direction);
    }

    public virtual ActionResult GetBtnUp(Btn btn) {
        if (isDown.TryGetValue(btn, out bool pressed) && !pressed &&
            wasDown.TryGetValue(btn, out bool wasPreviouslyPressed) && wasPreviouslyPressed) {
            return new ActionResult(true, direction);
        }
        return new ActionResult(false, direction);
    }

    public virtual ActionResult GetBtn(Btn btn) {
        if (!isDown.TryGetValue(btn, out bool pressed) || !pressed)
            return new ActionResult(false, direction);
        return new ActionResult(true, direction);
    }

    public virtual ActionResult GetBtnRepeat(Btn btn) {
        float now = Time.unscaledTime;

        if (!isDown.TryGetValue(btn, out bool pressed) || !pressed)
            return new ActionResult(false, direction);

        lastRepeatTime.TryGetValue(btn, out float latestRepeat);

        if (now - latestRepeat >= REPEAT_INTERVAL) {
            lastRepeatTime[btn] = now;
            return new ActionResult(true, direction);
        }

        return new ActionResult(false, direction);
    }

    protected void HandleButton(Btn btn, bool down) {
        isDown[btn] = down;

        if (down) {
            lastRepeatTime[btn] = float.NegativeInfinity;
        }
    }
}

[System.Serializable]
public struct PlayerId {
    public int value;

    public PlayerId(int value) {
        this.value = value;
    }

    public static implicit operator int(PlayerId id) => id.value;
}
