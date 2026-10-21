using UnityEditor;

namespace VOID.EnvModels
{
    public static class ModelsBatch1
    {
        [MenuItem("VOID/Env/Models/Партия 1 — все")]
        public static void BuildAll()
        {
            ModelMonitorCRT.Build();
            ModelKeyboard.Build();
            ModelPhone.Build();
            ModelRecorder.Build();
            ModelPills.Build();
            ModelWallClock.Build();
        }
    }
}
