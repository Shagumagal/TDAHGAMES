using UnityEngine;

public class ObjectGrabber : MonoBehaviour
{
  // Referencias
  public Camera cam;                   // Si está null, usa Camera.main
  public Transform holdPoint;          // Si está null, se crea uno en runtime (frente a la cámara)

  // Ajustes
  public LayerMask pickupMask = ~0;    // Capas agarrables
  public float interactDistance = 3f;  // Distancia para agarrar con la tecla
  public float holdDistance = 2f;      // Distancia si no hay holdPoint
  public float pullStrength = 40f;     // Fuerza para “jalar” hacia el punto
  public float maxSpeed = 15f;         // Límite de velocidad al sostener
  public float rotateSpeed = 8f;       // Qué tan rápido alinea rotación al mirar
  public float maxMass = 15f;          // Masa máxima agarrable

  // Controles
  public KeyCode grabKey = KeyCode.E;  // Agarrar/Soltar
  public KeyCode dropKey = KeyCode.Q;  // Soltar sin lanzar
  public float throwForce = 8f;        // Click izquierdo para lanzar

  // Colisiones a ignorar (opcional: agrega aquí colliders del player)
  public Collider[] ignoreWithPlayer;

  // Estado
  private Rigidbody held;
  private float prevDrag, prevAngDrag;
  private bool prevUseGravity;
  private CollisionDetectionMode prevCD;

  void Awake()
  {
    if (!cam) cam = Camera.main;
    if (!cam)
    {
      Debug.LogWarning("[ObjectGrabber] No hay Camera asignada ni Camera.main en escena.");
    }
    if (!holdPoint && cam)
    {
      var go = new GameObject("HoldPoint");
      holdPoint = go.transform;
      holdPoint.SetParent(cam.transform, false);
      holdPoint.localPosition = new Vector3(0, 0, holdDistance);
    }
  }

  void Update()
  {
    if (Input.GetKeyDown(grabKey))
    {
      if (held) Drop(false);
      else TryPickup();
    }

    if (held && Input.GetMouseButtonDown(0)) // Lanzar con click izquierdo
      Drop(true);

    if (held && Input.GetKeyDown(dropKey))   // Soltar sin lanzar
      Drop(false);
  }

  void FixedUpdate()
  {
    if (!held || !cam) return;

    Vector3 target = holdPoint ? holdPoint.position
                               : cam.transform.position + cam.transform.forward * holdDistance;

    // Mover con física (suave y estable)
    Vector3 toTarget = target - held.position;
    Vector3 desiredVel = toTarget * pullStrength;
    held.velocity = Vector3.ClampMagnitude(desiredVel, maxSpeed);

    // Alinear rotación al mirar
    Quaternion targetRot = Quaternion.Slerp(
      held.rotation,
      cam.transform.rotation,
      rotateSpeed * Time.fixedDeltaTime
    );
    held.MoveRotation(targetRot);
  }

  void TryPickup()
  {
    if (!cam) return;

    Ray ray = new Ray(cam.transform.position, cam.transform.forward);
    if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, pickupMask, QueryTriggerInteraction.Ignore))
    {
      var rb = hit.rigidbody;
      if (rb && rb.mass <= maxMass)
      {
        CachePhysics(rb, true);
        ToggleIgnoreWithPlayer(rb, true);
        held = rb;
      }
    }
  }

  void Drop(bool throwIt)
  {
    if (!held) return;

    if (throwIt && cam)
    {
      held.velocity = Vector3.zero;
      held.AddForce(cam.transform.forward * throwForce, ForceMode.VelocityChange);
    }

    CachePhysics(held, false);
    ToggleIgnoreWithPlayer(held, false);
    held = null;
  }

  void CachePhysics(Rigidbody rb, bool grabbing)
  {
    if (grabbing)
    {
      prevDrag = rb.drag;
      prevAngDrag = rb.angularDrag;
      prevUseGravity = rb.useGravity;
      prevCD = rb.collisionDetectionMode;

      rb.useGravity = false;
      rb.drag = 10f;
      rb.angularDrag = 10f;
      rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }
    else
    {
      rb.useGravity = prevUseGravity;
      rb.drag = prevDrag;
      rb.angularDrag = prevAngDrag;
      rb.collisionDetectionMode = prevCD;
    }
  }

  void ToggleIgnoreWithPlayer(Rigidbody rb, bool ignore)
  {
    if (ignoreWithPlayer == null || ignoreWithPlayer.Length == 0) return;

    var cols = rb.GetComponentsInChildren<Collider>(true);
    foreach (var col in cols)
    {
      foreach (var playerCol in ignoreWithPlayer)
      {
        if (playerCol) Physics.IgnoreCollision(col, playerCol, ignore);
      }
    }
  }

  void OnDisable()
  {
    if (held) Drop(false);
  }

  // Útil para otros scripts
  public bool IsHolding() => held != null;

  void OnDrawGizmosSelected()
  {
    if (!cam) return;
    Gizmos.color = Color.yellow;
    Gizmos.DrawRay(cam.transform.position, cam.transform.forward * interactDistance);
  }
}
