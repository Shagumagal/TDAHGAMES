using UnityEngine;

public class SSTRunner : MonoBehaviour
{
    public Rigidbody rb;
    public Transform forwardRef;
    public float maxSpeed = 4f;
    public float accel = 10f;
    public float decel = 12f;
    public KeyCode moveKey = KeyCode.Space;

    [HideInInspector] public bool allowControl = true;
    float _horizSpeed = 0f;

    void Reset()
    {
        rb = GetComponent<Rigidbody>();
        if (!forwardRef) forwardRef = transform;
    }

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    void FixedUpdate()
    {
        if (!rb) return;

        float target = (allowControl && Input.GetKey(moveKey)) ? maxSpeed : 0f;
        float a = (target > _horizSpeed) ? accel : decel;
        _horizSpeed = Mathf.MoveTowards(_horizSpeed, target, a * Time.fixedDeltaTime);

        Vector3 fwd = forwardRef ? forwardRef.forward : transform.forward;
        fwd.y = 0f; fwd.Normalize();
        Vector3 horizVel = fwd * _horizSpeed;

        Vector3 v = rb.linearVelocity; // conservar Y (gravedad)
        v.x = horizVel.x;
        v.z = horizVel.z;
        rb.linearVelocity = v;

        // (Opcional) rotar suavemente el Runner hacia su forward
        // Quaternion targetRot = Quaternion.LookRotation(fwd, Vector3.up);
        // rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, 0.15f));
    }

    public float CurrentSpeed()
    {
        Vector3 v = rb.linearVelocity; v.y = 0f;
        return v.magnitude;
    }
}
