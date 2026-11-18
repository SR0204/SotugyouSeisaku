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

    [Header("バトル開始演出設定")]
    [Tooltip("暗転が明けてからボスが動き出すまでの待機時間（秒）")]
    public float battleStartDelay = 2.0f;

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

                // ★ 起動時はボス選択UIを表示してマウスカーソルを解放
                bossSelectPanel.SetActive(true);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    void SpawnPlayer(Slider hpSlider, Slider stSlider, TextMeshProUGUI estText)
    {
        if (currentPlayerInstance != null) Destroy(currentPlayerInstance);

        if (playerPrefab != null)
        {
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
                if (cam != null)
                {
                    cam.target = currentPlayerInstance.transform;
                    cam.InitCameraPosition();
                }
            }
        }
    }

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

        Vector3 finalPos = targetPos;
        finalPos.y += 0.2f;

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

        Debug.Log("ボス撃破！余韻を残して休息エリアを表示します。");
        StartCoroutine(TransitionToRestAreaRoutine());
    }

    private IEnumerator TransitionToRestAreaRoutine()
    {
        yield return new WaitForSeconds(2.5f);
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
            Physics.SyncTransforms();
        }

        SpawnPlayer(healthSlider, staminaSlider, estusText);
        TeleportPlayer(playerPosition);
        RestAndHeal();

        if (bossSelectPanel != null)
        {
            bossSelectPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // ★ ボス選択＆アリーナ生成（開幕ディレイ付き）
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

        // 1. アリーナの決定と生成（床を最優先配置）
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

        if (selectedArenaPrefab != null)
        {
            currentStageInstance = Instantiate(selectedArenaPrefab, stagePosition, Quaternion.identity);
            Physics.SyncTransforms();
        }

        // 2. 床が固定されてからプレイヤーを生成＆安全配置
        SpawnPlayer(healthSlider, staminaSlider, estusText);
        TeleportPlayer(playerPosition);

        // 3. ボス生成処理
        if (bossPrefabs[bossIndex] != null)
        {
            currentBossInstance = Instantiate(bossPrefabs[bossIndex], bossPosition, Quaternion.identity);

            CharacterController cc = currentBossInstance.GetComponent<CharacterController>();
            UnityEngine.AI.NavMeshAgent agent = currentBossInstance.GetComponent<UnityEngine.AI.NavMeshAgent>();
            Collider bossCol = currentBossInstance.GetComponent<Collider>();

            if (cc != null) cc.enabled = false;
            if (agent != null) agent.enabled = false;
            if (bossCol != null) bossCol.enabled = false;

            // 高さ補正
            Vector3 spawnPos = bossPosition;
            RaycastHit hit;
            if (Physics.Raycast(bossPosition + Vector3.up * 3f, Vector3.down, out hit, 10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                spawnPos.y = hit.point.y + 0.2f;
            }
            else
            {
                spawnPos.y = bossPosition.y + 0.2f;
            }

            currentBossInstance.transform.position = spawnPos;

            // プレイヤーの方を静かに向かせて構えさせる
            if (currentPlayerInstance != null)
            {
                Vector3 lookDir = currentPlayerInstance.transform.position - spawnPos;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    currentBossInstance.transform.rotation = Quaternion.LookRotation(lookDir);
                }
            }

            if (bossCol != null) bossCol.enabled = true;
            if (cc != null) cc.enabled = true;

            if (agent != null)
            {
                agent.Warp(spawnPos);
                agent.enabled = true;
            }

            // UI設定（開幕ディレイが終わるまで一旦非表示にしておく）
            BossHealth bossHealth = currentBossInstance.GetComponent<BossHealth>();
            if (bossHealth != null && bossHPSlider != null)
            {
                bossHPSlider.gameObject.SetActive(false);
                bossHealth.bossHPSlider = bossHPSlider;
            }

            // カメラのターゲットセット＆敵方向自動ロックオン
            if (Camera.main != null)
            {
                ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
                if (cam != null)
                {
                    cam.target = currentPlayerInstance.transform;
                    cam.InitCameraPosition();
                }
            }

            // ★ 開幕ディレイ演出を開始
            StartCoroutine(BattleStartDelayRoutine(currentBossInstance));
        }

        isStageActive = true;
    }

    // ★ 開幕の「間」を作り、待機後にボスAIとHPバーを起動するコルーチン
    private IEnumerator BattleStartDelayRoutine(GameObject bossObj)
    {
        if (bossObj == null) yield break;

        // ボスについている行動系スクリプトを一時停止（BossHealth以外を停止）
        MonoBehaviour[] allScripts = bossObj.GetComponents<MonoBehaviour>();
        List<MonoBehaviour> pausedScripts = new List<MonoBehaviour>();

        foreach (var script in allScripts)
        {
            if (script != null && script.enabled && !(script is BossHealth))
            {
                script.enabled = false;
                pausedScripts.Add(script);
            }
        }

        UnityEngine.AI.NavMeshAgent agent = bossObj.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }

        // 指定秒数（デフォルト2秒）だけ対峙する「間」を作る
        yield return new WaitForSeconds(battleStartDelay);

        // ボスがまだ生存していれば行動開始＆HPバー表示！
        if (bossObj != null)
        {
            if (bossHPSlider != null)
            {
                bossHPSlider.gameObject.SetActive(true);
            }

            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
            }

            foreach (var script in pausedScripts)
            {
                if (script != null)
                {
                    script.enabled = true;
                }
            }
        }
    }

    public void RestAndHeal()
    {
        if (playerCtrl != null)
        {
            playerCtrl.currentHealth = playerCtrl.maxHealth;
            playerCtrl.currentFp = playerCtrl.maxFp;
            playerCtrl.currentHpEstusCount = playerCtrl.hpEstusCount;
            playerCtrl.currentFpEstusCount = playerCtrl.fpEstusCount;

            playerCtrl.TakeDamage(0);

            Debug.Log("休息完了：HP・FP・ポーションが全回復しました！");
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

        ShowRestArea();
    }
}