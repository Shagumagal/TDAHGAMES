using UnityEngine;

public class StopCueLookAt : MonoBehaviour
{
    [Header("Refs")]
    public Transform target;         // La gallina-jugadora (Chicken_001 o Runner)
    public Transform pivot;          // Eje que rota (si es null, usa transform)

    [Header("Rotación")]
    public float turnSpeedDeg = 360f; // grados/seg
    public bool startFacingAway = true;

    [Header("Offsets")]
    public float yOnly = 1f;          // 1 = rota solo en Y
    public float faceAwayYaw = 180f;  // cuánto “de espaldas” en verde

    bool _isStop;                     // true = ROJO (mirar al jugador)
    Quaternion _goal;

    void Reset() { pivot = transform; }

    void Awake()
    {
        if (!pivot) pivot = transform;
        if (target == null)
        {
            // intenta hallar al Runner en la escena
            var runner = GameObject.Find("Runner");
            if (runner) target = runner.transform;
        }
        // posture inicial (verde): de espaldas
        SetStop(false, instant:true);
    }

    void Update()
    {
        // interpola hacia _goal
        pivot.rotation = Quaternion.RotateTowards(
            pivot.rotation, _goal, turnSpeedDeg * Time.deltaTime
        );
    }

    public void SetStop(bool isStop)              { SetStop(isStop, instant:false); }
    public void SetGo()                           { SetStop(false, instant:false); }
    public void SetInstantStop(bool isStop)       { SetStop(isStop, instant:true); }

    void SetStop(bool isStop, bool instant)
    {
        _isStop = isStop;
        _goal = ComputeGoal(isStop);
        if (instant) pivot.rotation = _goal;
        // aquí puedes disparar audio/emotes si quieres
        // if (isStop) audioSource.PlayOneShot(stopClip);
    }

    Quaternion ComputeGoal(bool isStop)
    {
        if (target == null) return pivot.rotation;

        // mira al target solo en Y si yOnly=1
        Vector3 dir = (target.position - pivot.position);
        if (yOnly >= 1f) { dir.y = 0f; }
        if (dir.sqrMagnitude < 1e-4f) dir = pivot.forward;

        var look = Quaternion.LookRotation(dir.normalized, Vector3.up);
        if (!isStop) // en VERDE: de espaldas
            look = look * Quaternion.Euler(0f, faceAwayYaw, 0f);

        return look;
    }
}
