using UnityEngine;


namespace FadedDreams.Utilities
{
    [CreateAssetMenu(menuName = "RE:Dream/LayerRefs", fileName = "LayerRefs")]
    public class LayerRefs : ScriptableObject
    {
        public LayerMask worldObstacles; // 墙/地形（用于遮挡）
        public LayerMask enemy; // 敌人
        public LayerMask torch; // 火把
    }
}