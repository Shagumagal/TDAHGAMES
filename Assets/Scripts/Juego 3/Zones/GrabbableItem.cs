using UnityEngine;

[DisallowMultipleComponent]
public class GrabbableItem : MonoBehaviour
{
    [Header("Identificadores")]
    public string itemId = "Pala";   // nombre lógico del ítem
    public string binId  = "Herr";   // categoría/contendor válido (p.ej., "Herr","Sem","Agua")

    [Header("Estado")]
    public bool isPlaced = false;    // se marca true cuando queda colocado

    // (Opcional) cachés
    [HideInInspector] public Rigidbody rb;
    [HideInInspector] public Collider col;

    void Reset()
    {
        // Asegura que tenga Rigidbody/Collider
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        col = GetComponent<Collider>();
        if (col == null)
        {
            // Intenta añadir un BoxCollider por defecto si no existe ninguno
            col = gameObject.AddComponent<BoxCollider>();
        }
    }

    void Awake()
    {
        if (!rb)  rb  = GetComponent<Rigidbody>();
        if (!col) col = GetComponent<Collider>();
    }
}
