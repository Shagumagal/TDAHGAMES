// HoldPointHelper.cs
using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways]
public class HoldPointHelper : MonoBehaviour
{
    [Header("Refs")]
    public Camera playerCam;
    public PickUp pickup;

    [Header("Posición relativa (XY)")]
    public Vector3 localOffset = new Vector3(0f, -0.10f, 0f); // X/Y fijos; Z lo controla PickUp

    [Header("Seguimiento")]
    public bool stickToCamera = true; // si está activo, pega X/Y y rotación a la cámara (Z se respeta)

    [Header("Debug")]
    public bool debugGizmo = true;       // esfera y flecha en la Scene
    public bool debugBallInGame = false; // bolita visible en Game (sin collider)

    private Transform hold;

    void OnValidate()
    {
        if (!playerCam) playerCam = Camera.main;
        if (!pickup)    pickup    = FindObjectOfType<PickUp>();
    }

    void Awake()  { Setup(); }
    void Start()  { Setup(); }

    void Setup()
    {
        if (!playerCam) playerCam = Camera.main;

        // Si el PickUp no tiene holdPoint, lo creamos como hijo de la cámara.
        if (pickup)
        {
            if (pickup.holdPoint == null)
            {
                var go = new GameObject("HoldPoint");
                hold = go.transform;
                hold.SetParent(playerCam ? playerCam.transform : transform, false);
                // Z inicial cualquiera (PickUp lo sobrescribirá con su scroll)
                hold.localPosition = new Vector3(localOffset.x, localOffset.y, 2.0f);
                hold.localRotation = Quaternion.identity;
                pickup.holdPoint = hold;
            }
            else
            {
                hold = pickup.holdPoint;
                // Garantiza que sea hijo de la cámara para seguirla
                if (playerCam && hold.parent != playerCam.transform)
                    hold.SetParent(playerCam.transform, true);
            }
        }

        // Bolita opcional visible en Game
        if (debugBallInGame && hold && !hold.GetComponentInChildren<MeshRenderer>())
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "HoldPointDebugBall";
            s.transform.SetParent(hold, false);
            s.transform.localPosition = Vector3.zero;
            s.transform.localScale = Vector3.one * 0.05f;
            var col = s.GetComponent<Collider>(); if (Application.isEditor && col) DestroyImmediate(col);
        }
    }

    void LateUpdate()
    {
        if (!hold || !playerCam) return;

        if (stickToCamera)
        {
            // 🔧 Mantén X/Y pegados a la cámara, pero RESPETA el Z actual (lo ajusta PickUp con la rueda).
            float currentZ = hold.localPosition.z;
            hold.localPosition = new Vector3(localOffset.x, localOffset.y, currentZ);
            hold.rotation = playerCam.transform.rotation;

            // Asegura el parent correcto (por si algo lo cambió)
            if (hold.parent != playerCam.transform)
                hold.SetParent(playerCam.transform, true);
        }
    }

    void OnDrawGizmos()
    {
        if (!debugGizmo || hold == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.matrix = hold.localToWorldMatrix;
        Gizmos.DrawWireSphere(Vector3.zero, 0.05f);
        Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.25f);
    }

    // Utilidad por si quieres resetear el Z a un valor por defecto desde el inspector
    [ContextMenu("Reset Z to 2.0")]
    void ResetZ()
    {
        if (!hold) return;
        hold.localPosition = new Vector3(hold.localPosition.x, hold.localPosition.y, 2.0f);
    }
}
