using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class Common : MonoBehaviour
{
    [NonSerialized] public Player player0, player1;
    public static Common Instance { get; private set; }
    [SerializeField] Canvas targetCanvas;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
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
