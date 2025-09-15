using UnityEngine;

public class Striker : MonoBehaviour
{
    public float hp = 10;
    [SerializeField]
    private Vector2 inputVector;
    public Vector2 InputVector { get => inputVector; }

    private bool inputA;
    public bool InputA { get => inputA; }

    private bool inputB;
    public bool InputB { get => inputB; }

    private bool inputY;
    public bool InputY { get => inputY; }

    private bool inputX;
    public bool InputX { get => inputX; }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        // Update movement vector from Unity input axes (works with keyboard, joystick, etc.)
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        inputVector = new Vector2(h, v);

        // Update action buttons from keyboard keys A/B/C/D (held state)
        inputA = Input.GetKey(KeyCode.L);
        inputB = Input.GetKey(KeyCode.K);
        inputY = Input.GetKey(KeyCode.J);
        inputX = Input.GetKey(KeyCode.I);
    }
}
