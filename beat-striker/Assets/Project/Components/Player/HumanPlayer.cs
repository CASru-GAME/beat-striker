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
    [SerializeField] private float cursorSpeed = 5f;
    private PlayerInput playerInput;
    private GameInput input;

    private bool directionDown = false;
    private const float DIR_ON_THRESHOLD = 0.2f;
    private const float DIR_OFF_THRESHOLD = 0.15f;

    protected override void Awake() {
        base.Awake();

        playerInput = GetComponent<PlayerInput>();
        rectTransform = GetComponent<RectTransform>();
        DontDestroyOnLoad(gameObject);
        input = new GameInput();
    }

    protected override void Start() {
        base.Start();

        playerNumber = Common.Instance.players.Count;
        Common.Instance.players.Add(this);

        text.text = playerNumber + 1 + "P";
        text.color = playerColor[playerNumber];
    }

    void Update() {
        if (cursor.activeSelf != Common.Instance.cursorMode)
            cursor.SetActive(Common.Instance.cursorMode);
        if (!Common.Instance.cursorMode) return;

        if (GetBtnDown(Btn.East)) {
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

        var res = GetBtn(Btn.Direction);
        if (res) {
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
}
