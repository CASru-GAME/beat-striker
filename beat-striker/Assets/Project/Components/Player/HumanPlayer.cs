using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(RectTransform))]
public class HumanPlayer : Player, GameInput.IPlayerActions {
    public int playerNumber = -1;
    [SerializeField] private TextMeshProUGUI text;
    [SerializeField] private GameObject cursor;
    [SerializeField] protected Color[] playerColor;
    private RectTransform rectTransform;
    [SerializeField] private float cursorSpeed = 5000f;
    [SerializeField] private float cursorSpeedDegree = 0.3f;
    private float cursorTime = 0f;
    private PlayerInput playerInput;
    private GameInput input;

    private bool directionDown = false;
    private const float DIR_ON_THRESHOLD = 0.2f;
    private const float DIR_OFF_THRESHOLD = 0.15f;
    private GameObject lastHoveredObject;

    protected override void Awake() {
        base.Awake();

        playerInput = GetComponent<PlayerInput>();
        rectTransform = GetComponent<RectTransform>();
        DontDestroyOnLoad(gameObject);
        input = new GameInput();
    }

    protected override void Start() {
        base.Start();

        playerNumber = App.Instance.players.Count;
        App.Instance.players.Add(this);

        text.text = playerNumber + 1 + "P";
        text.color = playerColor[playerNumber];
    }

    void Update() {
        if (cursor.activeSelf != App.Instance.cursorMode)
            cursor.SetActive(App.Instance.cursorMode);
        if (!App.Instance.cursorMode) return;

        PointerEventData data = new(EventSystem.current) {
            position = transform.position,
            pointerId = playerNumber
        };

        List<RaycastResult> hoverResults = new();
        EventSystem.current.RaycastAll(data, hoverResults);
        GameObject currentHovered = FindBotan(hoverResults);

        if (currentHovered != lastHoveredObject) {
            if (lastHoveredObject)
                ExecuteEvents.Execute(lastHoveredObject, data, ExecuteEvents.pointerExitHandler);
            if (currentHovered)
                ExecuteEvents.Execute(currentHovered, data, ExecuteEvents.pointerEnterHandler);
            lastHoveredObject = currentHovered;
        }

        if (currentHovered && GetBtnDown(Btn.East)) {
            ExecuteEvents.Execute(currentHovered, data, ExecuteEvents.pointerClickHandler);
        }

        var res = GetBtn(Btn.Direction);
        if (res) {
            cursorTime += Time.deltaTime;
            rectTransform.anchoredPosition += cursorSpeed * (1 - Mathf.Exp(-cursorSpeedDegree * cursorTime)) * Time.deltaTime * res.direction;
        }
        else cursorTime = 0f;
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

    public void OnRightShoulder(InputAction.CallbackContext context) {
        if (context.started) HandleButton(Btn.RightShoulder, true);
        else if (context.canceled) HandleButton(Btn.RightShoulder, false);
    }

    public void OnLeftShoulder(InputAction.CallbackContext context) {
        if (context.started) HandleButton(Btn.LeftShoulder, true);
        else if (context.canceled) HandleButton(Btn.LeftShoulder, false);
    }

    public void OnRightTrigger(InputAction.CallbackContext context) {
        if (context.started) HandleButton(Btn.RightTrigger, true);
        else if (context.canceled) HandleButton(Btn.RightTrigger, false);
    }

    public void OnLeftTrigger(InputAction.CallbackContext context) {
        if (context.started) HandleButton(Btn.LeftTrigger, true);
        else if (context.canceled) HandleButton(Btn.LeftTrigger, false);
    }

    private GameObject FindBotan(List<RaycastResult> results) {
        foreach (var result in results) {
            if (result.gameObject.GetComponent<Botan>() || result.gameObject.GetComponent<Button>()) {
                return result.gameObject;
            }
        }
        return null;
    }
}
