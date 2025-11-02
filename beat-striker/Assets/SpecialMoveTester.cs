using UnityEngine;

// Small helper to test the special move from Inspector or other scripts.
public class SpecialMoveTester : MonoBehaviour
{
    public Animator animator;
    public GameObject slashPrefab;
    public int count = 12;
    public float spreadAngle = 60f;
    public float speed = 12f;
    public int damage = 8;
    public GameObject hitEffectPrefab;

    [Header("Input")]
    // Only respond to input when this flag is true. Set true on the player-controlled character.
    public bool listenForInput = false;
    public KeyCode triggerKey = KeyCode.I;

    // Context menu allows you to right-click the component title and run this in Editor (when in Play mode too).
    [ContextMenu("Test Special")]
    public void TestSpecial()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            Debug.LogWarning("SpecialMoveTester: Animator not assigned and not found on same GameObject.");
            return;
        }

        HeroSpecialSMB.SpawnSpecial(animator, slashPrefab, count, spreadAngle, speed, damage, hitEffectPrefab);
    }

    // キー入力でSpecialを発動
    void Update()
    {
        if (!listenForInput) return; // ignore input on non-player instances
        
        // プレイヤー判定（P1のみ入力を受け付ける）
        if (!gameObject.name.Contains("P1"))
        {
            return;
        }
        
        // check configured trigger key (supports legacy Input and new Input System)
        if (IsTriggerKeyPressed())
        {
            Debug.Log($"{triggerKey} pressed - triggering Special animation on {gameObject.name} (Player character)");
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            var view = GetComponent<Core.Battle.StrikerView>();
            if (view != null)
            {
                view.Special();  // アニメーターのSpecialトリガーを発火
                Debug.Log("Called StrikerView.Special()");
            }
            else
            {
                Debug.LogWarning("StrikerView component not found");
            }
        }
    }

    // Try both legacy Input and new Input System (via reflection) so this works in either project setup.
    private bool IsTriggerKeyPressed()
    {
        // try legacy Input
        try
        {
            if (UnityEngine.Input.GetKeyDown(triggerKey)) return true;
        }
        catch (System.InvalidOperationException)
        {
            // legacy Input not available (new Input System active)
        }

        // fallback: try to use InputSystem.Keyboard.current.<key>.wasPressedThisFrame via reflection
        var kbType = System.Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem") ?? System.Type.GetType("UnityEngine.InputSystem.Keyboard");
        if (kbType != null)
        {
            var currentProp = kbType.GetProperty("current", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            var current = currentProp?.GetValue(null);
            if (current != null)
            {
                string propName = triggerKey == KeyCode.I ? "iKey" : (triggerKey == KeyCode.Space ? "spaceKey" : null);
                if (!string.IsNullOrEmpty(propName))
                {
                    var keyProp = kbType.GetProperty(propName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    var keyObj = keyProp?.GetValue(current);
                    if (keyObj != null)
                    {
                        var wasPressed = keyObj.GetType().GetProperty("wasPressedThisFrame", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)?.GetValue(keyObj);
                        if (wasPressed is bool b && b) return true;
                    }
                }
            }
        }

        return false;
    }

    // Public method you can call from other scripts or UI to test at runtime
    public void TestSpecialPublic()
    {
        TestSpecial();
    }
}
