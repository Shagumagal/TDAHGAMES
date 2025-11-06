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

    Rigidbody held;

    void Awake()
    {
        if (!playerCam) playerCam = Camera.main;
    }

    void Update()
    {
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
        held.linearDamping = 0f;
        held = null;
    }

    public Rigidbody GetHeld() => held;
}
