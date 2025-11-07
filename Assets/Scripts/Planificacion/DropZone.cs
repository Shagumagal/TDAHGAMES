using System;
using System.Collections.Generic;
using UnityEngine;

public class DropZone : MonoBehaviour
{
    [Tooltip("IDs aceptados (de ItemTag.itemId)")]
    public List<string> acceptedIds = new() { "gallina","gato","caballo" };

    public bool acceptOnlyTagDraggable = true;
    public bool consumeOnAccept = true;     // si acierta, desaparece
    public float rejectPushForce = 4f;      // empujón suave al rechazar

    public event Action<string> OnItemAccepted;
    public event Action<string> OnItemRejected;

    // Para no contar múltiples veces mientras permanece dentro
    private readonly HashSet<int> inside = new();

    void OnTriggerEnter(Collider other) => Handle(other);

    void OnTriggerExit(Collider other)
    {
        var root = other.attachedRigidbody ? other.attachedRigidbody.gameObject : other.gameObject;
        inside.Remove(root.GetInstanceID());
    }

    void Handle(Collider other)
    {
        if (acceptOnlyTagDraggable && !other.CompareTag("Draggable")) return;

        var root = other.attachedRigidbody ? other.attachedRigidbody.gameObject : other.gameObject;
        int iid = root.GetInstanceID();
        if (inside.Contains(iid)) return;         // ya contado mientras está dentro
        inside.Add(iid);

        var tag = root.GetComponent<ItemTag>();
        if (!tag || string.IsNullOrWhiteSpace(tag.itemId))
        {
            OnItemRejected?.Invoke(null);
            return;
        }

        string id = tag.itemId.Trim().ToLowerInvariant();
        bool ok = acceptedIds.Contains(id);

        if (ok)
        {
            OnItemAccepted?.Invoke(id);
            if (consumeOnAccept)
            {
                inside.Remove(iid);
                Destroy(root);                    // desaparece al acertar
            }
        }
        else
        {
            OnItemRejected?.Invoke(id);
            var rb = root.GetComponent<Rigidbody>();
            if (rb)
            {
                Vector3 dir = (root.transform.position - transform.position).normalized;
                rb.AddForce(dir * rejectPushForce, ForceMode.VelocityChange); // lo “escupe”
            }
        }
    }
}
