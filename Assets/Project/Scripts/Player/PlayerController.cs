using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerController : MonoBehaviour
{
    // ==========================================
    // 1. 移動・ダッシュ設定
    // ==========================================
    [Header("=== 1. 移動・ダッシュ設定 ===")]
    [Tooltip("通常移動（歩き）のスピード")]
    public float walkSpeed = 6f;
    [Tooltip("ダッシュ時のスピード")]
    public float sprintSpeed = 10f;
    [Tooltip("回転の補間スピード")]
    public float rotationSpeed = 10f;

    [Space(5)]
    [Tooltip("このY座標（高さ）以下に落ちたら即死（デフォルト: -15m）")]
    public float fallDeathY = -15f;

    // ==========================================
    // 2. HP・回復（エスト瓶）設定
    // ==========================================
    [Header("=== 2. HP・回復設定 ===")]
    public float maxHealth = 100f;
    [HideInInspector] public float currentHealth;

    [Space(5)]
    [Tooltip("HPポーションの所持数（デフォルト: 4個）")]
    public int hpEstusCount = 4;
    [Tooltip("1回あたりのHP回復量")]
    public float healAmount = 40f;

    [Space(5)]
    [Tooltip("FPポーションの所持数（デフォルト: 3個）")]
    public int fpEstusCount = 3;
    [Tooltip("1回あたりのFP回復量")]
    public float fpHealAmount = 40f;

    [HideInInspector] public int currentHpEstusCount;
    [HideInInspector] public int currentFpEstusCount;

    public int currentEstusCount => currentHpEstusCount;
    public int maxEstusCount => hpEstusCount;

    // ==========================================
    // 3. スタミナ設定
    // ==========================================
    [Header("=== 3. スタミナ設定 ===")]
    public float maxStamina = 100f;
    [HideInInspector] public float currentStamina;
    [Tooltip("ダッシュ中の1秒あたりの消費スタミナ")]
    public float staminaDrainRate = 25f;
    [Tooltip("自動回復時の1秒あたりの回復量")]
    public float staminaRegenRate = 15f;
    [Tooltip("スタミナ消費アクション後、回復が始まるまでの待ち時間（秒）")]
    public float regenDelay = 1.0f;

    // ==========================================
    // 3-2. 魔法（FP）設定
    // ==========================================
    [Header("=== 3-2. 魔法（FP）設定 ===")]
    public float maxFp = 100f;
    [HideInInspector] public float currentFp;
    [Tooltip("魔法1発あたりの消費FP")]
    public float spellFpCost = 25f;
    [Tooltip("魔法1発あたりの消費スタミナ")]
    public float spellStaminaCost = 15f;

    [Tooltip("発射する魔法弾プレハブ")]
    public GameObject spellPrefab;
    [Tooltip("魔法の発生位置（手や杖の先端）")]
    public Transform spellSpawnPoint;
    [Tooltip("魔法弾の飛行スピード")]
    public float spellSpeed = 15f;

    [Tooltip("FP表示用のUIスライダー")]
    public Slider fpSlider;

    private bool isCasting = false;
    private bool isDrinking = false;

    // ==========================================
    // 4. 攻撃・コンボ設定
    // ==========================================
    [System.Serializable]
    public class ComboSettings
    {
        [Tooltip("攻撃開始から当たり判定が発生するまでの前隙（秒）")]
        public float hitDelay = 0.1f;
        [Tooltip("全体モーション時間（秒）")]
        public float duration = 0.6f;
        [Tooltip("攻撃の中心となる前方オフセット")]
        public float attackOffset = 1.2f;
    }

    [Header("=== 4. 攻撃・コンボ設定 ===")]
    [Tooltip("1ヒットあたりの攻撃力")]
    public float attackDamage = 25f;
    [Tooltip("攻撃1回あたりの消費スタミナ")]
    public float attackStaminaCost = 20f;
    [Tooltip("攻撃判定の半径")]
    public float attackRadius = 1.5f;

    [Tooltip("コンボ毎の個別タイミング設定（1段目, 2段目, 3段目）")]
    [SerializeField] private ComboSettings[] comboList = new ComboSettings[3];

    [HideInInspector] public bool isAttacking = false;
    private int comboStep = 0;
    private bool canQueueNextCombo = false;
    private bool isNextComboQueued = false;

    // ==========================================
    // 5. 回避（ローリング）・ジャンプ・ダウン設定
    // ==========================================
    [Header("=== 5. 回避・ジャンプ・ダウン設定 ===")]
    [Tooltip("ローリング時の移動スピード")]
    public float rollSpeed = 10f;
    [Tooltip("ローリング全体の動作時間（秒）")]
    public float rollDuration = 0.6f;
    [Tooltip("無敵時間（秒）")]
    public float invincibleDuration = 0.3f;
    [Tooltip("ローリングの消費スタミナ")]
    public float rollStaminaCost = 20f;

    [Space(5)]
    [Tooltip("ジャンプの高さ")]
    public float jumpHeight = 1.5f;
    [Tooltip("ジャンプの消費スタミナ")]
    public float jumpStaminaCost = 20f;
    [Tooltip("重力の強さ")]
    public float gravity = -19.62f;

    [Space(5)]
    [Tooltip("吹っ飛びダウンが発生するダメージの閾値")]
    public float knockdownDamageThreshold = 30f;
    [Tooltip("ダウン開始から起き上がり完了までの完全不能時間（秒）")]
    public float knockdownDuration = 3.0f;

    [HideInInspector] public bool isRolling = false;
    [HideInInspector] public bool isInvincible = false;
    [HideInInspector] public bool isKnockedDown = false;
    private bool isDead = false;
    private Vector3 rollDirection;

    // ==========================================
    // 6. UI & コンポーネント参照
    // ==========================================
    [Header("=== 6. UI & 参照設定 ===")]
    public Slider healthSlider;
    public Slider staminaSlider;
    public TextMeshProUGUI estusText;
    [Tooltip("基準とするメインカメラのTransform")]
    public Transform cameraTransform;

    private CharacterController controller;
    private Vector3 velocity;
    private Animator animator;
    private float regenTimer = 0f;
    private bool isSprinting = false;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        if (healthSlider == null)
        {
            GameObject hpObj = GameObject.Find("HPBar");
            if (hpObj != null) healthSlider = hpObj.GetComponent<Slider>();
        }

        if (staminaSlider == null)
        {
            GameObject stObj = GameObject.Find("StaminaBar");
            if (stObj != null) staminaSlider = stObj.GetComponent<Slider>();
        }

        if (estusText == null)
        {
            GameObject estusObj = GameObject.Find("Potion");
            if (estusObj != null) estusText = estusObj.GetComponent<TMPro.TextMeshProUGUI>();
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        currentHealth = maxHealth;
        UpdateHealthUI();

        currentHpEstusCount = hpEstusCount;
        currentFpEstusCount = fpEstusCount;
        UpdateEstusUI();

        currentStamina = maxStamina;
        if (staminaSlider != null)
        {
            staminaSlider.maxValue = maxStamina;
            staminaSlider.value = currentStamina;
        }

        currentFp = maxFp;
        if (fpSlider == null)
        {
            GameObject fpObj = GameObject.Find("FPBar");
            if (fpObj != null) fpSlider = fpObj.GetComponent<Slider>();
        }
        if (fpSlider != null)
        {
            fpSlider.maxValue = maxFp;
            fpSlider.value = currentFp;
        }

        isDead = false;
    }

    void Update()
    {
        // 落下死チェック
        if (transform.position.y < fallDeathY)
        {
            TakeDamage(9999f);
        }

        // ★ ダウン中の完全入力・移動ブロック（倒れている～起き上がるまで一切のキー操作を無効化）
        if (isKnockedDown)
        {
            if (animator != null) animator.SetFloat("Speed", 0f);

            if (controller.isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }

            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);

            if (currentStamina < maxStamina)
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
                currentStamina = Mathf.Min(currentStamina, maxStamina);

                if (staminaSlider != null)
                {
                    staminaSlider.value = currentStamina;
                }
            }

            return; // ★一切のキー入力判定・移動処理を行わずここで完全遮断
        }

        bool isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // 1. 回避中ガード
        if (isRolling)
        {
            Vector3 rollMove = rollDirection * rollSpeed;
            velocity.y += gravity * Time.deltaTime;
            rollMove.y = velocity.y;

            controller.Move(rollMove * Time.deltaTime);
            return;
        }

        // 2. 魔法詠唱中ガード
        if (isCasting)
        {
            if (animator != null) animator.SetFloat("Speed", 0f);

            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
            return;
        }

        // 3. ポーション飲むモーション中ガード
        if (isDrinking)
        {
            if (animator != null) animator.SetFloat("Speed", 0f);

            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
            return;
        }

        // 4. 攻撃中の処理
        if (isAttacking)
        {
            if (animator != null) animator.SetFloat("Speed", 0f);

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

        // --- 5. 入力取得とアクション判定 ---
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 dir = new Vector3(h, 0, v).normalized;

        bool isClickingUI = UnityEngine.EventSystems.EventSystem.current != null &&
                            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        // 通常攻撃
        if (!isClickingUI && Input.GetMouseButtonDown(0) && currentStamina >= attackStaminaCost && !isAttacking && !isCasting && !isDrinking)
        {
            StartCoroutine(AttackRoutine());
        }

        // 魔法発射（マウス右クリック）
        if (!isClickingUI && Input.GetMouseButtonDown(1) && !isAttacking && !isCasting && !isRolling && !isDrinking)
        {
            if (currentFp >= spellFpCost && currentStamina >= spellStaminaCost)
            {
                StartCoroutine(CastSpellRoutine());
            }
        }

        // ジャンプ中の攻撃判定
        if (!isGrounded && !isClickingUI && !isAttacking && !isCasting && !isDrinking)
        {
            if (Input.GetMouseButtonDown(0) && currentStamina >= attackStaminaCost)
            {
                StartCoroutine(JumpAttackRoutine());
            }
            else if (Input.GetMouseButtonDown(1) && currentFp >= spellFpCost && currentStamina >= spellStaminaCost)
            {
                StartCoroutine(CastSpellRoutine());
            }
        }

        // 回避入力判定
        if (Input.GetKeyDown(KeyCode.LeftControl) && dir.magnitude >= 0.1f && currentStamina >= rollStaminaCost && !isAttacking && !isCasting && !isDrinking)
        {
            isRolling = true;
            StartCoroutine(RollRoutine(dir));
            return;
        }

        // HPポーション使用
        if ((Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.E)) && !isDrinking && !isRolling && !isAttacking && !isCasting)
        {
            StartCoroutine(UseHpEstusRoutine());
        }

        // FPポーション使用
        if ((Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Q)) && !isDrinking && !isRolling && !isAttacking && !isCasting)
        {
            StartCoroutine(UseFpEstusRoutine());
        }

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

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded && currentStamina >= jumpStaminaCost && !isDrinking)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            currentStamina -= jumpStaminaCost;
            regenTimer = regenDelay;
        }

        // --- 6. 移動・回転計算 ---
        Vector3 moveVelocity = Vector3.zero;
        float currentSpeed = isSprinting ? sprintSpeed : walkSpeed;

        if (dir.magnitude >= 0.1f && !isAttacking && !isCasting && !isDrinking)
        {
            float targetAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            moveVelocity = moveDir.normalized * currentSpeed;

            Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            if (animator != null)
            {
                float animSpeed = isSprinting ? 1.0f : 0.5f;
                animator.SetFloat("Speed", animSpeed);
            }
        }
        else
        {
            // ★ WASD入力をやめた時は確実に Speed を 0 にリセットして Idle へ戻す
            if (animator != null && !isCasting && !isDrinking && !isAttacking && !isKnockedDown)
            {
                animator.SetFloat("Speed", 0f);
            }
        }

        if (!isSprinting && !isAttacking && !isCasting && !isDrinking)
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

        // --- 7. 重力加算とMove実行 ---
        velocity.y += gravity * Time.deltaTime;

        Vector3 finalMove = moveVelocity;
        finalMove.y = velocity.y;

        controller.Move(finalMove * Time.deltaTime);
    }

    private IEnumerator RollRoutine(Vector3 inputDir)
    {
        currentStamina -= rollStaminaCost;
        regenTimer = regenDelay;

        // ★ 1. まず入力方向からカメラ基準の進行方向を計算
        if (inputDir.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + cameraTransform.eulerAngles.y;
            rollDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
        }
        else
        {
            rollDirection = transform.forward;
        }

        // ★ 2. アニメーションを再生する前に、プレイヤーの向きを入力方向へ即座に向ける
        if (rollDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(rollDirection);
        }

        // ★ 3. 向きが確定してからアニメーショントリガーを発火
        if (animator != null)
        {
            animator.SetTrigger("Roll");
        }

        isInvincible = true;

        float timer = 0f;

        while (timer < rollDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / rollDuration;

            if (timer >= invincibleDuration)
            {
                isInvincible = false;
            }

            float speedMultiplier = Mathf.Lerp(1.5f, 0.3f, progress);
            rollSpeed = sprintSpeed * speedMultiplier;

            yield return null;
        }

        isRolling = false;
        isInvincible = false;

        if (animator != null)
        {
            animator.ResetTrigger("Roll");
        }
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        comboStep = 1;

        while (comboStep <= comboList.Length)
        {
            currentStamina -= attackStaminaCost;
            currentStamina = Mathf.Max(currentStamina, 0f);
            regenTimer = regenDelay;

            canQueueNextCombo = false;
            isNextComboQueued = false;

            ComboSettings currentCombo = comboList[comboStep - 1];

            if (animator != null)
            {
                animator.SetInteger("ComboStep", comboStep);

                if (comboStep == 1)
                {
                    animator.SetTrigger("Attack");
                }
            }

            if (currentCombo.hitDelay > 0f)
            {
                yield return new WaitForSeconds(currentCombo.hitDelay);
            }

            canQueueNextCombo = true;

            float activeDuration = Mathf.Max(0.01f, currentCombo.duration - currentCombo.hitDelay);
            float timer = 0f;
            HashSet<Collider> hitList = new HashSet<Collider>();

            while (timer < activeDuration)
            {
                timer += Time.deltaTime;
                CheckHitContinuous(currentCombo, hitList);
                yield return null;
            }

            canQueueNextCombo = false;

            if (isNextComboQueued && comboStep < comboList.Length)
            {
                comboStep++;
            }
            else
            {
                break;
            }
        }

        comboStep = 0;
        canQueueNextCombo = false;
        isNextComboQueued = false;

        if (animator != null)
        {
            animator.SetInteger("ComboStep", 0);
            animator.ResetTrigger("Attack");
        }

        yield return new WaitForSeconds(0.1f);
        isAttacking = false;
    }

    private IEnumerator CastSpellRoutine()
    {
        isCasting = true;

        currentFp -= spellFpCost;
        currentStamina -= spellStaminaCost;
        regenTimer = regenDelay;

        if (fpSlider != null) fpSlider.value = currentFp;
        if (staminaSlider != null) staminaSlider.value = currentStamina;

        if (animator != null)
        {
            animator.SetTrigger("CastSpell");
        }

        BossRadagonAI boss = FindObjectOfType<BossRadagonAI>();
        if (boss != null)
        {
            //boss.OnPlayerCastedSpell();
        }

        yield return new WaitForSeconds(0.4f);

        Transform spawnPoint = spellSpawnPoint != null ? spellSpawnPoint : transform;
        Vector3 spawnPos = spawnPoint.position + transform.forward * 0.5f;

        if (spellPrefab != null)
        {
            GameObject spell = Instantiate(spellPrefab, spawnPos, transform.rotation);
            Rigidbody rb = spell.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = transform.forward * spellSpeed;
            }
            Destroy(spell, 5.0f);
        }

        yield return new WaitForSeconds(0.3f);

        isCasting = false;
    }

    private IEnumerator JumpAttackRoutine()
    {
        isAttacking = true;
        currentStamina -= attackStaminaCost;
        regenTimer = regenDelay;

        if (animator != null) animator.SetTrigger("Attack");

        velocity.y = -15f;

        yield return new WaitForSeconds(0.2f);

        Vector3 attackCenter = transform.position + transform.forward * 1.0f;
        Collider[] hitColliders = Physics.OverlapSphere(attackCenter, attackRadius * 1.5f);

        foreach (var hitCollider in hitColliders)
        {
            BossHealth bossHealth = hitCollider.GetComponent<BossHealth>();
            if (bossHealth != null)
            {
                bossHealth.TakeDamage(attackDamage * 1.5f);
            }
        }

        yield return new WaitForSeconds(0.3f);
        isAttacking = false;
    }

    private void CheckHitContinuous(ComboSettings combo, HashSet<Collider> hitList)
    {
        Vector3 attackCenter = transform.position + transform.forward * combo.attackOffset;
        Collider[] hitColliders = Physics.OverlapSphere(attackCenter, attackRadius);

        foreach (var hitCollider in hitColliders)
        {
            if (hitList.Contains(hitCollider)) continue;

            bool damaged = false;

            BossHealth bossHealth = hitCollider.GetComponent<BossHealth>();
            if (bossHealth != null)
            {
                bossHealth.TakeDamage(attackDamage);
                damaged = true;
            }

            BossMinionAI minion = hitCollider.GetComponent<BossMinionAI>();
            if (minion != null)
            {
                minion.TakeDamage(999f);
                damaged = true;
            }

            if (damaged)
            {
                hitList.Add(hitCollider);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (comboList != null && comboList.Length > 0)
        {
            Vector3 attackCenter = transform.position + transform.forward * comboList[0].attackOffset;
            Gizmos.DrawWireSphere(attackCenter, attackRadius);
        }
    }

    private IEnumerator UseHpEstusRoutine()
    {
        if (currentHpEstusCount > 0 && currentHealth < maxHealth)
        {
            isDrinking = true;

            if (animator != null)
            {
                animator.SetTrigger("DrinkPotion");
            }

            BossRadagonAI boss = FindObjectOfType<BossRadagonAI>();
            if (boss != null)
            {
                boss.OnPlayerUsedPotion(transform.position);
            }

            yield return new WaitForSeconds(0.5f);

            currentHpEstusCount--;
            currentHealth += healAmount;
            currentHealth = Mathf.Min(currentHealth, maxHealth);

            UpdateHealthUI();
            UpdateEstusUI();

            yield return new WaitForSeconds(0.5f);

            if (animator != null)
            {
                animator.ResetTrigger("DrinkPotion");
            }

            isDrinking = false;
        }
    }

    private IEnumerator UseFpEstusRoutine()
    {
        if (currentFpEstusCount > 0 && currentFp < maxFp)
        {
            isDrinking = true;

            if (animator != null)
            {
                animator.SetTrigger("DrinkPotion");
            }

            BossRadagonAI boss = FindObjectOfType<BossRadagonAI>();
            if (boss != null)
            {
                boss.OnPlayerUsedPotion(transform.position);
            }

            yield return new WaitForSeconds(0.5f);

            currentFpEstusCount--;
            currentFp += fpHealAmount;
            currentFp = Mathf.Min(currentFp, maxFp);

            if (fpSlider != null) fpSlider.value = currentFp;
            UpdateEstusUI();

            yield return new WaitForSeconds(0.5f);

            if (animator != null)
            {
                animator.ResetTrigger("DrinkPotion");
            }

            isDrinking = false;
        }
    }

    public void TakeDamage(float damage)
    {
        // ★ 無敵中、死亡中、またはダウン〜起き上がり完了までは一切ダメージを受けない（ハメ防止）
        if (isDead || isInvincible || isKnockedDown) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0f);
        UpdateHealthUI();

        // ★ 追加：被弾時のカメラシェイク（大ダメージ時は強めに揺らす）
        if (ThirdPersonCamera.Instance != null)
        {
            if (damage >= knockdownDamageThreshold)
            {
                ThirdPersonCamera.Instance.ShakeCamera(0.25f, 0.35f);
            }
            else
            {
                ThirdPersonCamera.Instance.ShakeCamera(0.12f, 0.18f);
            }
        }

        // 死亡判定
        if (currentHealth <= 0)
        {
            Die();
            return; // ★ 死亡時はここで確実に処理を終了
        }

        // 死亡していない場合のみ大ダメージでダウン発火
        if (damage >= knockdownDamageThreshold)
        {
            StartCoroutine(KnockdownRoutine());
        }
    }

    // ★ 完全無敵保証付きのダウン〜起き上がりルーチン
    private IEnumerator KnockdownRoutine()
    {
        isKnockedDown = true;
        isInvincible = true; // ★ 倒れ始めから無敵開始

        isAttacking = false;
        isRolling = false;
        isDrinking = false;
        isCasting = false;

        if (animator != null)
        {
            animator.SetTrigger("KnockedDown");
        }

        // 倒れている時間＋立ち上がる時間（必要に応じてInspectorの knockdownDuration で調整）
        yield return new WaitForSeconds(knockdownDuration);

        // ★ 完全に立ち上がってプレイヤーが操作可能になってから無敵とダウン判定を同時解除！
        isKnockedDown = false;
        isInvincible = false;

        if (animator != null)
        {
            animator.ResetTrigger("KnockedDown");
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
            estusText.text = $"HP Pot: {currentHpEstusCount}  |  FP Pot: {currentFpEstusCount}";
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("プレイヤー死亡… YOU DIED");

        // 1. 進行中の全コルーチン（攻撃・ポーション・ローリング等）を強制停止
        StopAllCoroutines();

        // 2. 移動フラグ・状態フラグをリセット
        isAttacking = false;
        isRolling = false;
        isDrinking = false;
        isCasting = false;
        isKnockedDown = false;

        // 3. 物理判定・移動制御コンポーネントを停止
        if (controller != null)
        {
            controller.enabled = false; // 当たり判定・押し出し判定を無効化
        }

        // 4. アニメーション（Die）を発火
        if (animator != null)
        {
            animator.SetTrigger("Die");
        }

        // 5. ステージマネージャーへの通知
        StageManager stageManager = FindObjectOfType<StageManager>();
        if (stageManager != null)
        {
            stageManager.OnPlayerDied();
        }

        // 6. 入力スクリプト自身をオフにする
        this.enabled = false;
    }

    public void AE_MeleeHit() { }
    public void AE_OnActionEnd() { }
}