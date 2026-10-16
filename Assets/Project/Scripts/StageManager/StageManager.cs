using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StageManager : MonoBehaviour
{
    [Header("プレハブ設定")]
    [Tooltip("通常のアリーナプレハブ（ランダム選出用）")]
    public GameObject[] arenaPrefabs;

    [Tooltip("特定ボス専用のアリーナ（例: コロシアムなど）。空欄なら通常アリーナからランダム選出")]
    public GameObject[] bossSpecificArenas;

    public GameObject restAreaPrefab;           // 休息専用エリア
    public GameObject playerPrefab;
    public GameObject[] bossPrefabs;
    public GameObject uiCanvasPrefab;

    [Header("生成位置設定")]
    public Vector3 stagePosition = Vector3.zero;        // ★ (0, 0, 0) に統一
    public Vector3 playerPosition = Vector3.zero;       // ★ (0, 0, 0) に統一
    public Vector3 bossPosition = new Vector3(0, 0, 10f); // ★ Y=0 に統一（Z軸のみ距離を保つ）

    private GameObject currentStageInstance;
    private GameObject currentBossInstance;
    private GameObject currentPlayerInstance;
    private PlayerController playerCtrl;

    // UI参照用
    private Slider healthSlider;
    private Slider staminaSlider;
    private TextMeshProUGUI estusText;
    private Slider bossHPSlider;
    private GameObject bossSelectPanel;
    private TextMeshProUGUI gameOverText;

    private bool isStageActive = false;

    void Start()
    {
        InitializeStage();
    }

    void InitializeStage()
    {
        // 1. UI Canvas を生成
        if (uiCanvasPrefab != null)
        {
            GameObject canvasObj = Instantiate(uiCanvasPrefab);

            Transform hpObj = canvasObj.transform.Find("HPBar");
            if (hpObj != null) healthSlider = hpObj.GetComponent<Slider>();

            Transform staminaObj = canvasObj.transform.Find("StaminaBar");
            if (staminaObj != null) staminaSlider = staminaObj.GetComponent<Slider>();

            Transform potionObj = canvasObj.transform.Find("Potion");
            if (potionObj != null) estusText = potionObj.GetComponent<TextMeshProUGUI>();

            Transform bossHPObj = canvasObj.transform.Find("BossHPBar");
            if (bossHPObj != null) bossHPSlider = bossHPObj.GetComponent<Slider>();

            Transform gameOverObj = canvasObj.transform.Find("GameOverText");
            if (gameOverObj != null)
            {
                gameOverText = gameOverObj.GetComponent<TextMeshProUGUI>();
                gameOverText.gameObject.SetActive(false);
            }

            Transform lockOnIconObj = canvasObj.transform.Find("LockOnIcon");
            if (lockOnIconObj != null && Camera.main != null)
            {
                ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
                if (cam != null)
                {
                    cam.lockOnIcon = lockOnIconObj.GetComponent<Image>();
                }
            }

            // ボス選択ボタン自動登録
            Transform panelObj = canvasObj.transform.Find("BossSelectPanel");
            if (panelObj != null)
            {
                bossSelectPanel = panelObj.gameObject;

                for (int i = 0; i < bossPrefabs.Length; i++)
                {
                    int bossIndex = i;
                    string buttonName = $"Boss{bossIndex + 1}Button";

                    Transform btnObj = panelObj.Find(buttonName);
                    if (btnObj != null)
                    {
                        Button btn = btnObj.GetComponent<Button>();
                        if (btn != null)
                        {
                            btn.onClick.RemoveAllListeners();
                            btn.onClick.AddListener(() => SelectAndStartBoss(bossIndex));
                        }
                    }
                }

                bossSelectPanel.SetActive(false);
            }
        }

        // 2. Player 生成
        SpawnPlayer(healthSlider, staminaSlider, estusText);

        // 3. 最初は休息エリアを表示
        ShowRestArea();
    }

    void SpawnPlayer(Slider hpSlider, Slider stSlider, TextMeshProUGUI estText)
    {
        if (currentPlayerInstance != null) Destroy(currentPlayerInstance);

        if (playerPrefab != null)
        {
            // 仮位置で生成
            currentPlayerInstance = Instantiate(playerPrefab, playerPosition, Quaternion.identity);
            playerCtrl = currentPlayerInstance.GetComponent<PlayerController>();

            if (playerCtrl != null)
            {
                playerCtrl.healthSlider = hpSlider;
                playerCtrl.staminaSlider = stSlider;
                playerCtrl.estusText = estText;
            }

            if (Camera.main != null)
            {
                ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
                if (cam != null) cam.target = currentPlayerInstance.transform;
            }
        }
    }

    // ★ プレイヤーの自動安全接地テレポート
    private void TeleportPlayer(Vector3 targetPos)
    {
        if (currentPlayerInstance == null) return;

        CharacterController cc = currentPlayerInstance.GetComponent<CharacterController>();
        Rigidbody rb = currentPlayerInstance.GetComponent<Rigidbody>();

        if (cc != null) cc.enabled = false;

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // ★ 真下の床をRaycastで検知し、すり抜けない高さに着地させる
        Vector3 finalPos = targetPos;
        RaycastHit hit;
        if (Physics.Raycast(targetPos + Vector3.up * 10f, Vector3.down, out hit, 50f))
        {
            finalPos.y = hit.point.y + 0.1f; // 床の上に確実に乗せる
        }

        currentPlayerInstance.transform.position = finalPos;

        if (cc != null) cc.enabled = true;
    }

    void Update()
    {
        if (isStageActive && currentBossInstance == null)
        {
            OnBossDefeated();
        }
    }

    void OnBossDefeated()
    {
        isStageActive = false;

        if (bossHPSlider != null)
        {
            bossHPSlider.gameObject.SetActive(false);
        }

        Debug.Log("ボス撃破！休息エリアを表示します。");
        ShowRestArea();
    }

    public void ShowRestArea()
    {
        if (currentStageInstance != null)
        {
            Destroy(currentStageInstance);
        }

        if (restAreaPrefab != null)
        {
            currentStageInstance = Instantiate(restAreaPrefab, stagePosition, Quaternion.identity);
            Physics.SyncTransforms(); // 地形物理の即時更新
        }

        TeleportPlayer(playerPosition);

        RestAndHeal();

        if (bossSelectPanel != null)
        {
            bossSelectPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // ★ ボス選択＆アリーナ生成
    public void SelectAndStartBoss(int bossIndex)
    {
        if (bossIndex < 0 || bossIndex >= bossPrefabs.Length) return;

        if (bossSelectPanel != null)
        {
            bossSelectPanel.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (currentStageInstance != null)
        {
            Destroy(currentStageInstance);
        }

        // アリーナの決定
        GameObject selectedArenaPrefab = null;

        if (bossSpecificArenas != null && bossIndex < bossSpecificArenas.Length && bossSpecificArenas[bossIndex] != null)
        {
            selectedArenaPrefab = bossSpecificArenas[bossIndex];
        }
        else if (arenaPrefabs != null && arenaPrefabs.Length > 0)
        {
            int randomIndex = Random.Range(0, arenaPrefabs.Length);
            selectedArenaPrefab = arenaPrefabs[randomIndex];
        }

        // アリーナ生成
        if (selectedArenaPrefab != null)
        {
            currentStageInstance = Instantiate(selectedArenaPrefab, stagePosition, Quaternion.identity);
            Physics.SyncTransforms(); // ★ 生成したアリーナの物理コライダーを即時同期
        }

        // プレイヤーを着地テレポート
        TeleportPlayer(playerPosition);

        // ボス生成処理
        if (bossPrefabs[bossIndex] != null)
        {
            // 1. 生成
            currentBossInstance = Instantiate(bossPrefabs[bossIndex], bossPosition, Quaternion.identity);

            // 2. 移動を阻害するコンポーネントを一時停止
            CharacterController cc = currentBossInstance.GetComponent<CharacterController>();
            UnityEngine.AI.NavMeshAgent agent = currentBossInstance.GetComponent<UnityEngine.AI.NavMeshAgent>();
            Collider bossCol = currentBossInstance.GetComponent<Collider>();

            if (cc != null) cc.enabled = false;
            if (agent != null) agent.enabled = false;
            if (bossCol != null) bossCol.enabled = false;

            // 3. 真下の床の高さを Raycast で取得
            Vector3 spawnPos = bossPosition;
            RaycastHit hit;
            if (Physics.Raycast(bossPosition + Vector3.up * 20f, Vector3.down, out hit, 100f))
            {
                // ★ 床の表面（hit.point.y）から大きめに持ち上げる（例: + 0.5f 〜 1.0f）
                // CharacterController や NavMesh の接地判定で自然に床の上に着地します
                spawnPos.y = hit.point.y + 0.5f;
            }

            // 4. トランスフォームの位置を変更
            currentBossInstance.transform.position = spawnPos;

            // 5. 先に CharacterController / Collider を戻す
            if (bossCol != null) bossCol.enabled = true;
            if (cc != null) cc.enabled = true;

            // 6. 最後に NavMeshAgent を Warp して有効化
            if (agent != null)
            {
                agent.Warp(spawnPos);
                agent.enabled = true;
            }

            // 7. UI設定
            BossHealth bossHealth = currentBossInstance.GetComponent<BossHealth>();
            if (bossHealth != null && bossHPSlider != null)
            {
                bossHPSlider.gameObject.SetActive(true);
                bossHealth.bossHPSlider = bossHPSlider;
            }
        }

        isStageActive = true;
    }

    public void RestAndHeal()
    {
        if (playerCtrl != null)
        {
            playerCtrl.currentHealth = playerCtrl.maxHealth;
            playerCtrl.currentEstusCount = playerCtrl.maxEstusCount;
            playerCtrl.TakeDamage(0);
            Debug.Log("休息完了：HPとエスト瓶が全回復しました！");
        }
    }

    public void OnPlayerDied()
    {
        StartCoroutine(GameOverRoutine());
    }

    private IEnumerator GameOverRoutine()
    {
        isStageActive = false;

        if (currentBossInstance != null)
        {
            Destroy(currentBossInstance);
        }

        if (bossHPSlider != null)
        {
            bossHPSlider.gameObject.SetActive(false);
        }

        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(true);
        }

        yield return new WaitForSeconds(3.0f);

        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(false);
        }

        SpawnPlayer(healthSlider, staminaSlider, estusText);
        ShowRestArea();
    }
}