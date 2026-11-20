using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BattleManager : MonoBehaviour
{
    [Header("プレハブ設定")]
    [Tooltip("アリーナプレハブ（ボスID対応、またはランダム選出用）")]
    public GameObject[] arenaPrefabs;
    [Tooltip("特定ボス専用アリーナ（空欄なら通常アリーナ）")]
    public GameObject[] bossSpecificArenas;
    [Tooltip("ボスプレハブ一覧")]
    public GameObject[] bossPrefabs;
    [Tooltip("プレイヤープレハブ")]
    public GameObject playerPrefab;
    [Tooltip("UIキャンバスのプレハブ（Canvas）")]
    public GameObject uiCanvasPrefab;
    [Tooltip("ボス部屋の霧の壁プレハブ（BossFogEntry付き）")]
    public GameObject fogWallPrefab;

    [Header("生成位置（BossEntranceRoomがない場合のデフォルト）")]
    public Vector3 playerSpawnPos = new Vector3(0, 2f, -16f);
    public Vector3 fogWallPosition = new Vector3(0, 0, -10f);
    public Vector3 bossPosition = new Vector3(0, 0, 6f);

    [Header("バトル開始演出設定")]
    [Tooltip("霧がない場合の自動開始待機時間（秒）")]
    public float battleStartDelay = 2.0f;

    private GameObject currentBoss;
    private GameObject currentPlayer;

    // ボス一時停止用リスト
    private List<MonoBehaviour> pausedBossScripts = new List<MonoBehaviour>();
    private UnityEngine.AI.NavMeshAgent bossAgent;
    private bool isBossStarted = false;

    // UI参照用
    private Slider healthSlider;
    private Slider staminaSlider;
    private TextMeshProUGUI estusText;
    private Slider bossHPSlider;
    private TextMeshProUGUI gameOverText;

    private bool isBattleEnd = false;
    private bool isSetupComplete = false;

    void Start()
    {
        StartCoroutine(SetupBattleRoutine());
    }

    private IEnumerator SetupBattleRoutine()
    {
        int bossIndex = GameManager.SelectedBossIndex;

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

            Transform panelObj = canvasObj.transform.Find("BossSelectPanel");
            if (panelObj != null) panelObj.gameObject.SetActive(false);

            Transform lockOnObj = canvasObj.transform.Find("LockOnIcon");
            if (lockOnObj != null && Camera.main != null)
            {
                ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
                if (cam != null) cam.lockOnIcon = lockOnObj.GetComponent<Image>();
            }
        }

        // 2. アリーナ生成
        GameObject spawnedArena = null;
        GameObject selectedArena = null;
        if (bossSpecificArenas != null && bossIndex < bossSpecificArenas.Length && bossSpecificArenas[bossIndex] != null)
        {
            selectedArena = bossSpecificArenas[bossIndex];
        }
        else if (arenaPrefabs != null && arenaPrefabs.Length > 0)
        {
            int idx = Mathf.Clamp(bossIndex, 0, arenaPrefabs.Length - 1);
            selectedArena = arenaPrefabs[idx];
        }

        if (selectedArena != null)
        {
            spawnedArena = Instantiate(selectedArena, Vector3.zero, Quaternion.identity);
        }

        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();

        // 3. ステージ内の "BossEntranceRoom" を自動検索して、プレイヤー位置と霧の位置を自動計算！
        Vector3 finalPlayerSpawnPos = playerSpawnPos;
        Vector3 finalFogWallPos = fogWallPosition;
        Quaternion finalFogWallRot = Quaternion.identity;

        if (spawnedArena != null)
        {
            Transform entrance = FindChildByNameRecursive(spawnedArena.transform, "BossEntranceRoom");
            if (entrance != null)
            {
                // エントランス全体の当たり判定（Bounds）を取得して出口と中心を割り出す
                Collider[] cols = entrance.GetComponentsInChildren<Collider>();
                if (cols.Length > 0)
                {
                    Bounds bounds = cols[0].bounds;
                    for (int i = 1; i < cols.Length; i++)
                    {
                        bounds.Encapsulate(cols[i].bounds);
                    }

                    // プレイヤーはエントランスの中央付近にスポーン
                    finalPlayerSpawnPos = new Vector3(
                        bounds.center.x,
                        bounds.min.y + 2.0f,
                        bounds.center.z - (bounds.size.z * 0.1f)
                    );

                    // 霧の壁はエントランスの前方端（出口、max.z）に自動配置
                    finalFogWallPos = new Vector3(
                        bounds.center.x,
                        bounds.min.y,
                        bounds.max.z
                    );
                }
                else
                {
                    finalPlayerSpawnPos = entrance.position + new Vector3(0, 2f, 0);
                    finalFogWallPos = entrance.position + entrance.forward * 4.5f;
                }
                finalFogWallRot = entrance.rotation;
                fogWallPosition = finalFogWallPos;
            }
        }

        // 4. プレイヤー生成
        if (playerPrefab != null)
        {
            currentPlayer = Instantiate(playerPrefab, finalPlayerSpawnPos, finalFogWallRot);

            PlayerController playerCtrl = currentPlayer.GetComponent<PlayerController>();
            if (playerCtrl != null)
            {
                playerCtrl.healthSlider = healthSlider;
                playerCtrl.staminaSlider = staminaSlider;
                playerCtrl.estusText = estusText;
            }

            if (Camera.main != null)
            {
                ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
                if (cam != null) cam.target = currentPlayer.transform;
            }
        }

        // 5. ボス生成（霧をくぐるまでは行動停止）
        pausedBossScripts.Clear();

        if (bossPrefabs != null && bossIndex < bossPrefabs.Length && bossPrefabs[bossIndex] != null)
        {
            currentBoss = Instantiate(bossPrefabs[bossIndex], bossPosition, Quaternion.identity);

            if (currentPlayer != null)
            {
                Vector3 lookDir = currentPlayer.transform.position - currentBoss.transform.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    currentBoss.transform.rotation = Quaternion.LookRotation(lookDir);
                }
            }

            foreach (var script in currentBoss.GetComponents<MonoBehaviour>())
            {
                if (script != null && script.enabled && !(script is BossHealth))
                {
                    script.enabled = false;
                    pausedBossScripts.Add(script);
                }
            }

            bossAgent = currentBoss.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (bossAgent != null && bossAgent.isOnNavMesh)
            {
                bossAgent.isStopped = true;
            }

            BossHealth bossHealth = currentBoss.GetComponent<BossHealth>();
            if (bossHealth != null && bossHPSlider != null)
            {
                bossHPSlider.gameObject.SetActive(false);
                bossHealth.bossHPSlider = bossHPSlider;
            }
        }

        // 6. 霧の壁をエントランスの出口に自動生成して連動
        bool hasFogWall = false;
        if (fogWallPrefab != null)
        {
            GameObject fogObj = Instantiate(fogWallPrefab, finalFogWallPos, finalFogWallRot);
            BossFogEntry fogEntry = fogObj.GetComponent<BossFogEntry>();
            if (fogEntry != null)
            {
                fogEntry.Initialize(StartBossBattle);
                hasFogWall = true;
            }
        }

        // 7. カメラ初期化
        if (Camera.main != null)
        {
            ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
            if (cam != null)
            {
                cam.InitCameraPosition();
            }
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        isSetupComplete = true;

        if (!hasFogWall)
        {
            yield return new WaitForSeconds(battleStartDelay);
            StartBossBattle();
        }
    }

    // 名前（前方一致）で子オブジェクトを再帰検索するヘルパー
    private Transform FindChildByNameRecursive(Transform parent, string targetName)
    {
        foreach (Transform child in parent)
        {
            if (child.name.StartsWith(targetName)) return child;
            Transform found = FindChildByNameRecursive(child, targetName);
            if (found != null) return found;
        }
        return null;
    }

    public void StartBossBattle()
    {
        if (isBossStarted || isBattleEnd) return;
        isBossStarted = true;

        if (currentBoss != null)
        {
            if (bossHPSlider != null)
            {
                bossHPSlider.gameObject.SetActive(true);
            }

            if (bossAgent != null && bossAgent.isOnNavMesh)
            {
                bossAgent.isStopped = false;
            }

            foreach (var script in pausedBossScripts)
            {
                if (script != null)
                {
                    script.enabled = true;
                }
            }
        }
    }

    void Update()
    {
        if (isBattleEnd || !isSetupComplete) return;

        // 霧の壁を超えたら確実に戦闘開始
        //if (!isBossStarted && fogWallPrefab != null && currentPlayer != null)
        //{
        //    if (currentPlayer.transform.position.z > fogWallPosition.z + 0.5f)
        //    {
        //        StartBossBattle();
        //    }
        //}

        if (currentBoss == null)
        {
            OnBossDefeated();
        }
    }

    public void OnPlayerDied()
    {
        if (isBattleEnd) return;
        StartCoroutine(PlayerDiedRoutine());
    }

    private IEnumerator PlayerDiedRoutine()
    {
        isBattleEnd = true;
        if (bossHPSlider != null) bossHPSlider.gameObject.SetActive(false);
        if (gameOverText != null) gameOverText.gameObject.SetActive(true);

        yield return new WaitForSeconds(3.0f);

        GameManager.LoadRestAreaScene();
    }

    private void OnBossDefeated()
    {
        isBattleEnd = true;
        if (bossHPSlider != null) bossHPSlider.gameObject.SetActive(false);

        StartCoroutine(BossDefeatedRoutine());
    }

    private IEnumerator BossDefeatedRoutine()
    {
        yield return new WaitForSeconds(2.5f);
        GameManager.LoadRestAreaScene();
    }
}