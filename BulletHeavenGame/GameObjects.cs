using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BulletHeavenGame
{
    public abstract class GameObject
    {
        public Vector2 Position;
        public Texture2D Texture;
        public Color Tint = Color.White;
        public bool IsDead = false;
    }

    public class Player : GameObject
    {
        public int Health = 500;
        public int Damage = 20;
        public int BossDamage = 100;

        public void Reset()
        {
            Health = 500;
            IsDead = false;
        }
    }

    public class Enemy : GameObject
    {
        public int Health;
    }

    public class Boss : Enemy
    {
        public int ActiveSide;   
        public float SideTimer;  
    }

    public class Bullet : GameObject
    {
        public Vector2 Velocity;
    }

    public class PowerUp : GameObject
    {
        public enum Type { Health, Speed }
        public Type ItemType;
    }

    public class MeleeEffect : GameObject
    {
        public float TimeLeft;
    }

    public class ExplosionEffect : GameObject
    {
        public float TimeLeft;
        public float Size;
        public bool Triggered;
    }
}