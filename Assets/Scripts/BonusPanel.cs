using System;
using UnityEngine;

public enum BonusType
{
    Heal,
    Speed,
    Shield
}

// Permanent floor panel that grants a bonus
// when the player steps on it.
public class BonusPanel : MonoBehaviour
{
    public BonusType type;

    private CharacterController body;
    private Action<BonusPanel> onCollected;

    private Renderer rend;
    private Color baseColor;

    private bool collected;

    public void Init(
        BonusType type,
        Color color,
        CharacterController body,
        Action<BonusPanel> onCollected
    )
    {
        this.type = type;
        this.body = body;
        this.onCollected = onCollected;

        rend =
            GetComponent<Renderer>();

        baseColor = color;

        collected = false;

        if (rend != null)
            rend.material.color =
                baseColor;
    }

    void Update()
    {
        if (
            collected ||
            body == null
        )
        {
            return;
        }

        Vector3 p =
            transform.position;

        Vector3 s =
            transform.localScale;

        Bounds b =
            body.bounds;

        bool insideXZ =
            Mathf.Abs(
                b.center.x -
                p.x
            ) <
            s.x * 0.5f
            &&
            Mathf.Abs(
                b.center.z -
                p.z
            ) <
            s.z * 0.5f;

        // Feet must be near the floor.
        // Jumping over the panel does not activate it.
        bool onFloor =
            b.min.y <
            p.y +
            s.y * 0.5f +
            0.3f;

        if (
            insideXZ &&
            onFloor
        )
        {
            collected = true;

            if (rend != null)
            {
                rend.material.color =
                    new Color(
                        0.25f,
                        0.25f,
                        0.25f
                    );
            }

            onCollected?.Invoke(
                this
            );
        }
    }

    public void ResetPanel()
    {
        collected = false;

        if (rend != null)
        {
            rend.material.color =
                baseColor;
        }
    }
}