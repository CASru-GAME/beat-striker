using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class App : MonoBehaviour {
    [NonSerialized] public List<HumanPlayer> players = new();
    public static App Instance { get; private set; }
    [SerializeField] Canvas targetCanvas;
    [NonSerialized] public bool cursorMode;

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        cursorMode = true;
    }

    void OnEnable() {
        if (PlayerInputManager.instance == null) return;
        PlayerInputManager.instance.onPlayerJoined += OnPlayerJoined;
    }

    void OnDisable() {
        if (PlayerInputManager.instance == null) return;
        PlayerInputManager.instance.onPlayerJoined -= OnPlayerJoined;
    }

    void OnPlayerJoined(PlayerInput playerInput) {
        playerInput.transform.SetParent(targetCanvas.transform, false);

        if (playerInput.TryGetComponent<RectTransform>(out var rt)) {
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
