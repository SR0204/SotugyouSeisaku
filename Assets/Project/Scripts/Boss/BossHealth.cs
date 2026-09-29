using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BossHealth : MonoBehaviour
{
    [Header("ステータス設定")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("UI参照")]
    public Slider bossHPSlider;

    void Start()
    {
        currentHealth = maxHealth;
        UpdateUI();
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0f);

        UpdateUI();

        Debug.Log("ボスに " + damage + " ダメージ！ 残りHP: " + currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void UpdateUI()
    {
        if (bossHPSlider != null)
        {
            bossHPSlider.maxValue = maxHealth;
            bossHPSlider.value = currentHealth;
        }
    }

    private void Die()
    {
        Debug.Log("ボス撃破！");

        if (bossHPSlider != null)
        {
            bossHPSlider.gameObject.SetActive(false);
        }

        Destroy(gameObject);
    }
}