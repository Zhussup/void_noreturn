using System;
using UnityEngine;

/// <summary>
/// Объект, с которым игрок взаимодействует, наведя прицел и напечатав
/// серую подсказку (например, PICK UP у телефона). Подсказка служит
/// одновременно и действием: пока она не набрана, ничего не происходит.
/// </summary>
public class Interactable : MonoBehaviour
{
    [Tooltip("Что нужно напечатать. Пустая строка — взаимодействие недоступно.")]
    public string prompt = "PICK UP";

    [Tooltip("Доступно ли действие прямо сейчас.")]
    public bool available = true;

    [Tooltip("Максимальная дистанция прицеливания, м.")]
    public float maxDistance = 2.6f;

    public event Action Activated;

    public bool IsAvailable => available && !string.IsNullOrEmpty(prompt);
    public string Prompt => IsAvailable ? prompt : "";

    public void Activate()
    {
        if (!IsAvailable) return;
        if (Activated != null) Activated();
    }
}
