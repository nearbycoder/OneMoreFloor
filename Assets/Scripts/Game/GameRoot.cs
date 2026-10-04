using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>Bootstrap: the Main scene only contains this. Everything else is built at runtime.</summary>
    public sealed class GameRoot : MonoBehaviour
    {
        void Awake()
        {
            Application.targetFrameRate = 120;
        }
    }
}
