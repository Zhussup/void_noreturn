using System;
using UnityEngine;

/// <summary>
/// Объект, с которым игрок взаимодействует клавишей E, наведя на него прицел.
/// Пока действие доступно, у прицела видна короткая подпись с клавишей.
/// </summary>
public class Interactable : MonoBehaviour
{
    [Tooltip("Подпись у прицела, пока действие доступно. Пустая строка — недоступно.")]
    public string label = "E — взять трубку";

    [Tooltip("Доступно ли действие прямо сейчас.")]
    public bool available = true;

    [Tooltip("Максимальная дистанция прицеливания, м.")]
    public float maxDistance = 2.6f;

    public event Action Activated;

    public bool IsAvailable => available && !string.IsNullOrEmpty(label);
    public string Label => IsAvailable ? label : "";

    public void Activate()
    {
        if (!IsAvailable) return;
        if (Activated != null) Activated();
    }
}
