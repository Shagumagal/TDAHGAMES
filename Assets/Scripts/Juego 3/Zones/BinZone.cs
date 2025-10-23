using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BinZone : MonoBehaviour
{
    public string binId = "Herr";    // Qué categoría acepta
    public int capacity = 99;        // Límite de piezas esperadas
    public Transform snapArea;       // Punto base para apilar
    public float gridStep = 0.25f;   // Separación entre ítems al apilar
    public int perRow = 4;           // Ítems por fila

    void Reset()
    {
        var c = GetComponent<Collider>();
        c.isTrigger = true;
        if (snapArea == null)
        {
            GameObject p = new GameObject("SnapArea");
            p.transform.SetParent(transform);
            p.transform.localPosition = Vector3.zero;
            snapArea = p.transform;
        }
    }

    public Vector3 GetSnapPosition(int index)
    {
        int row = index / perRow;
        int col = index % perRow;
        return snapArea.position + new Vector3(col * gridStep, 0f, row * gridStep);
    }
}
