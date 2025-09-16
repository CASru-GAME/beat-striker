using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Common : MonoBehaviour
{
    [NonSerialized] public List<HumanPlayer> players = new();
    public static Common Instance { get; private set; }
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

    void OnEnable()
    {
        PlayerInputManager.instance.onPlayerJoined += OnPlayerJoined;
    }

    void OnDisable()
    {
        PlayerInputManager.instance.onPlayerJoined -= OnPlayerJoined;
    }

    void OnPlayerJoined(PlayerInput playerInput)
    {
        playerInput.transform.SetParent(targetCanvas.transform, false);

        if (playerInput.TryGetComponent<RectTransform>(out var rt))
        {
            rt.anchoredPosition = Vector2.zero;
        }
    }
}
