using UnityEngine;

namespace FadedDreams.World
{
    [DisallowMultipleComponent]
    public class Mirror2D : MonoBehaviour
    {
        [Tooltip("是否启用这面镜子的反射")]
        public bool enabledReflection = true;
    }
}
