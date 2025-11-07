using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ObjectGrabber : MonoBehaviour
{
    [Header("Referencias")]
    public Camera cam;
    public Transform holdPoint;

    [Header("Selección / Alcance")]
    public LayerMask pickupMask = ~0;      // pon aquí la capa "Item"
    public float interactDistance = 6f;
    public float holdDistance = 2.2f;

    [Header("Movimiento al sostener")]
    public float pullStrength = 40f;       // fuerza para llevar al HoldPoint
    public float maxSpeed = 15f;           // velocidad máxima del objeto
    public float rotateSpeed = 8f;         // seguimiento de rotación de la cámara
    public float maxMass = 15f;            // no tomar objetos más pesados

    [Header("Controles")]
    public KeyCode grabKey = KeyCode.E;    // tomar/soltar
    public KeyCode dropKey = KeyCode.Q;    // soltar (sin lanzar)
    public float throwForce = 8f;          // lanzar con click izquierdo

    [Header("Colisiones del jugador (opcional)")]
    public Collider[] ignoreWithPlayer;    // añade tus colliders del player

    private Rigidbody held;
    private float prevDrag, prevAngDrag;
    private bool prevUseGravity;
    private CollisionDetectionMode prevCD;

    private readonly List<Collider> heldCols = new();
    private readonly List<bool> heldColsPrevTrigger = new();

    void Awake()
    {
        if (!cam) cam = Camera.main;
        if (!cam) Debug.LogWarning("[ObjectGrabber] No hay Camera asignada ni Camera.main.");

        if (!holdPoint && cam)
        {
            var go = new GameObject("HoldPoint");
            holdPoint = go.transform;
            holdPoint.SetParent(cam.transform, false);
            holdPoint.localPosition = new Vector3(0, 0, holdDistance);
        }
    }

    void Update()
    {
        // E: tomar/soltar
        if (Input.GetKeyDown(grabKey))
        {
            if (held) Drop(false);
            else TryPickup();
        }

        // Click izq: lanzar
        if (held && Input.GetMouseButtonDown(0)) Drop(true);
        // Q: soltar sin lanzar
        if (held && Input.GetKeyDown(dropKey))   Drop(false);
    }

    void FixedUpdate()
    {
        if (!held || !cam) return;
        if (held.isKinematic) { Drop(false); return; }

        Vector3 target = holdPoint ? holdPoint.position
                                   : cam.transform.position + cam.transform.forward * holdDistance;

        // Llevar suavemente al punto
        Vector3 toTarget   = target - held.position;
        Vector3 desiredVel = toTarget * pullStrength;
        held.linearVelocity = Vector3.ClampMagnitude(desiredVel, maxSpeed);  // <-- velocity (no linearVelocity)

        // Alinear rotación con la cámara
        Quaternion targetRot = Quaternion.Slerp(
            held.rotation,
            cam.transform.rotation,
            rotateSpeed * Time.fixedDeltaTime
        );
        held.MoveRotation(targetRot);
    }

    void TryPickup()
    {
        if (!cam) return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, pickupMask, QueryTriggerInteraction.Collide))
        {
            var rb = hit.rigidbody;
            if (!rb) return;

            // Solo objetos "Draggable" y que no excedan la masa
            if (!hit.collider.CompareTag("Draggable")) return;
            if (rb.mass > maxMass) return;

            BeginHold(rb);
        }
    }

    void BeginHold(Rigidbody rb)
    {
        // Guardar estado
        prevDrag       = rb.linearDamping;           // <-- drag (no linearDamping)
        prevAngDrag    = rb.angularDamping;    // <-- angularDrag (no angularDamping)
        prevUseGravity = rb.useGravity;
        prevCD         = rb.collisionDetectionMode;

        // Ajustes para sujetar
        rb.useGravity = false;
        rb.linearDamping       = 10f;
        rb.angularDamping = 10f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.isKinematic = false;

        // Forzar capa Item para que el mask funcione siempre
        int itemLayer = LayerMask.NameToLayer("Item");
        if (itemLayer != -1) rb.gameObject.layer = itemLayer;

        ToggleIgnoreWithPlayer(rb, true);

        // Evitar enganches al sostener (temporalmente Trigger)
        heldCols.Clear(); heldColsPrevTrigger.Clear();
        rb.GetComponentsInChildren(true, heldCols);
        for (int i = 0; i < heldCols.Count; i++)
        {
            var c = heldCols[i];
            heldColsPrevTrigger.Add(c.isTrigger);
            c.isTrigger = true;
        }

        held = rb;
    }

    void Drop(bool throwIt)
    {
        if (!held) return;

        var rb = held;

        // Restaurar triggers
        for (int i = 0; i < heldCols.Count; i++)
        {
            var c = heldCols[i];
            if (c) c.isTrigger = heldColsPrevTrigger[i];
        }
        heldCols.Clear(); heldColsPrevTrigger.Clear();

        // Restaurar estado
        rb.useGravity = prevUseGravity;
        rb.linearDamping       = prevDrag;
        rb.angularDamping= prevAngDrag;
        rb.collisionDetectionMode = prevCD;

        // Lanzar si corresponde
        if (throwIt && cam)
        {
            rb.linearVelocity = Vector3.zero;
            rb.AddForce(cam.transform.forward * throwForce, ForceMode.VelocityChange);
        }

        ToggleIgnoreWithPlayer(rb, false);

        held = null;
    }

    void ToggleIgnoreWithPlayer(Rigidbody rb, bool ignore)
    {
        if (ignoreWithPlayer == null || ignoreWithPlayer.Length == 0) return;

        var cols = rb.GetComponentsInChildren<Collider>(true);
        foreach (var col in cols)
            foreach (var playerCol in ignoreWithPlayer)
                if (playerCol) Physics.IgnoreCollision(col, playerCol, ignore);
    }

    void OnDisable()
    {
        if (held) Drop(false);
    }

    public bool IsHolding() => held != null;
    public void ForceRelease() { if (held) Drop(false); }

    void OnDrawGizmosSelected()
    {
        if (!cam) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(cam.transform.position, cam.transform.forward * interactDistance);
    }
}
