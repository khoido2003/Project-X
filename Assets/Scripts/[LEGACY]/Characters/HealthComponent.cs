using System;
using UnityEngine;

public class HealthComponent : MonoBehaviour
{
    private float maxHealth;
    private float currentHealth;

    public event Action<float, float> OnHealthChanged;
    public event EventHandler OnDeath;

    public void Initialize(StatsData statsData)
    {
        maxHealth = statsData.maxHealth;
        currentHealth = maxHealth;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void ApplyDefenseBoost(float boost, float duration) { }

    private void Die()
    {
        OnDeath?.Invoke(this, EventArgs.Empty);

        // TODO: add death state
        Debug.Log("Character die!");
    }
}
