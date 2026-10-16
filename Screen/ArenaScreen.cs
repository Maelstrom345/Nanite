using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Nanite;

internal sealed class ArenaScreen : Screen
{
    // ---- Arena ----
    private readonly Arena _arena;

    // ---- Player / combat state ----
    private readonly PlayerState _playerState = new PlayerState();
    private readonly CombatManager _combatManager = new CombatManager();
    private Polarity _bladePolarity = Polarity.Positive;
    private float _polarityCharge = 0f;
    private const int MaxPolarityCharge = 100;
    private const float PolarityChargeDrainPerSecond = 10f;

    // ---- Blade ----
    private const float BladeInnerRadius = 60f;
    private const float BladeOuterRadius = 90f;

    // ---- Bullets ----
    private readonly List<Vector2> _bulletPositions = new();
    private readonly List<Vector2> _bulletVelocities = new();
    private const float BulletSpeed = 650f;
    private const int BulletSize = 8;

    // ---- Gun ----
    private float _gunCooldownTimer = 0f;
    private const float GunCooldownDuration = 0.18f;
    private MouseState _previousMouse;
    private ButtonState _previousRightMouseButton = ButtonState.Released;

    // ---- Textures ----
    private readonly Texture2D _bladeTexture;
    private readonly Texture2D _eyeSocketTexture;
    private readonly Texture2D _eyePupilTexture;
    private readonly Texture2D _targetTexture;
    private readonly Dictionary<string, List<Texture2D>> _enemyAnimations;

    private const float EyeSocketScale = 1.5f;

    public ArenaScreen(Game1 game, HiveData hive) : base(game)
    {
        _arena = new Arena(hive, BladeOuterRadius);

        // Load textures
        _bladeTexture = game.Content.Load<Texture2D>("Blade1_Frame1");
        _eyeSocketTexture = game.Content.Load<Texture2D>("eye_socket");
        _eyePupilTexture = game.Content.Load<Texture2D>("eye_pupil");
        _targetTexture = game.Content.Load<Texture2D>("Target_icon");

        // Load enemy animations
        _enemyAnimations = new Dictionary<string, List<Texture2D>>();
        var redFrames = new List<Texture2D>();
        for (int i = 1; i <= 9; i++)
            redFrames.Add(game.Content.Load<Texture2D>($"Red_bug_frame{i}"));
        _enemyAnimations["Red Bug"] = redFrames;

        var greenFrames = new List<Texture2D>();
        for (int i = 1; i <= 8; i++)
            greenFrames.Add(game.Content.Load<Texture2D>($"Green_bug_frame{i}"));
        _enemyAnimations["Armor Bug"] = greenFrames;

        var purpleFrames = new List<Texture2D>();
        for (int i = 1; i <= 7; i++)
            purpleFrames.Add(game.Content.Load<Texture2D>($"Purple_bug_frame{i}"));
        _enemyAnimations["Rapidfire Bug"] = purpleFrames;

        var yellowFrames = new List<Texture2D>();
        for (int i = 1; i <= 6; i++)
            yellowFrames.Add(game.Content.Load<Texture2D>($"Yellow_bug_frame{i}"));
        _enemyAnimations["Yellow Bug"] = yellowFrames;

        // Place the player at the arena center
        _playerState.Position = _arena.Center;
    }

    public override void Update(GameTime gameTime, KeyboardState keyboard, MouseState mouse)
    {
        float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Update arena
        _arena.Update(delta, _playerState.Position, _bladePolarity);

        // Polarity switch (right-click)
        if (mouse.RightButton == ButtonState.Pressed && _previousRightMouseButton == ButtonState.Released)
        {
            if (_bladePolarity == Polarity.Negative)
                _bladePolarity = Polarity.Positive;
            else if (_polarityCharge > 0f)
                _bladePolarity = Polarity.Negative;
        }
        _previousRightMouseButton = mouse.RightButton;

        // Polarity drain
        if (_bladePolarity == Polarity.Negative)
        {
            _polarityCharge = MathF.Max(0f, _polarityCharge - PolarityChargeDrainPerSecond * delta);
            if (_polarityCharge <= 0f)
                _bladePolarity = Polarity.Positive;
        }

        // Player movement
        float moveX = (keyboard.IsKeyDown(Keys.D) ? 1f : 0f) - (keyboard.IsKeyDown(Keys.A) ? 1f : 0f);
        float moveY = (keyboard.IsKeyDown(Keys.S) ? 1f : 0f) - (keyboard.IsKeyDown(Keys.W) ? 1f : 0f);
        Vector2 desired = _playerState.Position + new Vector2(moveX, moveY) * _playerState.Speed * delta;
        if (IsInArena(desired, _playerState.Size))
            _playerState.Position = desired;

        // Buzzsaw (blade damage to enemies)
        

        // Shooting
        UpdateGun(delta, mouse);

        // Bullets
        UpdateBullets(delta);

        // TEMP: press Q to exit back to menu
        if (keyboard.IsKeyDown(Keys.Q))
        {
            Game.SwitchScreen(new MenuScreen(Game));
        }
    }

    private bool IsInArena(Vector2 position, int size)
    {
        return Vector2.Distance(position, _arena.Center) + size / 2f < _arena.Radius;
    }

    private void UpdateGun(float delta, MouseState mouse)
    {
        if (_gunCooldownTimer > 0)
            _gunCooldownTimer -= delta;

        bool justClicked = mouse.LeftButton == ButtonState.Pressed &&
                           _previousMouse.LeftButton == ButtonState.Released;
        if (justClicked && _gunCooldownTimer <= 0)
        {
            Shoot(mouse);
            _gunCooldownTimer = GunCooldownDuration;
        }

        _previousMouse = mouse;
    }

    private void Shoot(MouseState mouse)
    {
        Vector2 mouseWorld = new Vector2(mouse.X, mouse.Y) + _playerState.Position
                             - new Vector2(GraphicsDevice.Viewport.Width / 2f, GraphicsDevice.Viewport.Height / 2f);

        Vector2 direction = mouseWorld - _playerState.Position;
        if (direction.LengthSquared() > 0.01f) direction.Normalize();

        _bulletPositions.Add(_playerState.Position);
        _bulletVelocities.Add(direction * BulletSpeed);
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
                Enemy enemy = _arena.Enemies[j];
                Rectangle enemyRect = new Rectangle(
                    (int)enemy.Position.X - enemy.Definition.Size / 2,
                    (int)enemy.Position.Y - enemy.Definition.Size / 2,
                    enemy.Definition.Size, enemy.Definition.Size);

                if (bulletRect.Intersects(enemyRect))
                {
                    enemy.Health /= 5;
                    if (enemy.Health <= 0)
                        _arena.Enemies.RemoveAt(j);
                    hit = true;
                    break;
                }
            }

            if (hit || _bulletPositions[i].LengthSquared() > 500_000f)
            {
                _bulletPositions.RemoveAt(i);
                _bulletVelocities.RemoveAt(i);
            }
        }
    }

    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(20, 5, 15));

        // Arena is centered on origin; camera would follow player. For now,
        // we render everything manually with an offset so the player is centered.
        Vector2 cameraOffset = _playerState.Position
            - new Vector2(GraphicsDevice.Viewport.Width / 2f, GraphicsDevice.Viewport.Height / 2f);

        SpriteBatch.Begin(transformMatrix: Matrix.CreateTranslation(-cameraOffset.X, -cameraOffset.Y, 0f));

        _arena.Draw(SpriteBatch, Pixel, _enemyAnimations);

        // Blade
        Vector2 bladeOrigin = new Vector2(_bladeTexture.Width / 2f, _bladeTexture.Height / 2f);
        SpriteBatch.Draw(_bladeTexture, _playerState.Position, null, Color.White,
            _combatManager.SawAngle, bladeOrigin,
            (BladeOuterRadius * 2) / _bladeTexture.Width, SpriteEffects.None, 0f);

        // Eye socket
        Vector2 socketOrigin = new Vector2(_eyeSocketTexture.Width / 2f - 24.5f, _eyeSocketTexture.Height / 2f - 68.5f);
        SpriteBatch.Draw(_eyeSocketTexture, _playerState.Position, null, Color.White, 0f,
            socketOrigin, EyeSocketScale * _playerState.Size / _eyeSocketTexture.Width,
            SpriteEffects.None, 0f);

        // Pupil
        Vector2 pupilOrigin = new Vector2(_eyePupilTexture.Width / 2f - 27f, _eyePupilTexture.Height / 2f - 55f);
        float pupilScale = _playerState.Size / (float)_eyeSocketTexture.Width;
        SpriteBatch.Draw(_eyePupilTexture, _playerState.Position, null, Color.White, 0f,
            pupilOrigin, pupilScale, SpriteEffects.None, 0f);

        // Bullets
        foreach (Vector2 p in _bulletPositions)
        {
            Rectangle r = new Rectangle((int)p.X - BulletSize / 2, (int)p.Y - BulletSize / 2,
                BulletSize, BulletSize);
            SpriteBatch.Draw(Pixel, r, Color.Yellow);
        }

        SpriteBatch.End();

        // HUD
        SpriteBatch.Begin();
        SpriteBatch.DrawString(Font, "Press Q to exit", new Vector2(10, 10), Color.White);
        SpriteBatch.End();
    }
}