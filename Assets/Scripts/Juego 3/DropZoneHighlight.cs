using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DropZoneHighlight : MonoBehaviour
{
    public Renderer visual;          // arrastra el material del canasto (opcional)
    public Color highlight = new Color(0,1,0,0.25f);
    private Color original;

    void Start(){
        if (visual == null) visual = GetComponentInChildren<Renderer>();
        if (visual != null) original = visual.material.color;
        GetComponent<Collider>().isTrigger = true; // por si acaso
    }

    void OnTriggerEnter(Collider other){
        if (visual && IsTarget(other.name)) visual.material.color = highlight;
    }
    void OnTriggerExit(Collider other){
        if (visual && IsTarget(other.name)) visual.material.color = original;
    }

    bool IsTarget(string n) => n=="Pala" || n=="Regadera" || n=="Hoz";
}
