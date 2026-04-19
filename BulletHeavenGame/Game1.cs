using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace BulletHeavenGame
{
    public class Game1 : Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        Player player;
        Texture2D playerTexture, enemyTexture, bossTexture, bulletTexture, healthTexture, speedTexture, meleeTexture, explosionTexture;
        
        List<Enemy> enemies = new();
        List<Bullet> bullets = new();
        List<PowerUp> powerUps = new();
        List<MeleeEffect> meleeEffects = new();
        List<ExplosionEffect> explosions = new();

        float spawnTimer = 0f;
        float shootTimer = 0f;
        float bossSpawnTimer = GameConstants.BossSpawnInterval;
        Random mainThreadRnd = new Random();
        
        
        // Состояние игры и счётчики
        int enemiesKilled = 0;
        int bossesKilled = 0;
        float gameTimeTotal = 0f;
        bool isGameOver = false;

        // Для меню
        float menuOffsetY = 0f; // Для анимации выплывания
        SpriteFont gameFont; 
        Texture2D blackPixel; // Для фона меню
        
        Vector2 cameraOffset = Vector2.Zero;
        KeyboardState previousKeyboardState;

        int gridCols, gridRows;
        List<GameObject>[,] grid;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            graphics.PreferredBackBufferWidth = GameConstants.WindowWidth;
            graphics.PreferredBackBufferHeight = GameConstants.WindowHeight;
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            player = new Player
            {
                Position = new Vector2(GameConstants.WindowWidth / 2f - 25f, GameConstants.WindowHeight / 2f - 25f)
            };

            gridCols = GameConstants.WindowWidth / GameConstants.CellSize + 2;
            gridRows = GameConstants.WindowHeight / GameConstants.CellSize + 2;
            grid = new List<GameObject>[gridCols, gridRows];
            
            for (var i = 0; i < gridCols; i++)
                for (var j = 0; j < gridRows; j++)
                    grid[i, j] = new List<GameObject>();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);

            playerTexture = CreateColoredTexture(50, 50, Color.Black);
            bulletTexture = CreateColoredTexture(20, 20, Color.DarkRed);
            healthTexture = CreateColoredTexture(5, 5, Color.GreenYellow);
            speedTexture = CreateColoredTexture(5, 5, Color.YellowGreen);
            meleeTexture = CreateColoredTexture(GameConstants.MeleeEffectSize, GameConstants.MeleeEffectSize, Color.Red);
            explosionTexture = CreateColoredTexture(GameConstants.BossExplosionSize, GameConstants.BossExplosionSize, new Color(255, 0, 0, 110));
            enemyTexture = CreateColoredTexture(30, 30, Color.White);
            bossTexture = CreateColoredTexture(80, 80, Color.DarkMagenta);

            player.Texture = playerTexture;
            
            gameFont = Content.Load<SpriteFont>("font");

            // Пиксель для отрисовки прямоугольников (меню)
            blackPixel = new Texture2D(GraphicsDevice, 1, 1);
            blackPixel.SetData(new[] { Color.White });

            player.Texture = playerTexture;
            menuOffsetY = GameConstants.WindowHeight; // Меню изначально за экраном
        }

        private Texture2D CreateColoredTexture(int width, int height, Color color)
        {
            var rect = new Texture2D(GraphicsDevice, width, height);
            var data = new Color[width * height];
            Array.Fill(data, color);
            rect.SetData(data);
            return rect;
        }

        protected override void Update(GameTime gameTime)
        {
            var currentKeyboard = Keyboard.GetState();
            if (currentKeyboard.IsKeyDown(Keys.Escape)) Exit();

            var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (isGameOver)
            {
                // Анимация меню (выползает снизу)
                menuOffsetY = MathHelper.Lerp(menuOffsetY, 0, 0.1f);

                if (currentKeyboard.IsKeyDown(Keys.R)) RestartGame();
                if (currentKeyboard.IsKeyDown(Keys.Escape)) Exit();
                return;
            }
            
            gameTimeTotal += dt;
            
            var mouse = Mouse.GetState();
            var mouseWorld = new Vector2(mouse.X + cameraOffset.X, mouse.Y + cameraOffset.Y);

            // Ввод и движение
            HandlePlayerMovement(currentKeyboard, dt);
            UpdateCamera();

            // Спавн
            UpdateSpawners(dt);

            // Обновление сущностей
            UpdateEntities(dt);

            // Бой
            HandleCombat(currentKeyboard, mouse, mouseWorld, dt);

            // Пространственная сетка и коллизии
            BuildCollisionGrid();
            HandleCollisions();
            ProcessChainExplosions();

            // Взаимодействие с лутом
            HandleLoot(dt);

            // Очистка
            CleanupDeadEntities();
            
            if (player.Health <= 0)
            {
                player.Health = 0;
                isGameOver = true;
            }
            
            previousKeyboardState = currentKeyboard;
            base.Update(gameTime);
        }
        
        private void RestartGame()
        {
            player.Reset();
            player.Position = new Vector2(GameConstants.WindowWidth / 2f, GameConstants.WindowHeight / 2f);
            enemies.Clear();
            bullets.Clear();
            powerUps.Clear();
            enemiesKilled = 0;
            bossesKilled = 0;
            gameTimeTotal = 0f;
            isGameOver = false;
            menuOffsetY = GameConstants.WindowHeight;
        }
        
        private void HandlePlayerMovement(KeyboardState keyboard, float dt)
        {
            var movement = Vector2.Zero;
            if (keyboard.IsKeyDown(Keys.W)) movement.Y -= 1;
            if (keyboard.IsKeyDown(Keys.S)) movement.Y += 1;
            if (keyboard.IsKeyDown(Keys.A)) movement.X -= 1;
            if (keyboard.IsKeyDown(Keys.D)) movement.X += 1;

            if (movement != Vector2.Zero) movement.Normalize();
            player.Position += movement * GameConstants.PlayerSpeed * dt;
        }

        private void UpdateCamera()
        {
            var deadX = (GameConstants.WindowWidth - GameConstants.CameraDeadZoneWidth) / 2;
            var deadY = (GameConstants.WindowHeight - GameConstants.CameraDeadZoneHeight) / 2;
            var deadZone = new Rectangle(deadX, deadY, GameConstants.CameraDeadZoneWidth, GameConstants.CameraDeadZoneHeight);

            var playerScreenX = player.Position.X - cameraOffset.X;
            var playerScreenY = player.Position.Y - cameraOffset.Y;

            if (playerScreenX < deadZone.Left)
                cameraOffset.X = player.Position.X - deadZone.Left;
            else if (playerScreenX + player.Texture.Width > deadZone.Right)
                cameraOffset.X = player.Position.X + player.Texture.Width - deadZone.Right;

            if (playerScreenY < deadZone.Top)
                cameraOffset.Y = player.Position.Y - deadZone.Top;
            else if (playerScreenY + player.Texture.Height > deadZone.Bottom)
                cameraOffset.Y = player.Position.Y + player.Texture.Height - deadZone.Bottom;
        }

        private int activeBossSide = -1;
        
        private void UpdateSpawners(float dt)
        {
            // Спавн босса
            bossSpawnTimer -= dt;
            if (bossSpawnTimer <= 0)
            {
                activeBossSide = SpawnBoss();
                bossSpawnTimer = GameConstants.BossSpawnInterval;
            }

            // Спавн обычных врагов
            spawnTimer -= dt;
            if (spawnTimer <= 0)
            {
                SpawnEnemies(activeBossSide);
                spawnTimer = GameConstants.SpawnInterval;
            }
        }

        private int SpawnBoss()
        {
            var side = mainThreadRnd.Next(4);
            var spawnPos = GetSpawnPosition(side, 100f, mainThreadRnd);

            enemies.Add(new Boss
            {
                Position = spawnPos,
                Texture = bossTexture,
                Tint = Color.White,
                Health = GameConstants.BossHealth,
                ActiveSide = side,
                SideTimer = GameConstants.BossSideChangeInterval
            });
            return side;
        }

        private void SpawnEnemies(int activeBossSide)
        {
            var newEnemies = new ConcurrentBag<Enemy>();

            Parallel.For(0, GameConstants.SpawnCountPerTick, i =>
            {
                var rnd = ThreadSafeRandom.Instance;
                var side = CalculateEnemySpawnSide(activeBossSide, rnd);
                var spawnPos = GetSpawnPosition(side, 50f, rnd);

                newEnemies.Add(new Enemy
                {
                    Position = spawnPos,
                    Texture = enemyTexture,
                    Tint = new Color(rnd.Next(256), rnd.Next(256), rnd.Next(256)),
                    Health = 20
                });
            });

            enemies.AddRange(newEnemies);
        }

        private int CalculateEnemySpawnSide(int activeBossSide, Random rnd)
        {
            if (activeBossSide == -1) return rnd.Next(4);

            int[] sidePool;
            if (activeBossSide == 0 || activeBossSide == 1)
                sidePool = new int[] { activeBossSide, activeBossSide, activeBossSide, activeBossSide, 2, 3 };
            else
                sidePool = new int[] { activeBossSide, activeBossSide, activeBossSide, activeBossSide, 0, 1 };

            return sidePool[rnd.Next(sidePool.Length)];
        }

        private Vector2 GetSpawnPosition(int side, float offset, Random rnd)
        {
            var left = cameraOffset.X - offset;
            var right = cameraOffset.X + GameConstants.WindowWidth + offset;
            var top = cameraOffset.Y - offset;
            var bottom = cameraOffset.Y + GameConstants.WindowHeight + offset;

            return side switch
            {
                0 => new Vector2(left, rnd.Next(GameConstants.WindowHeight) + cameraOffset.Y),
                1 => new Vector2(right, rnd.Next(GameConstants.WindowHeight) + cameraOffset.Y),
                2 => new Vector2(rnd.Next(GameConstants.WindowWidth) + cameraOffset.X, top),
                _ => new Vector2(rnd.Next(GameConstants.WindowWidth) + cameraOffset.X, bottom),
            };
        }

        private void UpdateEntities(float dt)
        {
            // Враги
            foreach (var e in enemies)
            {
                if (e.IsDead) continue;

                if (e is Boss b)
                {
                    b.SideTimer -= dt;
                    if (b.SideTimer <= 0)
                    {
                        b.ActiveSide = b.ActiveSide switch { 0 => 2, 2 => 1, 1 => 3, 3 => 0, _ => 0 };
                        b.SideTimer = GameConstants.BossSideChangeInterval;
                    }
                }

                var dir = player.Position - e.Position;
                if (dir != Vector2.Zero)
                {
                    dir.Normalize();
                    var speed = (e is Boss) ? GameConstants.BossSpeed : GameConstants.EnemySpeed;
                    e.Position += dir * speed * dt;
                }
            }

            // Пули
            foreach (var b in bullets)
            {
                if (b.IsDead) continue;
                b.Position += b.Velocity * dt;

                if (b.Position.X < cameraOffset.X - 100 || b.Position.X > cameraOffset.X + GameConstants.WindowWidth + 100 ||
                    b.Position.Y < cameraOffset.Y - 100 || b.Position.Y > cameraOffset.Y + GameConstants.WindowHeight + 100)
                    b.IsDead = true;
            }

            // Таймеры эффектов
            foreach (var effect in meleeEffects)
            {
                effect.TimeLeft -= dt;
                if (effect.TimeLeft <= 0f) effect.IsDead = true;
            }

            foreach (var explosion in explosions)
            {
                explosion.TimeLeft -= dt;
                if (explosion.TimeLeft <= 0f) explosion.IsDead = true;
            }
        }

        private void HandleCombat(KeyboardState keyboard, MouseState mouse, Vector2 mouseWorld, float dt)
        {
            // Ближний бой, удар вокруг
            if (mouse.LeftButton == ButtonState.Pressed)
            {
                foreach (var e in enemies)
                {
                    if (!e.IsDead && Vector2.DistanceSquared(player.Position, e.Position) < 900f)
                    {
                        e.IsDead = true;
                        if (e is Boss boss) TriggerBossExplosion(boss);
                    }
                }
            }

            // Удар по Space
            if (keyboard.IsKeyDown(Keys.Space) && previousKeyboardState.IsKeyUp(Keys.Space))
            {
                PerformMeleeAttack(mouseWorld);
            }

            // Авто-стрельба
            shootTimer -= dt;
            if (shootTimer <= 0)
            {
                var shootDir = mouse.LeftButton == ButtonState.Pressed ? mouseWorld - player.Position : GetAutoShootDirection();
                
                if (shootDir != Vector2.Zero) shootDir.Normalize();
                else shootDir = new Vector2(1f, 0f);

                bullets.Add(new Bullet
                {
                    Position = player.Position + new Vector2(0, 15),
                    Velocity = shootDir * GameConstants.BulletSpeed,
                    Texture = bulletTexture
                });

                shootTimer = GameConstants.ShootInterval;
            }
        }

        private Vector2 GetAutoShootDirection()
        {
            var target = GetNearestEnemy();
            if (target != null)
                return target.Position - player.Position;
            
            var angle = (float)(mainThreadRnd.NextDouble() * Math.PI * 2.0);
            return new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
        }

        private Enemy GetNearestEnemy()
        {
            Enemy nearest = null;
            var minDistSq = float.MaxValue;

            foreach (var e in enemies)
            {
                if (e.IsDead) continue;
                var distSq = Vector2.DistanceSquared(player.Position, e.Position);
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    nearest = e;
                }
            }
            return nearest;
        }

        private void PerformMeleeAttack(Vector2 mouseWorld)
        {
            var attackDir = mouseWorld - player.Position;
            if (attackDir == Vector2.Zero) attackDir = new Vector2(1f, 0f);
            else attackDir.Normalize();

            var attackCenter = player.Position + attackDir * GameConstants.MeleeRange;

            meleeEffects.Add(new MeleeEffect
            {
                Position = attackCenter - new Vector2(GameConstants.MeleeEffectSize / 2f, GameConstants.MeleeEffectSize / 2f),
                Texture = meleeTexture,
                Tint = Color.White,
                TimeLeft = GameConstants.MeleeEffectDuration
            });

            var hitRadiusSq = GameConstants.MeleeHitRadius * GameConstants.MeleeHitRadius;

            foreach (var e in enemies)
            {
                if (e.IsDead) continue;

                var enemyCenter = e.Position + new Vector2(e.Texture.Width / 2f, e.Texture.Height / 2f);
                if (Vector2.DistanceSquared(enemyCenter, attackCenter) <= hitRadiusSq)
                {
                    var knockDir = enemyCenter - player.Position;
                    if (knockDir == Vector2.Zero) knockDir = attackDir;
                    else knockDir.Normalize();

                    e.Position += knockDir * GameConstants.MeleeKnockbackDistance;
                }
            }
        }

        private void BuildCollisionGrid()
        {
            for (var i = 0; i < gridCols; i++)
                for (var j = 0; j < gridRows; j++)
                    grid[i, j].Clear();

            foreach (var e in enemies)
            {
                if (e.IsDead) continue;

                var screenPos = e.Position - cameraOffset;
                var cx = Math.Clamp((int)Math.Floor(screenPos.X / GameConstants.CellSize), 0, gridCols - 1);
                var cy = Math.Clamp((int)Math.Floor(screenPos.Y / GameConstants.CellSize), 0, gridRows - 1);
                grid[cx, cy].Add(e);
            }
        }

        private void HandleCollisions()
        {
            // Пули против врагов
            foreach (var b in bullets)
            {
                if (b.IsDead) continue;

                var bScreenPos = b.Position - cameraOffset;
                var bx = Math.Clamp((int)Math.Floor(bScreenPos.X / GameConstants.CellSize), 0, gridCols - 1);
                var by = Math.Clamp((int)Math.Floor(bScreenPos.Y / GameConstants.CellSize), 0, gridRows - 1);
                var bBounds = new Rectangle((int)bScreenPos.X, (int)bScreenPos.Y, b.Texture.Width, b.Texture.Height);
                var hit = false;

                for (var dx = -1; dx <= 1 && !hit; dx++)
                {
                    for (var dy = -1; dy <= 1 && !hit; dy++)
                    {
                        int nx = bx + dx, ny = by + dy;
                        if (nx >= 0 && ny >= 0 && nx < gridCols && ny < gridRows)
                        {
                            foreach (var obj in grid[nx, ny])
                            {
                                if (obj is Enemy e && !e.IsDead)
                                {
                                    var eScreenPos = e.Position - cameraOffset;
                                    var eBounds = new Rectangle((int)eScreenPos.X, (int)eScreenPos.Y, e.Texture.Width, e.Texture.Height);

                                    if (bBounds.Intersects(eBounds))
                                    {
                                        e.Health -= player.Damage;
                                        hit = true;
                                        
                                        if (e is Boss) b.IsDead = true;
                                        
                                        if (e.Health <= 0) KillEnemy(e);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Игрок против врагов
            var playerBounds = new Rectangle(
                (int)(player.Position.X - cameraOffset.X),
                (int)(player.Position.Y - cameraOffset.Y),
                player.Texture.Width, player.Texture.Height);

            foreach (var e in enemies)
            {
                if (e.IsDead) continue;

                var eScreenPos = e.Position - cameraOffset;
                var eBounds = new Rectangle((int)eScreenPos.X, (int)eScreenPos.Y, e.Texture.Width, e.Texture.Height);

                if (playerBounds.Intersects(eBounds))
                {
                    if (e is Boss)
                        player.Health -= player.BossDamage;
                    else player.Health -= player.Damage;
                    KillEnemy(e);
                }
            }
        }

        private void KillEnemy(Enemy e)
        {
            e.IsDead = true;
            if (e is Boss boss)
            {
                bossesKilled++;
                boss.Health = 0;
                TriggerBossExplosion(boss);
            }
            else if (e.Health <= 0) // Спавн лута только если убит оружием
            {
                enemiesKilled++;
                SpawnLoot(e.Position);
                e.Health = 1; // Защита от двойного спавна
            }
        }

        private void SpawnLoot(Vector2 position)
        {
            var type = (mainThreadRnd.Next(2) == 0) ? PowerUp.Type.Health : PowerUp.Type.Speed;
            powerUps.Add(new PowerUp
            {
                Position = position,
                Texture = (type == PowerUp.Type.Health) ? healthTexture : speedTexture,
                ItemType = type
            });
        }

        private void HandleLoot(float dt)
        {
            var playerBounds = new Rectangle(
                (int)(player.Position.X - cameraOffset.X),
                (int)(player.Position.Y - cameraOffset.Y),
                player.Texture.Width, player.Texture.Height);

            foreach (var p in powerUps)
            {
                if (p.IsDead) continue;

                var toPlayer = player.Position - p.Position;
                var distSq = toPlayer.LengthSquared();

                // Притягивание
                if (distSq > 1f)
                {
                    var dist = (float)Math.Sqrt(distSq);
                    var dir = toPlayer / dist;
                    var move = Math.Min(GameConstants.PowerUpSpeed * dt, dist);
                    p.Position += dir * move;
                }

                // Сбор
                var pScreenPos = p.Position - cameraOffset;
                var pBounds = new Rectangle((int)pScreenPos.X, (int)pScreenPos.Y, p.Texture.Width, p.Texture.Height);

                if (playerBounds.Intersects(pBounds))
                {
                    if (p.ItemType == PowerUp.Type.Health)
                        player.Health = Math.Min(player.Health + 20, 500);

                    p.IsDead = true;
                }
            }
        }

        private void TriggerBossExplosion(Boss boss)
        {
            explosions.Add(new ExplosionEffect
            {
                Position = boss.Position + new Vector2(boss.Texture.Width / 2f, boss.Texture.Height / 2f),
                Texture = explosionTexture,
                Tint = Color.DarkRed,
                TimeLeft = GameConstants.BossExplosionDuration,
                Size = GameConstants.BossExplosionSize,
                Triggered = false
            });
        }

        private void ProcessChainExplosions()
        {
            for (var i = 0; i < explosions.Count; i++)
            {
                var explosion = explosions[i];
                if (!explosion.Triggered)
                {
                    explosion.Triggered = true;
                    ApplyExplosionDamage(explosion);
                }
            }
        }

        private void ApplyExplosionDamage(ExplosionEffect explosion)
        {
            var screenCenter = explosion.Position - cameraOffset;
            var halfSize = explosion.Size / 2f;
            var expBounds = new Rectangle((int)(screenCenter.X - halfSize), (int)(screenCenter.Y - halfSize), (int)explosion.Size, (int)explosion.Size);
            
            var searchBounds = expBounds;
            searchBounds.Inflate(80, 80);

            var minX = Math.Clamp((int)Math.Floor((float)searchBounds.Left / GameConstants.CellSize), 0, gridCols - 1);
            var maxX = Math.Clamp((int)Math.Floor((float)(searchBounds.Right - 1) / GameConstants.CellSize), 0, gridCols - 1);
            var minY = Math.Clamp((int)Math.Floor((float)searchBounds.Top / GameConstants.CellSize), 0, gridRows - 1);
            var maxY = Math.Clamp((int)Math.Floor((float)(searchBounds.Bottom - 1) / GameConstants.CellSize), 0, gridRows - 1);

            for (var x = minX; x <= maxX; x++)
            {
                for (var y = minY; y <= maxY; y++)
                {
                    foreach (var obj in grid[x, y])
                    {
                        if (obj is Enemy e && !e.IsDead)
                        {
                            var eScreenPos = e.Position - cameraOffset;
                            var eBounds = new Rectangle((int)eScreenPos.X, (int)eScreenPos.Y, e.Texture.Width, e.Texture.Height);

                            if (expBounds.Intersects(eBounds))
                            {
                                e.Health = 0;
                                KillEnemy(e);
                            }
                        }
                    }
                }
            }
        }

        private void CleanupDeadEntities()
        {
            bullets.RemoveAll(b => b.IsDead);
            enemies.RemoveAll(e => e.IsDead);
            powerUps.RemoveAll(p => p.IsDead);
            meleeEffects.RemoveAll(m => m.IsDead);
            explosions.RemoveAll(e => e.IsDead);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            spriteBatch.Begin();

            foreach (var effect in meleeEffects)
                spriteBatch.Draw(effect.Texture, effect.Position - cameraOffset, effect.Tint);

            foreach (var explosion in explosions)
                spriteBatch.Draw(explosion.Texture, explosion.Position - new Vector2(explosion.Size / 2f, explosion.Size / 2f) - cameraOffset, explosion.Tint);

            spriteBatch.Draw(player.Texture, player.Position - cameraOffset, player.Tint);

            foreach (var pu in powerUps)
                spriteBatch.Draw(pu.Texture, pu.Position - cameraOffset, pu.Tint);

            foreach (var b in bullets)
                spriteBatch.Draw(b.Texture, b.Position - cameraOffset, b.Tint);


            var bosses = new List<Boss>();
            // Обычные враги
            foreach (var e in enemies)
                if (e is Boss boss)
                   bosses.Add(boss);
                else spriteBatch.Draw(e.Texture, e.Position - cameraOffset, e.Tint);


            // Боссы сверху
            foreach (var boss in bosses)
                spriteBatch.Draw(boss.Texture, boss.Position - cameraOffset, boss.Tint);
            
            // HUD
            spriteBatch.Draw(blackPixel, new Rectangle(15, GameConstants.WindowHeight - 210, 155, 45), Color.White);
            spriteBatch.DrawString(gameFont, $"Health: {player.Health}%", 
                new Vector2(20, GameConstants.WindowHeight - 200), Color.Red);
            var enemyText = $"Killed: {enemiesKilled}";
            var bossText = $"Bosses: {bossesKilled}";
            spriteBatch.DrawString(gameFont, enemyText, 
                new Vector2(GameConstants.WindowWidth - 300, GameConstants.WindowHeight - 100), Color.White);
            spriteBatch.DrawString(gameFont, bossText, 
                new Vector2(GameConstants.WindowWidth - 300, GameConstants.WindowHeight - 75), Color.White);
            
            if (isGameOver)
            {
                // Затемнение
                spriteBatch.Draw(blackPixel, new Rectangle(0, 0, GameConstants.WindowWidth, GameConstants.WindowHeight), Color.Black * 0.5f);

                // Плашка меню
                var menuRect = new Rectangle(GameConstants.WindowWidth / 2 - 200, (int)menuOffsetY + GameConstants.WindowHeight / 2 - 150, 400, 300);
                spriteBatch.Draw(blackPixel, menuRect, Color.DarkSlateGray);

                var stats = $"GAME OVER\n\nEnemies: {enemiesKilled}\nBosses: {bossesKilled}\nTime: {(int)gameTimeTotal}s\n\n[R] Play Again  [Esc] Exit";
                spriteBatch.DrawString(gameFont, stats, new Vector2(menuRect.X + 50, menuRect.Y + 50), Color.White);
            }
            
            spriteBatch.End();
            base.Draw(gameTime);
        }
    }
}