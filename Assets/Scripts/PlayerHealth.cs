using System;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    [Tooltip("Invulnerability time after being hit, so one laser = one hit.")]
    public float hitCooldown = 1f;

    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }
    public float ShieldTimeLeft => Mathf.Max(0f, shieldTimer);
    public float HitFlash => Mathf.Clamp01(flashTimer / 0.4f);

    public event Action Died;

    private float cooldownTimer;
    private float shieldTimer;
    private float flashTimer;

    void Awake()
    {
        CurrentHealth = maxHealth;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (cooldownTimer > 0f) cooldownTimer -= dt;
        if (shieldTimer > 0f) shieldTimer -= dt;
        if (flashTimer > 0f) flashTimer -= dt;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || shieldTimer > 0f || cooldownTimer > 0f) return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        cooldownTimer = hitCooldown;
        flashTimer = 0.4f;

        if (CurrentHealth <= 0f)
        {
            IsDead = true;
            Died?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
    }

    public void ApplyShield(float duration)
    {
        if (IsDead) return;
        shieldTimer = Mathf.Max(shieldTimer, duration);
    }

    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
        IsDead = false;
        cooldownTimer = 0f;
        shieldTimer = 0f;
        flashTimer = 0f;
    }
}
