using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Ball : MonoBehaviour
{
    public int id;                // 0,1,2
    public Peg CurrentPeg { get; set; }
    [HideInInspector] public bool dragging;

    private Vector3 grabOffset;

    void OnMouseDown()
    {
        if (CurrentPeg && CurrentPeg.Top != this) return; // sólo la de arriba
        dragging = true;
        grabOffset = transform.position - GetMouseWorld();
    }

    void OnMouseDrag()
    {
        if (!dragging) return;
        transform.position = GetMouseWorld() + grabOffset;
    }

    void OnMouseUp()
    {
        if (!dragging) return;
        dragging = false;

        // Buscar Peg más cercano bajo el cursor
        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 100f))
        {
            var peg = hit.collider.GetComponent<Peg>();
            if (peg && peg.CanPlace)
            {
                // mover legal
                CurrentPeg?.Pop(); // quitar de su poste actual
                peg.Push(this);
                ToLGame.Instance.RegisterMove();
                ToLGame.Instance.CheckSolved();
                return;
            }
        }

        // si no fue legal, volver a su poste original (re-snap)
        var back = CurrentPeg;
        if (back)
        {
            back.Pop(); // quitar temporal para re-posicionar bien
            back.Push(this);
        }
    }

    Vector3 GetMouseWorld()
    {
        var plane = new Plane(Vector3.up, Vector3.zero);
        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        plane.Raycast(ray, out float dist);
        return ray.GetPoint(dist);
    }
}
