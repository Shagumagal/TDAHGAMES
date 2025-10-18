using UnityEngine;

public class SimpleFollowCam : MonoBehaviour
{
    public Transform target;      // Runner
    public Vector3 offset = new Vector3(0f, 3.5f, -6f);
    public float followLerp = 8f;
    public float lookLerp = 12f;

    void LateUpdate()
    {
        if (!target) return;
        Vector3 desired = target.position + target.TransformDirection(offset);
        transform.position = Vector3.Lerp(transform.position, desired, followLerp * Time.deltaTime);
        Quaternion look = Quaternion.LookRotation((target.position - transform.position).normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, lookLerp * Time.deltaTime);
    }
}
