using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossProximity : MonoBehaviour
{
    [Header("予兆エフェクト")]
    [SerializeField] private GameObject redZonePrefab; // 赤い床（予兆用プレハブ）

    [Header("近接攻撃の設定")]
    [SerializeField] private float lightAttackRange = 2.5f;   // 通常攻撃（前方小円）
    [SerializeField] private float lightDamage = 15.0f;

    [SerializeField] private float heavyAttackRange = 4.0f;   // 強攻撃（前方大円）
    [SerializeField] private float heavyDamage = 35.0f;

    [SerializeField] private float spinAttackRange = 3.5f;    // なぎ払い（全周囲）
    [SerializeField] private float spinDamage = 20.0f;

    private Transform player;
    private PlayerController playerController;
    private bool isAttacking = false;

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerController = playerObj.GetComponent<PlayerController>();
        }
    }

    public void PerformRandomMeleeAttack()
    {
        if (isAttacking || player == null) return;

        int attackType = Random.Range(0, 3);
        switch (attackType)
        {
            case 0:
                StartCoroutine(LightAttackRoutine());
                break;
            case 1:
                StartCoroutine(HeavyAttackRoutine());
                break;
            case 2:
                StartCoroutine(SpinAttackRoutine());
                break;
        }
    }

    // 1. 通常攻撃（前方に小範囲予兆）
    private IEnumerator LightAttackRoutine()
    {
        isAttacking = true;
        Debug.Log("ボス：通常攻撃予兆！");

        // 予兆表示位置（ボスの少し前方）
        Vector3 zonePos = transform.position + transform.forward * (lightAttackRange * 0.5f);
        zonePos.y = 0.05f; // 地面より少し上

        GameObject zone = ShowRedZone(zonePos, new Vector3(lightAttackRange, lightAttackRange, 1f));

        yield return new WaitForSeconds(0.6f); // 溜め時間（回避可能時間）

        Destroy(zone); // 予兆消去

        // ダメージ判定
        float distance = Vector3.Distance(transform.position, player.position);
        if (distance <= lightAttackRange)
        {
            if (playerController != null) playerController.TakeDamage(lightDamage);
            Debug.Log("通常攻撃ヒット！");
        }

        isAttacking = false;
    }

    // 2. 強攻撃（前方に広い範囲予兆）
    private IEnumerator HeavyAttackRoutine()
    {
        isAttacking = true;
        Debug.Log("ボス：強攻撃予兆！");

        Vector3 zonePos = transform.position + transform.forward * (heavyAttackRange * 0.5f);
        zonePos.y = 0.05f;

        GameObject zone = ShowRedZone(zonePos, new Vector3(heavyAttackRange, heavyAttackRange * 1.2f, 1f));

        yield return new WaitForSeconds(1.0f); // 溜め時間（少し長め）

        Destroy(zone);

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance <= heavyAttackRange)
        {
            if (playerController != null) playerController.TakeDamage(heavyDamage);
            Debug.Log("強攻撃ヒット！");
        }

        isAttacking = false;
    }

    // 3. 周囲なぎ払い（ボスの足元を中心に全方位予兆）
    private IEnumerator SpinAttackRoutine()
    {
        isAttacking = true;
        Debug.Log("ボス：全方位なぎ払い予兆！");

        Vector3 zonePos = transform.position;
        zonePos.y = 0.05f;

        GameObject zone = ShowRedZone(zonePos, new Vector3(spinAttackRange * 2f, spinAttackRange * 2f, 1f));

        yield return new WaitForSeconds(0.8f);

        Destroy(zone);

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance <= spinAttackRange)
        {
            if (playerController != null) playerController.TakeDamage(spinDamage);
            Debug.Log("なぎ払いヒット！");
        }

        isAttacking = false;
    }

    // 赤い床を表示するヘルパー関数
    private GameObject ShowRedZone(Vector3 position, Vector3 scale)
    {
        if (redZonePrefab == null) return null;

        GameObject zone = Instantiate(redZonePrefab, position, Quaternion.Euler(90f, transform.eulerAngles.y, 0f));
        zone.transform.localScale = scale;
        return zone;
    }

    // UnityエディタのScene画面で範囲を確認・デバッグ用（ギズモ描画）
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + transform.forward * (lightAttackRange * 0.5f), lightAttackRange * 0.5f);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + transform.forward * (heavyAttackRange * 0.5f), heavyAttackRange * 0.5f);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, spinAttackRange);
    }
}