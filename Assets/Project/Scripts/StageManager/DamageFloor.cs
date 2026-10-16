using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageFloor : MonoBehaviour
{
    [Header("=== ダメージ設定 ===")]
    [Tooltip("1秒あたりにプレイヤーに与えるダメージ量")]
    public float damagePerSecond = 10f;

    private void OnTriggerStay(Collider other)
    {
        // 触れているのがプレイヤーの場合
        if (other.CompareTag("Player"))
        {
            // プレイヤーの足元をチェック
            if (IsPlayerOnSafeZone(other.gameObject))
            {
                Debug.Log("<color=green>【安全地帯】SafeZoneの上にいるためダメージを無効化中</color>");
                return; // 安全地帯の上ならダメージを与えない
            }

            Debug.Log("<color=red>【マグマ上】ダメージ発生中！</color>");

            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(damagePerSecond * Time.deltaTime);
            }
        }
    }

    // プレイヤーの足元が SafeZone か判定する処理
    private bool IsPlayerOnSafeZone(GameObject playerObj)
    {
        // プレイヤーの位置から少し上を開始点にする
        Vector3 rayStart = playerObj.transform.position + Vector3.up * 0.5f;

        // Scene画面でレイの判定線を視覚化（安全＝緑、ヒット確認用）
        Debug.DrawRay(rayStart, Vector3.down * 2.0f, Color.yellow);

        // プレイヤー自身のヒットを無視するために RaycastAll を使用
        RaycastHit[] hits = Physics.RaycastAll(rayStart, Vector3.down, 2.0f);

        foreach (var hit in hits)
        {
            // プレイヤー自身やその子要素は無視
            if (hit.collider.gameObject == playerObj || hit.collider.transform.IsChildOf(playerObj.transform))
            {
                continue;
            }

            // 当たったオブジェクトの名前とタグをログ出力
            Debug.Log($"足元が検出したオブジェクト: {hit.collider.name} (Tag: {hit.collider.tag})");

            // SafeZone タグがついたオブジェクトなら安全
            if (hit.collider.CompareTag("SafeZone"))
            {
                return true;
            }
        }

        return false;
    }
}