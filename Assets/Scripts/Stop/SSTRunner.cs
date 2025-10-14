using UnityEngine;

public class SSTRunner : MonoBehaviour
{
    public Rigidbody rb;
    public Transform forwardRef;      // dirección de avance (cámara o este transform)
    public float maxSpeed = 4f;       // velocidad tope al mantener Space
    public float accel = 10f;         // aceleración al empezar a moverse
    public float decel = 12f;         // desaceleración al soltar Space
    public KeyCode moveKey = KeyCode.Space;

    [HideInInspector] public bool allowControl = true;

    bool _goGate = false;             // lo abre/cierra el Manager
    float _horizSpeed = 0f;

    void Reset()
    {
        rb = GetComponent<Rigidbody>();
        if (!forwardRef) forwardRef = transform;
    }

    void Awake()
    {
        if (rb != null)
        {
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // Objetivo de velocidad: Space + gate abierto
        float target = (allowControl && _goGate && Input.GetKey(moveKey)) ? maxSpeed : 0f;
        float a = (target > _horizSpeed) ? accel : decel;
        _horizSpeed = Mathf.MoveTowards(_horizSpeed, target, a * Time.fixedDeltaTime);

        // Dirección horizontal
        Vector3 fwd = forwardRef ? forwardRef.forward : transform.forward;
        fwd.y = 0f; fwd.Normalize();
        Vector3 horizVel = fwd * _horizSpeed;

        // Conserva la componente Y que viene de la física (gravedad/saltos si los hubiese)
        Vector3 v = rb.linearVelocity;
        v.x = horizVel.x;
        v.z = horizVel.z;
        rb.linearVelocity = v;
    }

    public float CurrentSpeed()
    {
        Vector3 v = rb.linearVelocity; v.y = 0f;
        return v.magnitude;
    }

    public void setGoGate(bool open) { _goGate = open; }
}
