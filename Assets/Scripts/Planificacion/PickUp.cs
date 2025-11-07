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

    // ── NUEVO: zoom con rueda ───────────────────────────────────────────────
    [Header("Zoom con rueda")]
    public bool enableScrollAdjust = true;
    public bool adjustOnlyWhenHolding = true;     // si quieres que funcione solo al sostener
    public float minHoldZ = 0.8f;                 // límite cercano
    public float maxHoldZ = 4.0f;                 // límite lejano
    public float scrollSpeed = 0.5f;              // cuánto cambia por notch
    // ───────────────────────────────────────────────────────────────────────

    Rigidbody held;

    void Awake()
    {
        if (!playerCam) playerCam = Camera.main;

        // Si no existe, creamos un holdPoint como hijo de la cámara
        if (!holdPoint && playerCam)
        {
            var go = new GameObject("HoldPoint");
            holdPoint = go.transform;
            holdPoint.SetParent(playerCam.transform, false);
            holdPoint.localPosition = new Vector3(0, 0, Mathf.Clamp(2.2f, minHoldZ, maxHoldZ));
        }
    }

    void Update()
    {
        // ── NUEVO: ajustar distancia con la rueda ──
        if (enableScrollAdjust && holdPoint && (!adjustOnlyWhenHolding || held))
        {
            float scroll = Input.mouseScrollDelta.y;   // hacia ti suele ser positivo
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                float z = holdPoint.localPosition.z;
                z = Mathf.Clamp(z - scroll * scrollSpeed, minHoldZ, maxHoldZ);
                holdPoint.localPosition = new Vector3(holdPoint.localPosition.x, holdPoint.localPosition.y, z);
            }
        }
        // ────────────────────────────────────────────

        if (Input.GetMouseButtonDown(0)) TryPickupOrDrop();
        if (held) MoveHeld();
    }

    void TryPickupOrDrop()
    {
        if (held) { Drop(); return; }

        Ray ray = new Ray(playerCam.transform.position, playerCam.transform.forward);
        if (Physics.Raycast(ray, out var hit, pickupDistance, pickupMask, QueryTriggerInteraction.Ignore))
        {
            var rb = hit.rigidbody;
            if (rb && hit.collider.CompareTag("Draggable"))
            {
                held = rb;
                held.useGravity = false;
                // En Unity 6 este nombre existe; si usas versiones previas cambia a rb.drag
                held.linearDamping = 8f;
            }
        }
    }

    void MoveHeld()
    {
        Vector3 toPos = holdPoint.position - held.position;
        held.AddForce(toPos * moveForce * Time.deltaTime, ForceMode.VelocityChange);

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Q))
            Drop();
    }

    public void Drop()
    {
        if (!held) return;
        held.useGravity = true;
        // En Unity 6: linearDamping; en previas: rb.drag = 0
        held.linearDamping = 0f;
        held = null;
    }

    public Rigidbody GetHeld() => held;
}
