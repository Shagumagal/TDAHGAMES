using System.Collections.Generic;
using UnityEngine;

public class PickUp : MonoBehaviour
{
    [Header("Referencias")]
    public Camera playerCam;
    public Transform holdPoint;
    public LayerMask pickupMask;

    [Header("Ajustes")]
    public float pickupDistance = 3f;
    public float moveForce = 300f;

    // ── Zoom con rueda ───────────────────────────────────────────────
    [Header("Zoom con rueda")]
    public bool enableScrollAdjust = true;
    public bool adjustOnlyWhenHolding = true;     // solo ajustar cuando estoy sosteniendo algo
    public float minHoldZ = 0.8f;                 // límite cercano
    public float maxHoldZ = 4.0f;                 // límite lejano
    public float scrollSpeed = 0.5f;              // cuánto cambia por notch
    // ────────────────────────────────────────────────────────────────

    Rigidbody held;

    // Estado original del rigidbody para restaurar al soltar
    RigidbodyConstraints heldOriginalConstraints;
    float heldOriginalLinearDamping;
    float heldOriginalAngularDamping;
    bool heldOriginalUseGravity;

    // Colisiones ignoradas mientras tengo algo en la mano
    Collider heldCollider;
    readonly List<Collider> ignoredColliders = new();

    void Awake()
    {
        if (!playerCam) playerCam = Camera.main;

        // Si no existe, creamos un holdPoint como hijo de la cámara
        if (!holdPoint && playerCam)
        {
            var go = new GameObject("HoldPoint");
            holdPoint = go.transform;
            holdPoint.SetParent(playerCam.transform, false);
            holdPoint.localPosition = new Vector3(
                0f,
                0f,
                Mathf.Clamp(2.2f, minHoldZ, maxHoldZ)
            );
        }
    }

    void Update()
    {
        // ── Ajustar distancia con la rueda ────────────────────────────
        if (enableScrollAdjust && holdPoint && (!adjustOnlyWhenHolding || held))
        {
            float scroll = Input.mouseScrollDelta.y;   // hacia ti suele ser positivo
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                float z = holdPoint.localPosition.z;
                // restamos para que “hacia ti” acerque el objeto
                z = Mathf.Clamp(z - scroll * scrollSpeed, minHoldZ, maxHoldZ);
                holdPoint.localPosition = new Vector3(
                    holdPoint.localPosition.x,
                    holdPoint.localPosition.y,
                    z
                );
            }
        }
        // ──────────────────────────────────────────────────────────────

        if (Input.GetMouseButtonDown(0))
            TryPickupOrDrop();

        if (held)
            MoveHeld();
    }

    void TryPickupOrDrop()
    {
        // si ya tengo algo en la mano, soltar
        if (held)
        {
            Drop();
            return;
        }

        Ray ray = new Ray(playerCam.transform.position, playerCam.transform.forward);

        if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                pickupDistance,
                pickupMask,
                QueryTriggerInteraction.Ignore))
        {
            var rb = hit.rigidbody;
            if (rb && hit.collider.CompareTag("Draggable"))
            {
                held = rb;
                heldCollider = held.GetComponent<Collider>();

                // Guardar estado original
                heldOriginalConstraints    = held.constraints;
                heldOriginalLinearDamping  = held.linearDamping;   // Unity 6
                heldOriginalAngularDamping = held.angularDamping;  // Unity 6
                heldOriginalUseGravity     = held.useGravity;

                // Ajustes mientras está agarrado
                held.useGravity = false;

                // Congelar rotación para que no empiece a girar si choca
                held.constraints |=
                    RigidbodyConstraints.FreezeRotationX |
                    RigidbodyConstraints.FreezeRotationY |
                    RigidbodyConstraints.FreezeRotationZ;

                // Amortiguar movimientos bruscos
                held.linearDamping  = 8f;
                held.angularDamping = 8f;

                // ── NUEVO: ignorar colisiones con otros animales ───────
                ignoredColliders.Clear();
                if (heldCollider)
                {
                    GameObject[] others = GameObject.FindGameObjectsWithTag("Draggable");
                    foreach (var go in others)
                    {
                        if (!go || go == held.gameObject) continue;
                        var col = go.GetComponent<Collider>();
                        if (!col) continue;

                        Physics.IgnoreCollision(heldCollider, col, true);
                        ignoredColliders.Add(col);
                    }
                }
                // ────────────────────────────────────────────────────────
            }
        }
    }

    void MoveHeld()
    {
        Vector3 toPos = holdPoint.position - held.position;

        // Mover hacia el holdPoint usando fuerza
        held.AddForce(toPos * moveForce * Time.deltaTime, ForceMode.VelocityChange);

        // Botón derecho o Q para soltar
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Q))
            Drop();
    }

    public void Drop()
    {
        if (!held) return;

        // Restaurar estado original del rigidbody
        held.useGravity     = heldOriginalUseGravity;
        held.linearDamping  = heldOriginalLinearDamping;
        held.angularDamping = heldOriginalAngularDamping;
        held.constraints    = heldOriginalConstraints;

        // Volver a activar colisiones con otros animales
        if (heldCollider)
        {
            foreach (var col in ignoredColliders)
            {
                if (!col) continue;
                Physics.IgnoreCollision(heldCollider, col, false);
            }
        }
        ignoredColliders.Clear();
        heldCollider = null;

        held = null;
    }

    public Rigidbody GetHeld() => held;
}
