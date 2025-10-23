using UnityEngine;
using System.Collections.Generic;

public class ObjectGrabber : MonoBehaviour
{
    [Header("Referencias")]
    public Camera cam;                   // Si está null -> usa Camera.main
    public Transform holdPoint;          // Si está null -> se crea frente a la cámara

    [Header("Selección / Alcance")]
    public LayerMask pickupMask = ~0;    // Capas agarrables (excluye la zona)
    public float interactDistance = 6f;  // Distancia del raycast
    public float holdDistance = 2.2f;    // Distancia del punto de sujeción

    [Header("Movimiento al sostener")]
    public float pullStrength = 40f;     // Qué tan fuerte atrae al holdPoint
    public float maxSpeed = 15f;         // Velocidad máxima al sostener
    public float rotateSpeed = 8f;       // Alineación de rotación con la cámara
    public float maxMass = 15f;          // Masa máxima agarrable

    [Header("Controles")]
    public KeyCode grabKey = KeyCode.E;  // Agarrar / soltar
    public KeyCode dropKey = KeyCode.Q;  // Soltar sin lanzar
    public float throwForce = 8f;        // Click izquierdo para lanzar

    [Header("Colisiones del jugador (opcional)")]
    public Collider[] ignoreWithPlayer;  // Colliders a ignorar mientras sostengo

    // --- Estado ---
    private Rigidbody held;
    private float prevDrag, prevAngDrag;
    private bool prevUseGravity;
    private CollisionDetectionMode prevCD;

    // Guardamos y restauramos el estado de isTrigger de TODOS los colliders del objeto
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
        if (Input.GetKeyDown(grabKey))
        {
            if (held) Drop(false);
            else TryPickup();
        }

        if (held && Input.GetMouseButtonDown(0)) // lanzar con click izq
            Drop(true);

        if (held && Input.GetKeyDown(dropKey))   // soltar sin lanzar
            Drop(false);
    }

    void FixedUpdate()
    {
        if (!held || !cam) return;

        Vector3 target = holdPoint ? holdPoint.position
                                   : cam.transform.position + cam.transform.forward * holdDistance;

        Vector3 toTarget = target - held.position;
        Vector3 desiredVel = toTarget * pullStrength;
        held.linearVelocity = Vector3.ClampMagnitude(desiredVel, maxSpeed);

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
        // Importante: usamos Collide para “ver” colliders con isTrigger=true
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, pickupMask, QueryTriggerInteraction.Collide))
        {
            var rb = hit.rigidbody;
            if (rb && rb.mass <= maxMass)
            {
                BeginHold(rb);
            }
        }
    }

    void BeginHold(Rigidbody rb)
    {
        // cache fisicas
        prevDrag = rb.linearDamping;
        prevAngDrag = rb.angularDamping;
        prevUseGravity = rb.useGravity;
        prevCD = rb.collisionDetectionMode;

        rb.useGravity = false;
        rb.linearDamping = 10f;
        rb.angularDamping = 10f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.isKinematic = false; // podemos moverlo por velocidad sin físicas raras

        // Ignorar colisiones con el jugador
        ToggleIgnoreWithPlayer(rb, true);

        // Poner TODOS los colliders del objeto en trigger para que no se atasque
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

        if (throwIt && cam)
        {
            held.linearVelocity = Vector3.zero;
            held.AddForce(cam.transform.forward * throwForce, ForceMode.VelocityChange);
        }

        // Restaurar triggers originales
        for (int i = 0; i < heldCols.Count; i++)
        {
            var c = heldCols[i];
            if (c) c.isTrigger = heldColsPrevTrigger[i];
        }
        heldCols.Clear(); heldColsPrevTrigger.Clear();

        // Restaurar físicas
        held.useGravity = prevUseGravity;
        held.linearDamping = prevDrag;
        held.angularDamping = prevAngDrag;
        held.collisionDetectionMode = prevCD;

        ToggleIgnoreWithPlayer(held, false);
        held = null;
    }

    void ToggleIgnoreWithPlayer(Rigidbody rb, bool ignore)
    {
        if (ignoreWithPlayer == null || ignoreWithPlayer.Length == 0) return;

        var cols = rb.GetComponentsInChildren<Collider>(true);
        foreach (var col in cols)
        {
            foreach (var playerCol in ignoreWithPlayer)
            {
                if (playerCol) Physics.IgnoreCollision(col, playerCol, ignore);
            }
        }
    }

    void OnDisable()
    {
        if (held) Drop(false);
    }

    // === API pública útil para el Manager/Fases ===
    public bool IsHolding() => held != null;
    public void ForceRelease() { if (held) Drop(false); }

    void OnDrawGizmosSelected()
    {
        if (!cam) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(cam.transform.position, cam.transform.forward * interactDistance);
    }
}
