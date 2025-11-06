using UnityEngine;

public class SimpleFollowCam : MonoBehaviour
{
    public Transform target;      // Runner
    public Vector3 offset = new Vector3(0f, 3.5f, -6f);
    public float followLerp = 8f;
    public float lookLerp = 12f;

    // 👉 Altura del punto de mira sobre el pivot del jugador (pecho/cara)
    public float lookHeight = 1.3f;

    void LateUpdate()
    {
        if (!target) return;

        Vector3 desired = target.position + target.TransformDirection(offset);
        transform.position = Vector3.Lerp(transform.position, desired, followLerp * Time.deltaTime);

        // 🔧 Apunta al centro: pecho/cara (sube el objetivo)
        Vector3 aim = target.position + Vector3.up * lookHeight;
        Quaternion look = Quaternion.LookRotation((aim - transform.position).normalized, Vector3.up);

        transform.rotation = Quaternion.Slerp(transform.rotation, look, lookLerp * Time.deltaTime);
    }
}
