using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AoeExplosion : MonoBehaviour
{
    [Header("=== ダメージ設定 ===")]
    public float damage = 25.0f;      // 与えるダメージ
    public float lifeTime = 1.5f;      // オブジェクトの存続時間（エフェクトが消えるまでの時間）

    [Header("=== 演出設定 (派手さアップ) ===")]
    [SerializeField] private ParticleSystem explosionParticle; // 爆発のパーティクル
    [SerializeField] private Light explosionLight;             // 爆発の閃光ライト
    [SerializeField] private float lightDuration = 0.2f;        // 閃光の持続時間
    [SerializeField] private AudioSource audioSource;           // 爆発音再生用
    [SerializeField] private AudioClip explosionSound;         // 爆発のSE

    [Header("=== カメラシェイク設定 ===")]
    [SerializeField] private bool enableCameraShake = true;
    [SerializeField] private float shakeDuration = 0.3f;        // 揺れる時間
    [SerializeField] private float shakeMagnitude = 0.4f;       // 揺れの強さ

    void Start()
    {
        // 1. パーティクル再生
        if (explosionParticle != null)
        {
            explosionParticle.Play();
        }

        // 2. 爆発音（SE）再生
        if (audioSource != null && explosionSound != null)
        {
            audioSource.PlayOneShot(explosionSound);
        }

        // 3. 瞬間的な閃光（ライト）コルーチンを開始
        if (explosionLight != null)
        {
            StartCoroutine(FlashLightRoutine());
        }

        // 4. カメラシェイク（画面揺れ）
        if (enableCameraShake)
        {
            ShakeCamera();
        }

        // 指定時間後に自動消滅（パーティクルや音が消える余裕を持たせる）
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        // プレイヤーに当たったらダメージ
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(damage);
            }
        }
    }

    // 爆発の瞬間にパッと光って消える処理
    private IEnumerator FlashLightRoutine()
    {
        explosionLight.enabled = true;
        float timer = 0f;
        float startIntensity = explosionLight.intensity;

        while (timer < lightDuration)
        {
            timer += Time.deltaTime;
            // ライトの強さを徐々に減衰
            explosionLight.intensity = Mathf.Lerp(startIntensity, 0f, timer / lightDuration);
            yield return null;
        }

        explosionLight.enabled = false;
    }

    // メインカメラを簡易的に揺らす処理
    private void ShakeCamera()
    {
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            StartCoroutine(CameraShakeRoutine(mainCam));
        }
    }

    private IEnumerator CameraShakeRoutine(Camera cam)
    {
        Vector3 originalPos = cam.transform.localPosition;
        float timer = 0f;

        while (timer < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;

            cam.transform.localPosition = originalPos + new Vector3(x, y, 0f);
            timer += Time.deltaTime;
            yield return null;
        }

        cam.transform.localPosition = originalPos;
    }
}