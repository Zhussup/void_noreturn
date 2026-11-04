using UnityEngine;

/// <summary>
/// CRT-постэффект камеры: скан-линии, мерцание кинескопа, расфокус сходимости
/// RGB, прокат развёртки и «пересканирование» полос. Вешается на камеру из
/// NightDirector.BuildCrt; вспышки силы (FlashCrt) включает NightDirector
/// в момент проверки кибербезов — и тут же гасит.
/// Шейдер — VOID/CRT_Post (Assets/VOID/Art/Shaders/CRT_Post.shader).
/// </summary>
[RequireComponent(typeof(Camera))]
public class CrtFilter : MonoBehaviour
{
    public Material CrtMat;
    [Range(0f, 1f)] public float intensity = 0.5f;

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (CrtMat == null || intensity <= 0.001f)
        {
            Graphics.Blit(src, dst); // без эффекта — прямо в цель
            return;
        }
        CrtMat.SetFloat("_CrtIntensity", intensity);
        Graphics.Blit(src, dst, CrtMat);
    }
}