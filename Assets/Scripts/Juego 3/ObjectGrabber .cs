using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ObjectGrabber : MonoBehaviour
{
    [Header("Referencias")]
    public Camera cam;
    public Transform holdPoint;

    [Header("Selección / Alcance")]
    public LayerMask pickupMask = ~0;
    public float interactDistance = 6f;
    public float holdDistance = 2.2f;

    [Header("Movimiento al sostener")]
    public float pullStrength = 40f;
    public float maxSpeed = 15f;
    public float rotateSpeed = 8f;
    public float maxMass = 15f;

    [Header("Controles")]
    public KeyCode grabKey = KeyCode.E;
    public KeyCode dropKey = KeyCode.Q;
    public float throwForce = 8f;

    [Header("Colisiones del jugador (opcional)")]
    public Collider[] ignoreWithPlayer;

    [Header("Integración Clasificador")]
    public DropZoneClassifier classifier;
    public bool autoDeliverOnDrop = true;
    public float deliverDelayOnThrow = 0.06f;

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
        if (Input.GetKeyDown(grabKey))
        {
            if (held) Drop(false);
            else TryPickup();
        }

        if (held && Input.GetMouseButtonDown(0)) Drop(true);
        if (held && Input.GetKeyDown(dropKey))   Drop(false);
    }

    void FixedUpdate()
    {
        if (!held || !cam) return;
        if (held.isKinematic) { Drop(false); return; }

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
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, pickupMask, QueryTriggerInteraction.Collide))
        {
            var rb = hit.rigidbody;
            if (rb && rb.mass <= maxMass) BeginHold(rb);
        }
    }

    void BeginHold(Rigidbody rb)
    {
        prevDrag = rb.linearDamping;
        prevAngDrag = rb.angularDamping;
        prevUseGravity = rb.useGravity;
        prevCD = rb.collisionDetectionMode;

        rb.useGravity = false;
        rb.linearDamping = 10f;
        rb.angularDamping = 10f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.isKinematic = false;

        ToggleIgnoreWithPlayer(rb, true);

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

        if (throwIt && cam)
        {
            rb.linearVelocity = Vector3.zero;
            rb.AddForce(cam.transform.forward * throwForce, ForceMode.VelocityChange);
        }

        for (int i = 0; i < heldCols.Count; i++)
        {
            var c = heldCols[i];
            if (c) c.isTrigger = heldColsPrevTrigger[i];
        }
        heldCols.Clear(); heldColsPrevTrigger.Clear();

        rb.useGravity = prevUseGravity;
        rb.linearDamping = prevDrag;
        rb.angularDamping = prevAngDrag;
        rb.collisionDetectionMode = prevCD;

        ToggleIgnoreWithPlayer(rb, false);

        if (autoDeliverOnDrop && classifier)
        {
            if (throwIt)
                StartCoroutine(DeliverAfter(rb.gameObject, deliverDelayOnThrow));
            else
                classifier.TryExternalDrop(rb.gameObject);
        }

        held = null;
    }

    IEnumerator DeliverAfter(GameObject go, float delay)
    {
        yield return new WaitForFixedUpdate();
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (classifier) classifier.TryExternalDrop(go);
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
