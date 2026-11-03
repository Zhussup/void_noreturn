using UnityEngine;

/// <summary>
/// Прицел + клавиша E. Луч из центра камеры находит ближайший Interactable;
/// пока он доступен, у прицела видна подпись действия, а E его активирует.
/// </summary>
public class InteractionSystem : MonoBehaviour
{
    NightDirector dir;
    Camera cam;

    readonly RaycastHit[] hits = new RaycastHit[12];
    Interactable target;

    public bool HasTarget => target != null && target.IsAvailable;
    public string Label => HasTarget ? target.Label : "";

    public void Init(NightDirector director, Camera camera)
    {
        dir = director;
        cam = camera;
    }

    public void Tick()
    {
        if (dir == null || cam == null) { Clear(); return; }

        bool allowed = dir.Running &&
                       (dir.Modal == ModalKind.None || dir.Modal == ModalKind.Call);
        if (!allowed) { Clear(); return; }

        target = FindTarget();
        if (!HasTarget) return;

        if (Input.GetKeyDown(KeyCode.E))
            target.Activate();
    }

    public void Clear()
    {
        target = null;
    }

    // ------------------------------------------------------------ поиск цели

    Interactable FindTarget()
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        int count = Physics.RaycastNonAlloc(ray, hits, 3.0f, ~0, QueryTriggerInteraction.Collide);

        int nearest = -1;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider col = hits[i].collider;
            if (col == null) continue;
            if (col.GetComponentInParent<PlayerController>() != null) continue;
            if (hits[i].distance < nearestDistance)
            {
                nearestDistance = hits[i].distance;
                nearest = i;
            }
        }

        if (nearest < 0) return null;

        Interactable found = hits[nearest].collider.GetComponentInParent<Interactable>();
        if (found == null || !found.IsAvailable) return null;
        if (nearestDistance > found.maxDistance) return null;
        return found;
    }
}
