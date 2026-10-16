using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Nanite;

internal sealed class UIManager
{
    public void DrawMenu(SpriteBatch spriteBatch, SpriteFont font, Texture2D eyeSocket,
        Texture2D eyePupil, float zoomScale, float eyeSocketScale, int playerSize,
        float pupilMaxOffset, float animationTime, int viewportWidth, int viewportHeight,
        bool isZoomingOut)
    {
        Vector2 menuCenter = new Vector2(viewportWidth / 2f, viewportHeight / 2f);
        Vector2 socketOrigin = new Vector2(eyeSocket.Width / 2f - 24.5f, eyeSocket.Height / 2f - 68.5f);
        float socketScale = zoomScale * eyeSocketScale * playerSize / eyeSocket.Width;
        spriteBatch.Draw(eyeSocket, menuCenter, null, Color.White, 0f, socketOrigin,
            socketScale, SpriteEffects.None, 0f);

        Vector2 pupilOffset = new Vector2(
            MathF.Cos(animationTime),
            MathF.Sin(animationTime * 0.7f)) * (pupilMaxOffset * zoomScale * 0.5f);
        Vector2 pupilOrigin = new Vector2(eyePupil.Width / 2f - 27f, eyePupil.Height / 2f - 55f);
        float pupilScale = zoomScale * playerSize / (float)eyeSocket.Width;
        spriteBatch.Draw(eyePupil, menuCenter + pupilOffset, null, Color.White, 0f,
            pupilOrigin, pupilScale, SpriteEffects.None, 0f);

        if (!isZoomingOut)
        {
            const string prompt = "Press ENTER";
            Vector2 size = font.MeasureString(prompt);
            spriteBatch.DrawString(font, prompt,
                new Vector2(menuCenter.X - size.X / 2f, menuCenter.Y + 150f), Color.White);
        }
    }

    public void DrawHud(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font,
        PlayerState player, CombatManager combat, EnemyManager enemies,
        int preferredWidth, int preferredHeight, float polarityChargeDrainPerSecond)
    {
        spriteBatch.DrawString(font, "Score: " + player.Score + "  XP: " + player.Experience,
            new Vector2(10, 10), Color.White);

        const int barWidth = 200;
        const int barHeight = 20;
        spriteBatch.Draw(pixel, new Rectangle(10, 40, barWidth, barHeight), Color.Gray);
        int filledWidth = (int)((float)player.Armor / player.MaxArmor * barWidth);
        spriteBatch.Draw(pixel, new Rectangle(10, 40, filledWidth, barHeight),
            player.Armor <= player.ArmorBreakThreshold ? Color.DarkRed : Color.LimeGreen);

        spriteBatch.Draw(pixel, new Rectangle(10, 70, barWidth, 14), Color.DarkGray);
        int ammoFilledWidth = (int)((float)player.Ammo / player.MaxAmmo * barWidth);
        spriteBatch.Draw(pixel, new Rectangle(10, 70, ammoFilledWidth, 14), Color.Orange);

        spriteBatch.Draw(pixel, new Rectangle(10, 90, barWidth, 14), Color.DarkGray);
        int chargeFilledWidth = (int)(player.RapidFireCharge / player.MaxRapidFireCharge * barWidth);
        spriteBatch.Draw(pixel, new Rectangle(10, 90, chargeFilledWidth, 14), Color.Purple);

        if (player.IsDead)
        {
            spriteBatch.Draw(pixel, new Rectangle(0, 0, preferredWidth, preferredHeight), Color.Black * 0.7f);
            const string deathText = "YOU DIED";
            Vector2 deathSize = font.MeasureString(deathText) * 2f;
            spriteBatch.DrawString(font, deathText,
                new Vector2(preferredWidth / 2f - deathSize.X / 2f,
                    preferredHeight / 2f - deathSize.Y / 2f),
                Color.Red, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);

            const string restartText = "Press R to restart";
            Vector2 restartSize = font.MeasureString(restartText);
            spriteBatch.DrawString(font, restartText,
                new Vector2(preferredWidth / 2f - restartSize.X / 2f,
                    preferredHeight / 2f + 40f), Color.White);
        }

        spriteBatch.DrawString(font, "Kills: " + combat.TotalKills, new Vector2(10, 120), Color.White);
        spriteBatch.DrawString(font,
            "Red spawn stage: " + enemies.GetCurrentRedBugSpawnStage(combat.TotalKills) + "/" +
            EnemyManager.RedBugSpawnStageCount + " (" +
            enemies.GetRedBugSpawnChancePercent(combat.TotalKills).ToString("F0") + "%)",
            new Vector2(10, 145), Color.Red);

        string bladeChargeText = player.BladePolarity == Polarity.Positive ? "Positive" : "Negative";
        Color bladeChargeColor = player.BladePolarity == Polarity.Positive ? Color.Cyan : Color.OrangeRed;
        spriteBatch.DrawString(font,
            "Blade: " + bladeChargeText + "  Charge: " + player.PolarityCharge.ToString("F0") +
            "/" + player.MaxPolarityCharge,
            new Vector2(10, 170), bladeChargeColor);
        spriteBatch.Draw(pixel, new Rectangle(10, 194, 200, 16), Color.DarkGray);
        int polarityMeterWidth = (int)((float)player.PolarityCharge / player.MaxPolarityCharge * 200);
        spriteBatch.Draw(pixel, new Rectangle(10, 194, polarityMeterWidth, 16), bladeChargeColor);
        spriteBatch.DrawString(font,
            "Right-click: switch  Charge drain: " + polarityChargeDrainPerSecond.ToString("F0") + "/s",
            new Vector2(10, 214), Color.White);
    }

    public void DrawWorldMap(SpriteBatch spriteBatch, Texture2D pixel, WorldMap map,
        bool showWorldMap, Vector2 playerPosition, bool showRegionBoundaries,
        int viewportWidth, int viewportHeight)
    {
        if (!showWorldMap) return;

        int mapSize = System.Math.Min(560, System.Math.Min(viewportWidth, viewportHeight) - 40);
        if (mapSize <= 0) return;

        Rectangle mapBounds = new Rectangle(
            (viewportWidth - mapSize) / 2,
            (viewportHeight - mapSize) / 2,
            mapSize,
            mapSize);
        map.Draw(spriteBatch, pixel, mapBounds, playerPosition, showRegionBoundaries);
    }

    public void DrawHiveEntryPrompt(SpriteBatch spriteBatch, Texture2D pixel,
        SpriteFont font, HiveData hive, int viewportWidth, int viewportHeight)
    {
        if (hive == null) return;
        string prompt = $"E  Enter {hive.Type} Hive";
        Vector2 textSize = font.MeasureString(prompt);
        Rectangle panel = new Rectangle(
            (viewportWidth - (int)textSize.X - 28) / 2,
            viewportHeight - 84,
            (int)textSize.X + 28,
            (int)textSize.Y + 16);
        Color accent = hive.Type == HiveType.Dangerous ? Color.OrangeRed : Color.Cyan;
        spriteBatch.Draw(pixel, panel, Color.Black * 0.85f);
        spriteBatch.DrawString(font, prompt, new Vector2(panel.X + 14, panel.Y + 8), accent);
    }

    public void DrawOverworldHiveGuide(SpriteBatch spriteBatch, Texture2D pixel,
        SpriteFont font, HiveData hive, Vector2 playerPosition, Camera2D camera,
        int viewportWidth, int viewportHeight)
    {
        if (hive == null) return;

        Color accent = hive.Type == HiveType.Dangerous ? Color.OrangeRed : Color.Cyan;
        Vector2 screenCenter = new Vector2(viewportWidth / 2f, viewportHeight / 2f);
        Vector2 hiveScreenPosition = camera.WorldToScreen(hive.WorldPosition);
        Vector2 direction = hiveScreenPosition - screenCenter;

        if (direction.LengthSquared() > 0.01f)
        {
            direction.Normalize();
            const float edgeMargin = 38f;
            float availableX = Math.Max(1f, viewportWidth / 2f - edgeMargin);
            float availableY = Math.Max(1f, viewportHeight / 2f - edgeMargin);
            float scaleX = MathF.Abs(direction.X) > 0.001f
                ? availableX / MathF.Abs(direction.X)
                : float.PositiveInfinity;
            float scaleY = MathF.Abs(direction.Y) > 0.001f
                ? availableY / MathF.Abs(direction.Y)
                : float.PositiveInfinity;
            Vector2 arrowTip = screenCenter + direction * MathF.Min(scaleX, scaleY);
            Vector2 arrowTail = arrowTip - direction * 36f;
            Vector2 perpendicular = new Vector2(-direction.Y, direction.X);

            DrawHudLine(spriteBatch, pixel, arrowTail, arrowTip, Color.Black, 11f);
            DrawHudLine(spriteBatch, pixel, arrowTail, arrowTip, accent, 6f);
            DrawHudLine(spriteBatch, pixel, arrowTip,
                arrowTip - direction * 19f + perpendicular * 13f, Color.Black, 11f);
            DrawHudLine(spriteBatch, pixel, arrowTip,
                arrowTip - direction * 19f - perpendicular * 13f, Color.Black, 11f);
            DrawHudLine(spriteBatch, pixel, arrowTip,
                arrowTip - direction * 19f + perpendicular * 13f, accent, 6f);
            DrawHudLine(spriteBatch, pixel, arrowTip,
                arrowTip - direction * 19f - perpendicular * 13f, accent, 6f);
        }

        float distance = Vector2.Distance(playerPosition, hive.WorldPosition);
        string label = $"{hive.Type.ToString().ToUpperInvariant()} HIVE  {distance:F0}m";
        Vector2 labelSize = font.MeasureString(label);
        Rectangle panel = new Rectangle(
            (viewportWidth - (int)labelSize.X - 24) / 2,
            242,
            (int)labelSize.X + 24,
            (int)labelSize.Y + 12);
        spriteBatch.Draw(pixel, panel, Color.Black * 0.8f);
        spriteBatch.DrawString(font, label,
            new Vector2(panel.X + 12, panel.Y + 6), accent);
    }

    private static void DrawHudLine(SpriteBatch spriteBatch, Texture2D pixel,
        Vector2 start, Vector2 end, Color color, float thickness)
    {
        Vector2 difference = end - start;
        float length = difference.Length();
        if (length <= 0f) return;
        spriteBatch.Draw(pixel, start, null, color, MathF.Atan2(difference.Y, difference.X),
            Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
    }

    public void DrawTargetCursor(SpriteBatch spriteBatch, Texture2D targetTexture, MouseState mouse)
    {
        const int targetSize = 96;
        Rectangle targetRect = new Rectangle(
            mouse.X - targetSize / 2,
            mouse.Y - targetSize / 2,
            targetSize,
            targetSize);
        spriteBatch.Draw(targetTexture, targetRect, Color.White);
    }
}