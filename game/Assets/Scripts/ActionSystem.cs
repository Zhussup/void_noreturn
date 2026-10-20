using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Прицел + печать действия. Луч из центра камеры находит Interactable;
/// игрок печатает серую подсказку (PICK UP / HANG UP), она заполняется
/// и по совпадению вызывается действие. Пробелы при наборе можно не ставить.
/// </summary>
public class ActionSystem : MonoBehaviour
{
    NightDirector dir;
    Camera cam;

    readonly RaycastHit[] hits = new RaycastHit[12];
    Interactable target;
    readonly List<char> typed = new List<char>();
    float lastReject = -100f;

    public bool HasTarget => target != null && target.IsAvailable;
    public string Prompt => HasTarget ? target.Prompt : "";
    public int TypedCount => typed.Count;
    public bool RejectedRecently => Time.time - lastReject < 0.25f;

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

        Interactable found = FindTarget();
        if (found != target)
        {
            target = found;
            typed.Clear();
        }
        if (!HasTarget) return;

        string typedThisFrame = Input.inputString;
        for (int i = 0; i < typedThisFrame.Length; i++)
        {
            char c = typedThisFrame[i];
            if (char.IsControl(c) || c == ' ') continue;
            // цифры и знаки не участвуют в подписи: за столом 1/2 — таблетка
            // и диктофон, их нельзя превращать в «неверную букву»
            if (!char.IsLetter(c)) continue;

            char expected = ExpectedChar(target.Prompt, typed.Count);
            if (expected == '\0') break;

            if (char.ToUpperInvariant(c) == char.ToUpperInvariant(expected))
            {
                typed.Add(expected);
                if (typed.Count >= LetterCount(target.Prompt))
                {
                    Interactable activated = target;
                    typed.Clear();
                    activated.Activate();
                    return;
                }
            }
            else
            {
                lastReject = Time.time;
            }
        }

        if (Input.GetKeyDown(KeyCode.Backspace) && typed.Count > 0)
            typed.RemoveAt(typed.Count - 1);
    }

    public void Clear()
    {
        target = null;
        typed.Clear();
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

    // ------------------------------------------------------------ буквы

    static int LetterCount(string text)
    {
        int n = 0;
        for (int i = 0; i < text.Length; i++)
            if (text[i] != ' ') n++;
        return n;
    }

    static char ExpectedChar(string text, int letterIndex)
    {
        int n = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == ' ') continue;
            if (n == letterIndex) return text[i];
            n++;
        }
        return '\0';
    }
}
