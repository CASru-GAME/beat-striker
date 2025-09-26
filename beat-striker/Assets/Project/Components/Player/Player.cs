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
    LeftTrigger
}

public abstract class Player : MonoBehaviour {
    public struct ActionResult {
        public bool success;
        public Vector2 direction;
        public ActionResult(bool success, Vector2 direction) {
            this.success = success;
            this.direction = direction;
        }
        public static implicit operator bool(ActionResult result) => result.success;
    }

    public Striker strikerPrefab;

    protected struct InputEvent {
        public Btn btn;
        public bool isDown;
        public float time;
        public Vector2 dirSnapshot;
        public bool consumed;
        public bool pending;
        public float confirmAt;
    }

    protected const int BUFFER_CAPACITY = 64;
    protected readonly List<InputEvent> buffer = new(BUFFER_CAPACITY);
    protected readonly Dictionary<Btn, bool> isDown = new();
    protected const float DEBOUNCETIME = 0.2f;
    protected const float START_GRACE = 0.1f;
    protected Vector2 direction;
    protected const float REPEAT_INTERVAL = 0.3f;
    protected readonly Dictionary<Btn, float> lastRepeatTime = new();

    protected virtual void Awake() {
        foreach (Btn b in Enum.GetValues(typeof(Btn)))
            isDown[b] = false;
    }

    protected virtual void Start() {
    }

    public virtual ActionResult GetBtnDown(params Btn[] btns)
        => TryMatch(wantDown: true, btns);

    public virtual ActionResult GetBtnUp(params Btn[] btns)
        => TryMatch(wantDown: false, btns);

    public virtual ActionResult GetBtn(Btn btn) {
        if (!isDown.TryGetValue(btn, out bool pressed) || !pressed)
            return new ActionResult(false, Vector2.zero);
        return new ActionResult(true, direction);
    }

    public virtual ActionResult GetBtnRepeat(Btn btn) {
        float now = Time.unscaledTime;

        if (!isDown.TryGetValue(btn, out bool pressed) || !pressed)
            return new ActionResult(false, Vector2.zero);

        lastRepeatTime.TryGetValue(btn, out float latestRepeat);

        if (now - latestRepeat >= REPEAT_INTERVAL) {
            lastRepeatTime[btn] = now;
            return new ActionResult(true, direction);
        }

        return new ActionResult(false, Vector2.zero);
    }

    protected static bool ShouldGrace(Btn b) => b != Btn.Direction;

    protected void HandleButton(Btn btn, bool down) {
        isDown[btn] = down;

        float now = Time.unscaledTime;

        if (down) lastRepeatTime[btn] = float.NegativeInfinity;

        var ev = new InputEvent {
            btn = btn,
            isDown = down,
            time = now,
            dirSnapshot = direction,
            consumed = false,
            pending = down && ShouldGrace(btn),
            confirmAt = down && ShouldGrace(btn) ? now + START_GRACE
                                                 : now
        };

        PushEvent(ev);
        PruneOld();
    }

    protected void PushEvent(in InputEvent ev) {
        if (buffer.Count >= BUFFER_CAPACITY)
            buffer.RemoveAt(0);
        buffer.Add(ev);
    }

    protected void PruneOld() {
        float now = Time.unscaledTime;
        float limit = now - (DEBOUNCETIME * 4f);
        int firstAlive = 0;
        for (; firstAlive < buffer.Count; firstAlive++)
            if (buffer[firstAlive].time >= limit) break;
        if (firstAlive > 0) buffer.RemoveRange(0, firstAlive);
    }

    protected ActionResult TryMatch(bool wantDown, params Btn[] btns) {
        float now = Time.unscaledTime;

        if (btns == null || btns.Length == 0) return new ActionResult(false, Vector2.zero);

        int[] pickedIdx = new int[btns.Length];
        for (int i = 0; i < pickedIdx.Length; i++) pickedIdx[i] = -1;

        for (int i = buffer.Count - 1; i >= 0; i--) {
            var ev = buffer[i];
            if (ev.consumed) continue;
            if (ev.isDown != wantDown) continue;

            if ((now - ev.time) > DEBOUNCETIME) break;

            for (int k = 0; k < btns.Length; k++) {
                if (pickedIdx[k] != -1) continue;
                if (ev.btn != btns[k]) continue;

                if (wantDown && btns.Length == 1 && ev.pending && ShouldGrace(ev.btn) && now < ev.confirmAt) {
                    continue;
                }

                pickedIdx[k] = i;
                break;
            }

            bool allPicked = true;
            for (int k = 0; k < pickedIdx.Length; k++)
                if (pickedIdx[k] == -1) { allPicked = false; break; }
            if (allPicked) break;
        }

        for (int k = 0; k < pickedIdx.Length; k++)
            if (pickedIdx[k] == -1) return new ActionResult(false, Vector2.zero);

        float latest = float.NegativeInfinity;
        float earliest = float.PositiveInfinity;
        for (int k = 0; k < pickedIdx.Length; k++) {
            float t = buffer[pickedIdx[k]].time;
            if (t > latest) latest = t;
            if (t < earliest) earliest = t;
        }
        if ((latest - earliest) > DEBOUNCETIME) return new ActionResult(false, Vector2.zero);

        int useIdx = pickedIdx[0];
        for (int k = 0; k < pickedIdx.Length; k++)
            if (buffer[pickedIdx[k]].btn == Btn.Direction) { useIdx = pickedIdx[k]; break; }
        for (int k = 0; k < pickedIdx.Length; k++)
            if (buffer[pickedIdx[k]].time > buffer[useIdx].time) useIdx = pickedIdx[k];
        Vector2 dir = buffer[useIdx].dirSnapshot;

        for (int k = 0; k < pickedIdx.Length; k++) {
            var e = buffer[pickedIdx[k]];
            e.consumed = true;
            e.pending = false;
            buffer[pickedIdx[k]] = e;
        }
        return new ActionResult(true, dir);
    }
}
