namespace FadedDreams.Enemies
{
    public interface IDamageable
    {
        void TakeDamage(float amount);
        bool IsDead { get; }
    }
}