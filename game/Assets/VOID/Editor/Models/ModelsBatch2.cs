using UnityEditor;

namespace VOID.EnvModels
{
    public static class ModelsBatch2
    {
        [MenuItem("VOID/Env/Models/Партия 2 — все")]
        public static void BuildAll()
        {
            ModelDoor.Build();
            ModelWindowFrame.Build();
            ModelRadiator.Build();
            ModelCeilingLamp.Build();
            ModelDeskLamp.Build();
            ModelLaptop.Build();
            ModelMug.Build();
            ModelGlass.Build();
            ModelTrashBin.Build();
            ModelShelf.Build();
            ModelMouse.Build();
            ModelCables.Build();
        }
    }
}
