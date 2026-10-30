using UnityEngine;

/// <summary>
/// Строка ввода с опечатками от бессонницы: часть верных нажатий «не засчитывается».
/// Возвращает только текст; Enter/Backspace приложение обрабатывает само.
/// Курсор живёт внутри строки: ←/→, Home/End, Delete и Backspace работают
/// по позиции, а не только в конце.
/// </summary>
public class InputField
{
    public string text = "";
    public int caret;              // 0..text.Length
    public float lastReject = -100f;
    public int maxLength = 72;

    public void Clear()
    {
        text = "";
        caret = 0;
    }

    /// <summary>Поставить текст и курсор в конец (история команд, внешняя правка).</summary>
    public void SetText(string value)
    {
        text = value ?? "";
        caret = text.Length;
    }

    /// <summary>После внешней правки text курсор не должен выйти за границы.</summary>
    public void ClampCaret()
    {
        caret = Mathf.Clamp(caret, 0, text.Length);
    }

    public bool RejectedRecently => Time.time - lastReject < 0.26f;

    /// <summary>Скормить нажатия. chance — вероятность потерять символ (0..1).</summary>
    public void Tick(float chance)
    {
        ClampCaret();

        // Правка внутри строки. Backspace из inputString ниже игнорируется,
        // чтобы не удалять дважды.
        if (Input.GetKeyDown(KeyCode.LeftArrow)) caret = Mathf.Max(0, caret - 1);
        if (Input.GetKeyDown(KeyCode.RightArrow)) caret = Mathf.Min(text.Length, caret + 1);
        if (Input.GetKeyDown(KeyCode.Home)) caret = 0;
        if (Input.GetKeyDown(KeyCode.End)) caret = text.Length;
        if (Input.GetKeyDown(KeyCode.Delete) && caret < text.Length)
            text = text.Remove(caret, 1);
        if (Input.GetKeyDown(KeyCode.Backspace) && caret > 0)
        {
            text = text.Remove(caret - 1, 1);
            caret--;
        }

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
            if (text.Length < maxLength)
            {
                text = text.Insert(caret, c.ToString());
                caret++;
            }
        }
    }

    public string Masked(char mask = '*')
    {
        return new string(mask, text.Length);
    }

    /// <summary>Текст с курсором в позиции caret. Пустой символ сохраняет ширину строки.</summary>
    public string WithCursor(bool show, string cursor = "_")
    {
        return text.Insert(Mathf.Clamp(caret, 0, text.Length), show ? cursor : " ");
    }

    /// <summary>То же для пароля: звёздочки, курсор на своей позиции.</summary>
    public string MaskedWithCursor(bool show, string cursor = "_", char mask = '*')
    {
        return Masked(mask).Insert(Mathf.Clamp(caret, 0, text.Length), show ? cursor : " ");
    }
}
