using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Nanite;

internal sealed class ArenaScreen : Screen
{
    private readonly Arena _arena;
    private readonly PlayerState _playerState = new PlayerState();
    private readonly CombatManager _combatManager = new CombatManager();
    private readonly HiveData _myHive;

    private const float BladeInnerRadius = 60f;
    private const float BladeOuterRadius = 90f;

    // ---- Overdrive (Saw State) ----
    private float _overdriveCharge = 0f;
    private const float MaxOverdriveCharge = 100f; 
    private float _overdriveActiveTimer = 0f;
    private const float OverdriveDuration = 5f; 
    
    // Bullets (Infinite Ammo)
    private readonly List<Vector2> _bulletPositions = new();
    private readonly List<Vector2> _bulletVelocities = new();
    private const float BulletSpeed = 700f;
    private const int BulletSize = 8;
    private const int BulletDamage = 15;

    // Rapid Fire
    private float _rapidFireCharge = 300f;
    private const float MaxRapidFireCharge = 300f;
    private float _rapidFireDrainPerShot = 3f;
    private float _rapidFireRefillPerSecond = 25f;
    private float _rapidFireShotInterval = 0.08f;
    private float _rapidFireShotTimer = 0f;
    
    private MouseState _previousMouse;
    private float _contactDamageCooldown = 0f;

    // Textures
    private readonly Texture2D _bladeTexture;
    private readonly Texture2D _eyeSocketTexture;
    private readonly Texture2D _eyePupilTexture;
    private readonly Texture2D _targetTexture;
    private readonly Dictionary<string, List<Texture2D>> _enemyAnimations;

    private const float EyeSocketScale = 1.5f;
    private Vector2 _pupilOffset = Vector2.Zero;
    private const float PupilMaxOffset = 5f;

    private bool _cleared = false;
    private float _clearedTimer = 0f;
    private const float ClearedDuration = 2f;

    private int _score = 0;

    public ArenaScreen(Game1 game, HiveData hive) : base(game)
    {
        _myHive = hive;
        _arena = new Arena(hive, BladeOuterRadius);
        _arena.OnEnemyKilledReward = GrantReward;
    _arena.OnRedBugExplosion = pos => _combatManager.AddExplosion(pos);
        _bladeTexture = game.Content.Load<Texture2D>("Blade1_Frame1");
        _eyeSocketTexture = game.Content.Load<Texture2D>("eye_socket");
        _eyePupilTexture = game.Content.Load<Texture2D>("eye_pupil");
        _targetTexture = game.Content.Load<Texture2D>("Target_icon");

        _enemyAnimations = new Dictionary<string, List<Texture2D>>();
        var red = new List<Texture2D>();
        for (int i = 1; i <= 9; i++) red.Add(game.Content.Load<Texture2D>($"Red_bug_frame{i}"));
        _enemyAnimations["Red Bug"] = red;

        var green = new List<Texture2D>();
        for (int i = 1; i <= 8; i++) green.Add(game.Content.Load<Texture2D>($"Green_bug_frame{i}"));
        _enemyAnimations["Armor Bug"] = green;

        var purple = new List<Texture2D>();
        for (int i = 1; i <= 7; i++) purple.Add(game.Content.Load<Texture2D>($"Purple_bug_frame{i}"));
        _enemyAnimations["Rapidfire Bug"] = purple;

        var yellow = new List<Texture2D>();
        for (int i = 1; i <= 6; i++) yellow.Add(game.Content.Load<Texture2D>($"Yellow_bug_frame{i}"));
        _enemyAnimations["Yellow Bug"] = yellow;

        _playerState.Position = _arena.Center;
        _combatManager.Reset();
    }

    public override void Update(GameTime gameTime, KeyboardState keyboard, MouseState mouse)
    {
        float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_cleared || _playerState.IsDead)
        {
            _clearedTimer += delta;
            if (_clearedTimer >= ClearedDuration)
            {
                if (_cleared) _myHive.Cleared = true; // Mark it cleared on the map!
                Game.SwitchScreen(new HiveMapScreen(Game)); // Go back to map
            }
            return;
        }

        _combatManager.TickSpin(delta);
        if (_contactDamageCooldown > 0f) _contactDamageCooldown -= delta;

        // ---- Overdrive Logic ----
        bool isOverdriveActive = _overdriveActiveTimer > 0f;

        if (isOverdriveActive)
        {
            _overdriveActiveTimer -= delta;
            _playerState.Speed = 400f; 
        }
        else
        {
            _playerState.Speed = 250f; 
            if (keyboard.IsKeyDown(Keys.Space) && _overdriveCharge >= MaxOverdriveCharge)
            {
                _overdriveActiveTimer = OverdriveDuration;
                _overdriveCharge = 0f; 
            }
        }

        // Movement
        float mx = (keyboard.IsKeyDown(Keys.D) ? 1f : 0f) - (keyboard.IsKeyDown(Keys.A) ? 1f : 0f);
        float my = (keyboard.IsKeyDown(Keys.S) ? 1f : 0f) - (keyboard.IsKeyDown(Keys.W) ? 1f : 0f);
        Vector2 desired = _playerState.Position + new Vector2(mx, my) * _playerState.Speed * delta;
        if (IsInArena(desired, _playerState.Size))
            _playerState.Position = desired;

        _arena.Update(delta, _playerState.Position, _combatManager);
        
        if (isOverdriveActive)
        {
            // Instakill everything in the ring
            _arena.ApplyInstakillSweep(_playerState.Position, BladeInnerRadius, BladeOuterRadius, _combatManager);
        }
        else
        {
            _arena.ApplyBladeSweep(_playerState.Position, BladeInnerRadius, BladeOuterRadius, _combatManager);
        }

        CheckContactDamage();
        UpdateGun(delta, mouse);
        UpdateBullets(delta);
        UpdatePupil(mouse);
        _combatManager.UpdateExplosionEffects(delta);

        if (_arena.Waves.WavesComplete && _arena.Enemies.Count == 0)
        {
            _cleared = true;
            _clearedTimer = 0f;
        }
    }

    private bool IsInArena(Vector2 position, int size)
        => Vector2.Distance(position, _arena.Center) + size / 2f < _arena.Radius;

        private void CheckContactDamage()
    {
        if (_overdriveActiveTimer > 0f) return; // Invincible in Overdrive
        if (_contactDamageCooldown > 0f) return; 

        Rectangle playerRect = new Rectangle(
            (int)_playerState.Position.X - _playerState.Size / 2,
            (int)_playerState.Position.Y - _playerState.Size / 2,
            _playerState.Size, _playerState.Size);

        foreach (Enemy e in _arena.Enemies)
        {
            Rectangle r = new Rectangle(
                (int)e.Position.X - e.Definition.Size / 2,
                (int)e.Position.Y - e.Definition.Size / 2,
                e.Definition.Size, e.Definition.Size);
                
            if (!playerRect.Intersects(r)) continue;

            // Enemies hit the SAW for heavy damage. You must back off.
            int damage = e.Definition.Name == "Armor Bug" ? 5 : 15;
            _playerState.Armor = Math.Max(0, _playerState.Armor - damage);
            
            if (_playerState.Armor <= 0) { _playerState.IsDead = true; return; }
            
            _contactDamageCooldown = 0.25f; // Take damage 4 times a second if swarmed
            break; 
        }
    }

    private void UpdateGun(float delta, MouseState mouse)
    {
        bool holdingFire = mouse.LeftButton == ButtonState.Pressed;

        float refillRate = holdingFire ? _rapidFireRefillPerSecond * 0.2f : _rapidFireRefillPerSecond;
        if (_rapidFireCharge < MaxRapidFireCharge)
            _rapidFireCharge = Math.Min(MaxRapidFireCharge, _rapidFireCharge + refillRate * delta);

        if (holdingFire)
        {
            _rapidFireShotTimer -= delta;
            if (_rapidFireShotTimer <= 0f)
            {
                Shoot(mouse);
                if (_rapidFireCharge > 0f)
                {
                    _rapidFireCharge = Math.Max(0f, _rapidFireCharge - _rapidFireDrainPerShot);
                    _rapidFireShotTimer = _rapidFireShotInterval; 
                }
                else
                {
                    _rapidFireShotTimer = 0.25f; 
                }
            }
        }
        else
        {
            _rapidFireShotTimer = 0f; 
        }
        _previousMouse = mouse;
    }

    private void Shoot(MouseState mouse)
    {
        Vector2 sc = new Vector2(GraphicsDevice.Viewport.Width / 2f, GraphicsDevice.Viewport.Height / 2f);
        Vector2 world = _playerState.Position + (new Vector2(mouse.X, mouse.Y) - sc);
        Vector2 dir = world - _playerState.Position;
        if (dir.LengthSquared() > 0.01f) dir.Normalize();
        
        _bulletPositions.Add(_playerState.Position);
        _bulletVelocities.Add(dir * BulletSpeed);
    }

    private void UpdateBullets(float delta)
    {
        for (int i = _bulletPositions.Count - 1; i >= 0; i--)
        {
            _bulletPositions[i] += _bulletVelocities[i] * delta;
            Rectangle bulletRect = new Rectangle(
                (int)_bulletPositions[i].X - BulletSize / 2,
                (int)_bulletPositions[i].Y - BulletSize / 2,
                BulletSize, BulletSize);

            bool hit = false;
            for (int j = _arena.Enemies.Count - 1; j >= 0; j--)
            {
                Enemy e = _arena.Enemies[j];
                Rectangle r = new Rectangle(
                    (int)e.Position.X - e.Definition.Size / 2,
                    (int)e.Position.Y - e.Definition.Size / 2,
                    e.Definition.Size, e.Definition.Size);

                if (bulletRect.Intersects(r))
                {
                    e.Health -= BulletDamage;
                    if (e.Health <= 0)
                    {
                        bool isRed = e.Definition.Name == "Red Bug";
                        Vector2 pos = e.Position;
                        string reward = e.Definition.RewardType;
                        _arena.Enemies.RemoveAt(j);
                        
                                               // Give Overdrive charge for ALL bullet kills
                        _arena.OnEnemyKilledReward?.Invoke(reward); 
                        
                        // Still detonate the Red Bug if it died
                        if (isRed) _arena.OnRedBugExplosion?.Invoke(pos);
                    }
                    hit = true;
                    break;
                }
            }

            if (hit || _bulletPositions[i].LengthSquared() > 1_000_000f)
            {
                _bulletPositions.RemoveAt(i);
                _bulletVelocities.RemoveAt(i);
            }
        }
    }

    private void UpdatePupil(MouseState mouse)
    {
        Vector2 sc = new Vector2(GraphicsDevice.Viewport.Width / 2f, GraphicsDevice.Viewport.Height / 2f);
        Vector2 toMouse = new Vector2(mouse.X, mouse.Y) - sc;
        if (toMouse.LengthSquared() > 0.01f)
        {
            float d = MathF.Min(toMouse.Length(), PupilMaxOffset);
            toMouse.Normalize();
            _pupilOffset = toMouse * d;
        }
    }

       private void GrantReward(string type)
    {
        // 1. Fill Overdrive FAST on any kill (both bullets and blade)
        if (_overdriveActiveTimer <= 0f)
            _overdriveCharge = Math.Min(MaxOverdriveCharge, _overdriveCharge + 5f); 

        // 2. Grant the actual reward
        switch (type)
        {
            case "armor":
                _playerState.Armor = Math.Min(_playerState.MaxArmor, _playerState.Armor + 5);
                break;
            case "xp":
            default:
                _score += 1;
                break;
        }
    }
    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(20, 5, 15));

        Vector2 camOffset = _playerState.Position
            - new Vector2(GraphicsDevice.Viewport.Width / 2f, GraphicsDevice.Viewport.Height / 2f);

        SpriteBatch.Begin(transformMatrix: Matrix.CreateTranslation(-camOffset.X, -camOffset.Y, 0f));

        _arena.Draw(SpriteBatch, Pixel, _enemyAnimations);
        _combatManager.DrawExplosionEffects(SpriteBatch, Pixel);

        Color bladeColor = _overdriveActiveTimer > 0f ? Color.Red : Color.White;

        Vector2 bladeOrigin = new Vector2(_bladeTexture.Width / 2f, _bladeTexture.Height / 2f);
        SpriteBatch.Draw(_bladeTexture, _playerState.Position, null, bladeColor,
            _combatManager.SawAngle, bladeOrigin,
            (BladeOuterRadius * 2) / _bladeTexture.Width, SpriteEffects.None, 0f);

        Vector2 socketOrigin = new Vector2(_eyeSocketTexture.Width / 2f - 24.5f, _eyeSocketTexture.Height / 2f - 68.5f);
        SpriteBatch.Draw(_eyeSocketTexture, _playerState.Position, null, Color.White, 0f,
            socketOrigin, EyeSocketScale * _playerState.Size / _eyeSocketTexture.Width,
            SpriteEffects.None, 0f);

        Vector2 pupilOrigin = new Vector2(_eyePupilTexture.Width / 2f - 27f, _eyePupilTexture.Height / 2f - 55f);
        float pupilScale = _playerState.Size / (float)_eyeSocketTexture.Width;
        SpriteBatch.Draw(_eyePupilTexture, _playerState.Position + _pupilOffset, null, Color.White, 0f,
            pupilOrigin, pupilScale, SpriteEffects.None, 0f);

        foreach (Vector2 p in _bulletPositions)
        {
            Rectangle r = new Rectangle((int)p.X - BulletSize / 2, (int)p.Y - BulletSize / 2, BulletSize, BulletSize);
            SpriteBatch.Draw(Pixel, r, Color.Yellow);
        }

        SpriteBatch.End();

        SpriteBatch.Begin();
        DrawHud();
        SpriteBatch.End();
    }

    private void DrawHud()
    {
        int barW = 200, barH = 20;
        
        SpriteBatch.Draw(Pixel, new Rectangle(10, 40, barW, barH), Color.Gray);
        int armorW = (int)((float)_playerState.Armor / _playerState.MaxArmor * barW);
        SpriteBatch.Draw(Pixel, new Rectangle(10, 40, armorW, barH), Color.LimeGreen);

        SpriteBatch.Draw(Pixel, new Rectangle(10, 70, barW, 14), Color.DarkGray);
        int overW = (int)(_overdriveCharge / MaxOverdriveCharge * barW);
        SpriteBatch.Draw(Pixel, new Rectangle(10, 70, overW, 14), Color.Cyan);
        
        if (_overdriveCharge >= MaxOverdriveCharge)
            SpriteBatch.DrawString(Font, "OVERDRIVE READY! [SPACE]", new Vector2(220, 65), Color.Cyan);
        else if (_overdriveActiveTimer > 0f)
            SpriteBatch.DrawString(Font, $"OVERDRIVE ACTIVE: {_overdriveActiveTimer:F1}s", new Vector2(220, 65), Color.Red);

        SpriteBatch.Draw(Pixel, new Rectangle(10, 90, barW, 14), Color.DarkGray);
        int rapidW = (int)(_rapidFireCharge / MaxRapidFireCharge * barW);
        SpriteBatch.Draw(Pixel, new Rectangle(10, 90, rapidW, 14), Color.Magenta);

        SpriteBatch.DrawString(Font, $"Wave {_arena.Waves.CurrentWave}/{_arena.Waves.TotalWaves}",
            new Vector2(10, 10), Color.White);
        SpriteBatch.DrawString(Font, $"Spin: {_combatManager.Spin:F2} rad/s",
            new Vector2(10, 115), Color.Violet);
        SpriteBatch.DrawString(Font, $"Enemies: {_arena.Enemies.Count}  |  Kills: {_combatManager.TotalKills}  |  Score: {_score}",
            new Vector2(10, 135), Color.White);

        MouseState m = Mouse.GetState();
        Rectangle target = new Rectangle(m.X - 48, m.Y - 48, 96, 96);
        SpriteBatch.Draw(_targetTexture, target, Color.White);

        if (_cleared || _playerState.IsDead)
        {
            Rectangle overlay = new Rectangle(0, 0, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
            SpriteBatch.Draw(Pixel, overlay, Color.Black * 0.7f);
            string txt = _cleared ? "HIVE CLEARED" : "DEFEATED";
            Color col = _cleared ? Color.LimeGreen : Color.Red;
            Vector2 size = Font.MeasureString(txt) * 2f;
            SpriteBatch.DrawString(Font, txt, new Vector2(GraphicsDevice.Viewport.Width / 2f - size.X / 2f, GraphicsDevice.Viewport.Height / 2f - size.Y / 2f), col, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);
        }
    }
}