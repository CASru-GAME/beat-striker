using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class App : MonoBehaviour {
    [NonSerialized] public List<Player> players = new();
    public static App Instance { get; private set; }
    [SerializeField] Canvas targetCanvas;
    [NonSerialized] public bool cursorMode;
    public event Action<Player> OnPlayerJoin, OnEscape;
    public CPUPlayer cpuPrefab;

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
        Debug.Log($"Player Joined: {(playerInput.devices.Count == 0 ? "" : playerInput.devices[0].name)}");
        playerInput.transform.SetParent(targetCanvas.transform, false);

        if (playerInput.TryGetComponent<RectTransform>(out var rt)) {
            rt.anchoredPosition = Vector2.zero;
        }

        if (playerInput.TryGetComponent<Player>(out var p)) {
            p.playerNumber = new(Instance.players.Count);
            players.Add(p);
            OnPlayerJoin?.Invoke(p);
        }
    }

    internal void Escape(HumanPlayer p) {
        OnEscape?.Invoke(p);
    }
}

public enum StrikerType {
    Hero, Wizard, Warrior, Satan
}