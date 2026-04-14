namespace BulletHeavenGame
{
    public static class GameConstants
    {
        // Окно и камера
        public const int WindowWidth = 1920;
        public const int WindowHeight = 1080;
        public const int CameraDeadZoneWidth = 320;
        public const int CameraDeadZoneHeight = 180;

        // Игрок и стрельба
        public const float PlayerSpeed = 450f;
        public const float BulletSpeed = 600f;
        public const float ShootInterval = 0.02f;

        // Враги
        public const float EnemySpeed = 200f;
        public const float SpawnInterval = 0.1f;
        public const int SpawnCountPerTick = 100;
        
        // Босс
        public const float BossSpawnInterval = 2f;
        public const float BossSpeed = 90f;
        public const int BossHealth = 2000;
        public const float BossSideChangeInterval = 6f;
        public const int BossExplosionSize = 400;
        public const float BossExplosionDuration = 0.3f;

        // Поверапы
        public const float PowerUpSpeed = 420f;
        
        // Ближний бой
        public const float MeleeRange = 110f;
        public const float MeleeHitRadius = 70f;
        public const float MeleeKnockbackDistance = 180f;
        public const float MeleeEffectDuration = 0.30f;
        public const int MeleeEffectSize = 60;

        // Оптимизация
        public const int CellSize = 50;
    }
}