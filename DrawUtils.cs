using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace MergeControl;

/// <summary>
/// Some drawing utilities, borrowed from a previous mod.
/// </summary>
public class DrawUtils
{
	/// <summary>
	///     Draw a tile overlay over a large area, mimicking the style of smart cursor.<br />
	///     Adapted from <see cref="Main"/>.DrawSmartCursor() by DavidFDev.
	/// </summary>
	/// <param name="tileRect">Tile rectangle to draw the overlay.</param>
	/// <param name="insideColour">Colour of the overlay's inside. Default: (0.6f, 0.54f, 0.06f, 0.36f).</param>
	/// <param name="borderColour">Colour of the overlay's border. Default: (1f, 0.95f, 0.3f, 1f).</param>
	/// <param name="borderWidth">Pixel width of the overlay's border. Default: 2f.</param>
	internal static void DrawRectangle(Rectangle tileRect, Color? insideColour = null, Color? borderColour = null, float borderWidth = 2f)
	{
		if (Main.PlayerLoaded is false || Main.LocalPlayer.dead || tileRect.IsEmpty)
			return;

		borderWidth = Math.Max(0f, borderWidth);

		// Determine smart cursor screen position
		var smartCursorWorldPos = new Vector2(tileRect.X, tileRect.Y) * 16f;
		var smartCursorScreenPos = smartCursorWorldPos - Main.screenPosition;

		if (Main.LocalPlayer.gravDir < 0f)
			smartCursorScreenPos.Y = (float)(Main.screenHeight - smartCursorScreenPos.Y - 16.0);

		var rect = new Rectangle(0, 0, 1, 1);

		// Begin the sprite batch
		Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);

		// Draw inside
		if (insideColour is not null)
		{
			var cA = insideColour.GetValueOrDefault(new Color(0.6f, 0.54f, 0.06f, 0.36f));

			if (cA.A > 0)
			{
				Main.spriteBatch.Draw(
				TextureAssets.MagicPixel.Value, smartCursorScreenPos, rect, cA, 0f, Vector2.Zero, tileRect.Size() * 16f,
				SpriteEffects.None, 0
				);
			}
		}

		// Draw border-
		if (borderColour is not null)
		{
			var cB = borderColour.GetValueOrDefault(new Color(1f, 0.95f, 0.3f, 1f));

			if (cB.A > 0 && borderWidth > 0f)
			{
				var scale = tileRect.Size() * 16f;

				Main.spriteBatch.Draw(
				TextureAssets.MagicPixel.Value, smartCursorScreenPos + Vector2.UnitX * -borderWidth, rect, cB, 0f,
				Vector2.Zero, new Vector2(borderWidth, scale.Y), SpriteEffects.None, 0f
				);

				Main.spriteBatch.Draw(
				TextureAssets.MagicPixel.Value, smartCursorScreenPos + Vector2.UnitX * scale.X, rect, cB, 0f,
				Vector2.Zero, new Vector2(borderWidth, scale.Y), SpriteEffects.None, 0f
				);

				Main.spriteBatch.Draw(
				TextureAssets.MagicPixel.Value, smartCursorScreenPos + Vector2.UnitY * -borderWidth, rect, cB, 0f,
				Vector2.Zero, new Vector2(scale.X, borderWidth), SpriteEffects.None, 0f
				);

				Main.spriteBatch.Draw(
				TextureAssets.MagicPixel.Value, smartCursorScreenPos + Vector2.UnitY * scale.Y, rect, cB, 0f,
				Vector2.Zero, new Vector2(scale.X, borderWidth), SpriteEffects.None, 0f
				);
			}
		}

		// End the sprite batch
		Main.spriteBatch.End();
	}
}