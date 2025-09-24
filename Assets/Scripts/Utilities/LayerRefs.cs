using UnityEngine;


namespace FadedDreams.Utilities
{
    [CreateAssetMenu(menuName = "RE:Dream/LayerRefs", fileName = "LayerRefs")]
    public class LayerRefs : ScriptableObject
    {
        public LayerMask worldObstacles; // ǽ/���Σ������ڵ���
        public LayerMask enemy; // ����
        public LayerMask torch; // ���
    }
}