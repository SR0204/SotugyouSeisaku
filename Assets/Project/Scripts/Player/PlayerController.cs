using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerController : MonoBehaviour
{
    [Header("移動・ダッシュ設定")]
    public float walkSpeed = 6f;
    public float sprintSpeed = 10f;
    public float rotationSpeed = 10f;

    [Header("HP設定")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("攻撃設定")]
    public float attackDamage = 25f;
    public float attackStaminaCost = 20f;
    public float attackRange = 2.0f;
    [SerializeField] private float attackHitDelay = 0.25f;  // モーション開始から判定までの遅延
    [SerializeField] private float attackDuration = 0.8f;  // モーション1回あたりの長さ
    public bool isAttacking = false;                        // 攻撃中フラグ

    // ★コンボ制御用フィールド
    [Header("コンボ設定")]
    [SerializeField] private int maxComboStep = 3;        // 最大コンボ数（通常3段攻撃）
    private int comboStep = 0;                             // 現在のコンボ段階
    private bool canQueueNextCombo = false;               // 先行入力（連打）の受付ウィンドウ
    private bool isNextComboQueued = false;               // 次のコンボが予約されているか

    [Header("攻撃範囲の可視化設定")]
    [SerializeField] private GameObject attackZonePrefab;
    [SerializeField] private float zoneDisplayTime = 0.2f;

    [Header("回避（ローリング）設定")]
    public float rollSpeed = 12f;
    public float rollDuration = 0.5f;
    public float invincibleDuration = 0.3f;
    public float rollStaminaCost = 20f;
    public bool isRolling = false;
    public bool isInvincible = false;
    private Vector3 rollDirection;

    [Header("エスト瓶（回復）設定")]
    public int maxEstusCount = 3;
    public int currentEstusCount;
    public float healAmount = 40f;
    public TextMeshProUGUI estusText;

    [Header("スタミナ設定")]
    public float maxStamina = 100f;
    public float currentStamina;
    public float staminaDrainRate = 25f;
    public float staminaRegenRate = 15f;
    public float regenDelay = 1.0f;

    private float regenTimer = 0f;
    private bool isSprinting = false;

    [Header("ジャンプ・重力設定")]
    public float jumpHeight = 1.5f;
    public float jumpStaminaCost = 20f;
    public float gravity = -19.62f;

    [Header("UI設定")]
    public Slider healthSlider;
    public Slider staminaSlider;

    [Header("参照")]
    public Transform cameraTransform;

    private CharacterController controller;
    private Vector3 velocity;
    private Animator animator;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        currentHealth = maxHealth;
        UpdateHealthUI();

        currentEstusCount = maxEstusCount;
        UpdateEstusUI();

        currentStamina = maxStamina;
        if (staminaSlider != null)
        {
            staminaSlider.maxValue = maxStamina;
            staminaSlider.value = currentStamina;
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    void Update()
    {
        bool isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // ローリング中の移動処理
        if (isRolling)
        {
            controller.Move(rollDirection * rollSpeed * Time.deltaTime);
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
            return;
        }

        // 攻撃中の重力移動処理
        if (isAttacking)
        {
            if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
            }

            // ★攻撃中にクリックされた場合、先行入力ウィンドウ内なら予約フラグを立てる
            bool clickingUI = UnityEngine.EventSystems.EventSystem.current != null &&
                              UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            if (!clickingUI && Input.GetMouseButtonDown(0) && canQueueNextCombo)
            {
                if (currentStamina >= attackStaminaCost)
                {
                    isNextComboQueued = true;
                }
            }

            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
            return;
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 dir = new Vector3(h, 0, v).normalized;

        // 攻撃（マウス左クリック）発動判定
        bool isClickingUI = UnityEngine.EventSystems.EventSystem.current != null &&
                            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        if (!isClickingUI && Input.GetMouseButtonDown(0) && currentStamina >= attackStaminaCost && !isAttacking)
        {
            StartCoroutine(AttackRoutine());
        }

        // 回避（Left Controlキー）
        if (Input.GetKeyDown(KeyCode.LeftControl) && currentStamina >= rollStaminaCost && !isAttacking)
        {
            StartCoroutine(RollRoutine(dir));
        }

        // エスト使用（Eキー / Rキー）
        if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.E))
        {
            UseEstus();
        }

        // ダッシュ（Left Shift）
        bool wantsToSprint = Input.GetKey(KeyCode.LeftShift);
        if (wantsToSprint && dir.magnitude >= 0.1f && currentStamina > 0f)
        {
            isSprinting = true;
            currentStamina -= staminaDrainRate * Time.deltaTime;
            currentStamina = Mathf.Max(currentStamina, 0f);
            regenTimer = regenDelay;
        }
        else
        {
            isSprinting = false;
        }

        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        // 移動と回転
        if (dir.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            controller.Move(moveDir.normalized * currentSpeed * Time.deltaTime);

            Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // アニメーションのSpeed値を更新
        if (animator != null)
        {
            if (dir.magnitude >= 0.1f)
            {
                float animSpeed = isSprinting ? 1.0f : 0.5f;
                animator.SetFloat("Speed", animSpeed);
            }
            else
            {
                animator.SetFloat("Speed", 0f);
            }
        }

        // ジャンプ（Spaceキー）
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && currentStamina >= jumpStaminaCost)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            currentStamina -= jumpStaminaCost;
            regenTimer = regenDelay;
        }

        // スタミナ自然回復
        if (!isSprinting && !isRolling && !isAttacking)
        {
            if (regenTimer > 0f)
            {
                regenTimer -= Time.deltaTime;
            }
            else if (currentStamina < maxStamina)
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
                currentStamina = Mathf.Min(currentStamina, maxStamina);
            }
        }

        if (staminaSlider != null)
        {
            staminaSlider.value = currentStamina;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private IEnumerator RollRoutine(Vector3 inputDir)
    {
        isRolling = true;
        isInvincible = true;

        currentStamina -= rollStaminaCost;
        regenTimer = regenDelay;

        if (inputDir.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
            rollDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        }
        else
        {
            rollDirection = transform.forward;
        }

        transform.rotation = Quaternion.LookRotation(rollDirection);

        float timer = 0f;
        while (timer < rollDuration)
        {
            timer += Time.deltaTime;

            if (timer >= invincibleDuration)
            {
                isInvincible = false;
            }

            yield return null;
        }

        isRolling = false;
        isInvincible = false;
    }

    // ★変更: コンボ対応の攻撃ルーチン
    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        comboStep = 1;

        while (comboStep <= maxComboStep)
        {
            // スタミナ消費と自然回復タイマー更新
            currentStamina -= attackStaminaCost;
            currentStamina = Mathf.Max(currentStamina, 0f);
            regenTimer = regenDelay;

            canQueueNextCombo = false;
            isNextComboQueued = false;

            // アニメーション再生（ComboStep: 1, 2, 3 ...）
            if (animator != null)
            {
                animator.SetInteger("ComboStep", comboStep);
                animator.SetTrigger("Attack");
            }

            // 1. 攻撃判定発生までの遅延
            yield return new WaitForSeconds(attackHitDelay);

            // 2. 攻撃判定生成 ＆ ダメージ処理
            ExecuteAttackHit();

            // 3. モーションの後半（連打受付時間帯）へ入る
            canQueueNextCombo = true;

            float remainingTime = attackDuration - attackHitDelay;
            if (remainingTime > 0f)
            {
                yield return new WaitForSeconds(remainingTime);
            }

            // 受付終了
            canQueueNextCombo = false;

            // 連打（予約）されていて、かつ最大段数未満なら次の段階へ継続
            if (isNextComboQueued && comboStep < maxComboStep)
            {
                comboStep++;
            }
            else
            {
                break; // 単発または連打がなければルーチン終了
            }
        }

        // コンボ終了処理
        comboStep = 0;
        canQueueNextCombo = false;
        isNextComboQueued = false;

        if (animator != null)
        {
            animator.SetInteger("ComboStep", 0);
        }

        isAttacking = false;
    }

    // 攻撃判定・処理をまとめたヘルパーメソッド
    private void ExecuteAttackHit()
    {
        Vector3 attackCenter = transform.position + transform.forward * (attackRange * 0.5f);

        if (attackZonePrefab != null)
        {
            Vector3 zonePos = attackCenter;
            zonePos.y = 0.05f;

            GameObject zone = Instantiate(attackZonePrefab, zonePos, Quaternion.Euler(90f, transform.eulerAngles.y, 0f));
            zone.transform.localScale = new Vector3(attackRange, attackRange, 1f);
            Destroy(zone, zoneDisplayTime);
        }

        Collider[] hitColliders = Physics.OverlapSphere(attackCenter, attackRange * 0.5f);
        foreach (var hitCollider in hitColliders)
        {
            BossHealth bossHealth = hitCollider.GetComponent<BossHealth>();
            if (bossHealth != null)
            {
                bossHealth.TakeDamage(attackDamage);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 attackCenter = transform.position + transform.forward * (attackRange * 0.5f);
        Gizmos.DrawWireSphere(attackCenter, attackRange * 0.5f);
    }

    private void UseEstus()
    {
        if (currentEstusCount > 0 && currentHealth < maxHealth)
        {
            currentEstusCount--;
            currentHealth += healAmount;
            currentHealth = Mathf.Min(currentHealth, maxHealth);

            UpdateHealthUI();
            UpdateEstusUI();
        }
    }

    public void TakeDamage(float damage)
    {
        if (isInvincible)
        {
            return;
        }

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0f);
        UpdateHealthUI();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void UpdateHealthUI()
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
    }

    private void UpdateEstusUI()
    {
        if (estusText != null)
        {
            estusText.text = "Potion: " + currentEstusCount;
        }
    }

    private void Die()
    {
        this.enabled = false;

        StageManager stageManager = FindObjectOfType<StageManager>();
        if (stageManager != null)
        {
            stageManager.OnPlayerDied();
        }
    }
}