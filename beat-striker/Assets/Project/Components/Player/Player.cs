using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum Btn {
    North,
    West,
    South,
    East,
    Direction
}

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(RectTransform))]
public class Player : MonoBehaviour, GameInput.IPlayerActions {
    public struct ActionResult {
        public bool success;
        public Vector2 direction;
        public ActionResult(bool success, Vector2 direction) {
            this.success = success;
            this.direction = direction;
        }
        public static implicit operator bool(ActionResult result) => result.success;
    }
    RectTransform rectTransform;
    [SerializeField] float cursorSpeed = 5f;
    private PlayerInput playerInput;
    public Striker strikerPrefab;
    GameInput input;
    [SerializeField] TextMeshProUGUI text;
    int playerNumber = -1;
    [SerializeField] Color[] playerColor;

    private struct InputEvent {
        public Btn btn;
        public bool isDown;
        public float time;
        public Vector2 dirSnapshot;
        public bool consumed;
        public bool pending;
        public float confirmAt;
    }

    private const int BUFFER_CAPACITY = 64;
    private readonly List<InputEvent> buffer = new(BUFFER_CAPACITY);
    private readonly Dictionary<Btn, bool> isDown = new();
    private const float DEBOUNCETIME = 0.2f;
    private const float START_GRACE = 0.1f;
    private Vector2 direction;
    private bool directionDown = false;
    private const float DIR_ON_THRESHOLD = 0.2f;
    private const float DIR_OFF_THRESHOLD = 0.15f;
    private const float REPEAT_INTERVAL = 0.3f;
    private readonly Dictionary<Btn, float> lastRepeatTime = new();

    void Awake() {
        playerInput = GetComponent<PlayerInput>();
        rectTransform = GetComponent<RectTransform>();
        DontDestroyOnLoad(gameObject);
        input = new GameInput();

        foreach (Btn b in Enum.GetValues(typeof(Btn)))
            isDown[b] = false;
    }

    void Start() {
        if (Common.Instance.player0 == null) {
            Common.Instance.player0 = this;
            playerNumber = 0;
        }
        else if (Common.Instance.player1 == null) {
            Common.Instance.player1 = this;
            playerNumber = 1;
        }
        else {
            Destroy(gameObject);
            return;
        }

        text.text = playerNumber + 1 + "P";
        text.color = playerColor[playerNumber];
    }

    void Update() {
        if (GetActionDown(Btn.East)) {
            Vector2 pos = transform.position;
            PointerEventData pointerData = new(EventSystem.current) {
                position = pos
            };

            List<RaycastResult> results = new();
            EventSystem.current.RaycastAll(pointerData, results);

            foreach (var result in results) {
                if (result.gameObject.TryGetComponent<Button>(out var btn)) {
                    btn.onClick.Invoke();
                    break;
                }
            }
        }

        var res = GetAction(Btn.Direction);
        if (res) {
                        Debug.Log("aaa");
            rectTransform.anchoredPosition += cursorSpeed * Time.deltaTime * res.direction;
        }
    }

    void OnEnable() {
        input.asset.devices = playerInput.devices;
        playerInput.onControlsChanged += OnControlsChanged;

        input.Player.AddCallbacks(this);
        input.Player.Enable();
    }

    void OnDisable() {
        input.Player.RemoveCallbacks(this);
        input.Player.Disable();

        playerInput.onControlsChanged -= OnControlsChanged;
    }

    void OnDestroy() {
        input.Dispose();
    }

    private void OnControlsChanged(PlayerInput changed) {
        if (changed == playerInput)
            input.asset.devices = playerInput.devices;
    }

    public ActionResult GetActionDown(params Btn[] btns)
        => TryMatch(wantDown: true, btns);


    public ActionResult GetActionUp(params Btn[] btns)
        => TryMatch(wantDown: false, btns);


    public ActionResult GetAction(Btn btn) {
        if (!isDown.TryGetValue(btn, out bool pressed) || !pressed)
            return new ActionResult(false, Vector2.zero);
        return new ActionResult(true, direction);
    }

    public ActionResult GetActionRepeat(Btn btn) {
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

    public void OnDirection(InputAction.CallbackContext context) {
        direction = context.ReadValue<Vector2>();
        float mag = direction.magnitude;

        bool nextDown = directionDown ? (mag >= DIR_OFF_THRESHOLD)
                                      : (mag >= DIR_ON_THRESHOLD);

        if (nextDown != directionDown) {
            directionDown = nextDown;
            HandleButton(Btn.Direction, directionDown);
        }
    }

    public void OnNorth(InputAction.CallbackContext context) {
        if (context.started) HandleButton(Btn.North, true);
        else if (context.canceled) HandleButton(Btn.North, false);
    }
    public void OnWest(InputAction.CallbackContext context) {
        if (context.started) HandleButton(Btn.West, true);
        else if (context.canceled) HandleButton(Btn.West, false);
    }
    public void OnSouth(InputAction.CallbackContext context) {
        if (context.started) HandleButton(Btn.South, true);
        else if (context.canceled) HandleButton(Btn.South, false);
    }
    public void OnEast(InputAction.CallbackContext context) {
        if (context.started) HandleButton(Btn.East, true);
        else if (context.canceled) HandleButton(Btn.East, false);
    }

    private static bool ShouldGrace(Btn b) => b != Btn.Direction;

    private void HandleButton(Btn btn, bool down) {
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

    private void PushEvent(in InputEvent ev) {
        if (buffer.Count >= BUFFER_CAPACITY)
            buffer.RemoveAt(0);
        buffer.Add(ev);
    }

    private void PruneOld() {
        float now = Time.unscaledTime;
        float limit = now - (DEBOUNCETIME * 4f);
        int firstAlive = 0;
        for (; firstAlive < buffer.Count; firstAlive++)
            if (buffer[firstAlive].time >= limit) break;
        if (firstAlive > 0) buffer.RemoveRange(0, firstAlive);
    }

    private ActionResult TryMatch(bool wantDown, params Btn[] btns) {
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
