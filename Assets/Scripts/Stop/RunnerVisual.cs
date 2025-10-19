using UnityEngine;

public class RunnerVisual : MonoBehaviour
{
    public Animator animator;       // arrastra aquí el Animator correcto (el que tiene ChickenPlayer)
    public Rigidbody runnerRb;      // Rigidbody del Runner
    public Transform forwardRef;

    public string speedParam = "Speed";
    public float speedSmoothing = 8f;
    public float rotateSmoothing = 10f;
    public float minLookSpeed = 0.1f;
    public float expectedMaxSpeed = 4f;

    float _animSpeed;

    void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        if (!runnerRb) runnerRb = GetComponentInParent<Rigidbody>();
        if (!forwardRef && runnerRb) forwardRef = runnerRb.transform;
    }

    void Update()
    {
        if (!runnerRb || !animator || animator.runtimeAnimatorController == null) return;

        Vector3 v = runnerRb.linearVelocity; v.y = 0f;
        float spd = v.magnitude;
        float norm = expectedMaxSpeed > 0 ? Mathf.Clamp01(spd / expectedMaxSpeed) : 0f;

        _animSpeed = Mathf.Lerp(_animSpeed, norm, 1f - Mathf.Exp(-speedSmoothing * Time.deltaTime));
        animator.SetFloat(speedParam, _animSpeed);

        Vector3 lookDir = v.sqrMagnitude > (minLookSpeed * minLookSpeed)
            ? v.normalized
            : (forwardRef ? forwardRef.forward : transform.forward);
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 1e-4f)
        {
            Quaternion target = Quaternion.LookRotation(lookDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-rotateSmoothing * Time.deltaTime));
        }
    }
}
