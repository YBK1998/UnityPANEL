using UnityEngine;

namespace A11Sim
{
    /// Makes the simulation start in any scene (the project scene already contains it).
    static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindAnyObjectByType<SchematicView>() == null)
                new GameObject("A11 Circuit").AddComponent<SchematicView>();
        }
    }
}
