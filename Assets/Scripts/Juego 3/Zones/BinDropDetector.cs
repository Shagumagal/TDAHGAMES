using UnityEngine;

[RequireComponent(typeof(BinZone))]
public class BinDropDetector : MonoBehaviour
{
    public SortTaskManager manager;
    BinZone bin;

    void Awake()
    {
        bin = GetComponent<BinZone>();
        if (manager == null) manager = FindObjectOfType<SortTaskManager>();
    }

    void OnTriggerStay(Collider other)
    {
        var item = other.GetComponentInParent<GrabbableItem>();
        if (item == null || item.isPlaced) return;

        // Heurística de “drop” (quieto y cerca)
        var rb = item.GetComponent<Rigidbody>();
        bool nearlyStopped = rb == null || (rb.velocity.sqrMagnitude < 0.02f && rb.angularVelocity.sqrMagnitude < 0.02f);

        if (nearlyStopped)
        {
            manager.TryPlaceInBin(item, bin);
        }
    }
}
