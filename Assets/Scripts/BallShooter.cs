using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class BallShooter : MonoBehaviour
{
    [Header("Ball")]
    public float ballSpeed = 22f;
    public float ballLifetime = 8f;
    public float fireCooldown = 0.22f;
    [Range(0f, 1f)] public float bounciness = 0.92f;
    public float mass = 0.35f;
    public float impactForce = 8f;
    public float ballRadius = 0.16f;

    [Header("Magazine")]
    public int magazineSize = 10;
    public float reloadTime = 1.5f;

    [Header("References")]
    public Camera aimCamera;
    public GameManager gameManager;
    public UIManager uiManager;
    public Material ballMaterial;

    int ammo;
    bool reloading;
    float nextFireTime;

    public int Ammo => ammo;
    public bool IsReloading => reloading;

    void Start()
    {
        ammo = Mathf.Max(1, magazineSize);
        UpdateUI();
    }

    void Update()
    {
        if (gameManager != null && gameManager.IsFinished) return;
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && !reloading) StartCoroutine(Reload());
        if (ammo <= 0 && !reloading) { StartCoroutine(Reload()); return; }
        if (!reloading && Mouse.current != null && Mouse.current.leftButton.isPressed && Time.time >= nextFireTime)
            Fire();
    }

    void Fire()
    {
        if (aimCamera == null) return;
        nextFireTime = Time.time + Mathf.Max(0.02f, fireCooldown);
        ammo--;
        if (gameManager != null) gameManager.RegisterShot();

        Vector3 origin = aimCamera.transform.position + aimCamera.transform.forward * 0.45f;
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "BouncingBall";
        ball.transform.position = origin;
        ball.transform.localScale = Vector3.one * ballRadius * 2f;
        if (ballMaterial != null) ball.GetComponent<Renderer>().sharedMaterial = ballMaterial;
        Collider collider = ball.GetComponent<Collider>();
        PhysicsMaterial physicsMaterial = new PhysicsMaterial("BouncyBall");
        physicsMaterial.bounciness = Mathf.Clamp01(bounciness);
        physicsMaterial.bounceCombine = PhysicsMaterialCombine.Maximum;
        physicsMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;
        physicsMaterial.dynamicFriction = 0.05f;
        physicsMaterial.staticFriction = 0.05f;
        collider.material = physicsMaterial;

        Rigidbody rb = ball.AddComponent<Rigidbody>();
        rb.mass = Mathf.Max(0.01f, mass);
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.linearVelocity = aimCamera.transform.forward * ballSpeed;
        BouncingBall behavior = ball.AddComponent<BouncingBall>();
        behavior.lifetime = ballLifetime;
        behavior.impactForce = impactForce;
        UpdateUI();
    }

    IEnumerator Reload()
    {
        if (reloading) yield break;
        reloading = true;
        UpdateUI();
        yield return new WaitForSeconds(Mathf.Max(0.1f, reloadTime));
        ammo = Mathf.Max(1, magazineSize);
        reloading = false;
        UpdateUI();
    }

    void UpdateUI() { if (uiManager != null) uiManager.SetAmmo(ammo, magazineSize, reloading); }
}
