using UnityEngine;

[System.Diagnostics.DebuggerDisplay("{" + nameof(DebuggerDisplay ) + "(),nq}")]
public class WizardController : MonoBehaviour

{
    // === 1. 公開変数 (Inspectorで設定) ===
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float dashSpeed = 8f;
    public float jumpForce = 10f;
    
    [Header("Ground Check")]
    public Transform groundCheck;
    public LayerMask groundLayer;

    // === 2. プライベート変数 (コンポーネント) ===
    private Rigidbody2D rb;
    private Animator charAnimator; // ★修正点: 変数名を統一

    // === 3. 内部状態変数 ===
    private float horizontalInput;
    private bool isGrounded;
    private bool jumpRequested = false; 

    // =======================================================
    // 初期設定 (Awake)
    // =======================================================

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        charAnimator = GetComponent<Animator>(); // ★修正点: 取得する変数名を統一

        // コンポーネントの存在チェック
        if (rb == null) Debug.LogError("Rigidbody2Dコンポーネントが見つかりません！");
        if (charAnimator == null) 
        {
            Debug.LogError("Animatorコンポーネントが見つかりません！スクリプトを無効化します。");
            enabled = false;
            return;
        }
    }

    // =======================================================
    // 入力とアニメーション制御 (Update)
    // =======================================================

    void Update()
    {
        // === 1. 入力値の取得 ===
        horizontalInput = Input.GetAxisRaw("Horizontal");
        
        // === 2. アニメーターの更新 (すべて charAnimator に修正) ===
        charAnimator.SetBool("IsGrounded", isGrounded);
        charAnimator.SetFloat("Speed", Mathf.Abs(horizontalInput)); 

        // 進行方向への反転
        if (horizontalInput != 0)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Sign(horizontalInput); 
            transform.localScale = scale;
        }

        // === 3. ジャンプ処理の入力検知 ===
        if (isGrounded && Input.GetKeyDown(KeyCode.Space)) 
        {
            jumpRequested = true;
        }

        // === 4. 攻撃処理 ===
        HandleAttackInput();
    }

    // =======================================================
    // 物理演算の処理 (FixedUpdate)
    // =======================================================

    void FixedUpdate()
    {
        // === 1. 地面判定の更新 ===
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, 0.2f, groundLayer);

        // === 2. 左右移動の制御 ===
        float currentSpeed = walkSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            currentSpeed = dashSpeed;
        }

        float targetSpeedX = horizontalInput * currentSpeed;
        rb.linearVelocity = new Vector2(targetSpeedX, rb.linearVelocity.y);

        // === 3. ジャンプの実行 ===
        if (jumpRequested)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpRequested = false;
        }
    }
    
    // =======================================================
    // 攻撃入力の処理
    // =======================================================

    private void HandleAttackInput()
    {
        if (isGrounded)
        {
            // --- 地上攻撃 (すべて charAnimator に修正) ---
            if (Input.GetKeyDown(KeyCode.Z))      // 例: 打撃
            {
                charAnimator.SetInteger("AttackType", 1); 
                charAnimator.SetTrigger("AttackTrigger");
            }
            else if (Input.GetKeyDown(KeyCode.X)) // 例: 魔法攻撃
            {
                charAnimator.SetInteger("AttackType", 2);
                charAnimator.SetTrigger("AttackTrigger");
            }
            else if (Input.GetKeyDown(KeyCode.F)) // 例: 溜め攻撃
            {
                charAnimator.SetInteger("AttackType", 3);
                charAnimator.SetTrigger("AttackTrigger");
            }
            else if (Input.GetKeyDown(KeyCode.R)) // 例: 必殺技
            {
                charAnimator.SetInteger("AttackType", 4);
                charAnimator.SetTrigger("AttackTrigger");
            }
        }
        else // 空中
        {
            // --- 空中攻撃 (すべて charAnimator に修正) ---
            if (Input.GetMouseButtonDown(0)) {
                int airAttack = 0;

                if (horizontalInput != 0) airAttack = 1; // 空中横攻撃
                else if (Input.GetKey(KeyCode.S)) airAttack = 2; // 空中下攻撃

                if (airAttack > 0) {
                    charAnimator.SetInteger("AirAttackType", airAttack);
                    charAnimator.SetTrigger("AttackTrigger");
                }
            }
        }
    }

private string DebuggerDisplay => ToString();
        }
    
