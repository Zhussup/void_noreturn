using UnityEngine;

/// <summary>
/// Строка ввода с опечатками от бессонницы: часть верных нажатий «не засчитывается».
/// Возвращает только текст; Enter/Backspace приложение обрабатывает само.
/// </summary>
public class InputField
{
    public string text = "";
    public float lastReject = -100f;
    public int maxLength = 72;

    public void Clear() => text = "";

    public bool RejectedRecently => Time.time - lastReject < 0.26f;

    /// <summary>Скормить нажатия. chance — вероятность потерять символ (0..1).</summary>
    public void Tick(float chance)
    {
        string typed = Input.inputString;
        for (int i = 0; i < typed.Length; i++)
        {
            char c = typed[i];
            if (c == '\b' || c == '\n' || c == '\r' || char.IsControl(c)) continue;
            // E, забранная приближением монитора, не должна печататься в строке.
            if ((c == 'e' || c == 'E') && PlayerController.ConsumeFocusKeyChar(c)) continue;
            if (UnityEngine.Random.value < chance)
            {
                lastReject = Time.time;
                continue;
            }
            if (text.Length < maxLength) text += c;
        }

        if (Input.GetKeyDown(KeyCode.Backspace) && text.Length > 0)
            text = text.Substring(0, text.Length - 1);
    }

    public string Masked(char mask = '*')
    {
        return new string(mask, text.Length);
    }

    public string WithCursor(bool show)
    {
        return text + (show ? "_" : " ");
    }
}
