using UnityEngine;
using System;

[RequireComponent(typeof(Collider))]
public class DropZone : MonoBehaviour
{
    [Tooltip("IDs aceptados por esta zona (ej.: Caja: pala, regadera, hoz; CorralGallinas: gallina).")]
    public string[] acceptedIds;

    public Action<string> OnItemAccepted; // itemId
    public Action<string> OnItemRejected; // itemId

    void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        var tag = other.GetComponent<ItemTag>();
        var rb  = other.attachedRigidbody;
        if (!tag || rb == null) return;

        bool ok = Array.Exists(acceptedIds, id => id == tag.itemId);
        if (ok) OnItemAccepted?.Invoke(tag.itemId);
        else    OnItemRejected?.Invoke(tag.itemId);
    }
}
