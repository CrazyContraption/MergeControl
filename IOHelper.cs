using Ionic.Zlib;
using Microsoft.Xna.Framework;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace MergeControl
{
	/// <summary>
	/// Some IO utilities for drawing - and more importantly - world tile data saving and loading.
	/// </summary>
	internal class WorldIO : ModSystem
	{
		public override void PostDrawTiles()
		{
			if (Main.netMode is not NetmodeID.Server && MergeControl.Selection.IsEmpty is false)
				DrawUtils.DrawRectangle(
					new Rectangle(MergeControl.Selection.X, MergeControl.Selection.Y, MergeControl.Selection.Width, MergeControl.Selection.Height),
					borderColour: Color.White,
					borderWidth: 2.5f
				);
		}

		public override void SaveWorldData(TagCompound tag)
		{
			byte[] infoData = TransformData(Main.tile.GetData<TileInfo>());

			// Compress the data
			tag["merging"] = IOHelper.Compress(infoData, CompressionLevel.BestSpeed);
		}

		public override void LoadWorldData(TagCompound tag)
		{
			if (tag.GetByteArray("merging") is byte[] infoData)
				// Decompress the data
				TransformData(IOHelper.Decompress(infoData, CompressionLevel.BestSpeed), Main.tile.GetData<TileInfo>());
		}

		/// <summary>
		/// Transforms <paramref name="data"/> into a byte array.<br/>
		/// Used for writing to world storage.
		/// </summary>
		/// <param name="data">The <paramref name="data"/> array to be transformed.</param>
		/// <returns>The "type-less" byte array.</returns>
		private static byte[] TransformData(TileInfo[] data)
		{
			byte[] converted = new byte[data.Length];

			unsafe
			{
				fixed (TileInfo* fixedPtr = data) fixed (byte* fixedConvPtr = converted)
				{
					TileInfo* ptr = fixedPtr;
					byte* convPtr = fixedConvPtr;
					int length = data.Length;

					for (int i = 0; i < length; i++, ptr++, convPtr++)
						*convPtr = ptr->mergeData;
				}
			}

			return converted;
		}

		/// <summary>
		/// Transforms <paramref name="data"/> from a byte array into a <seealso cref="TileInfo"/> array.<br/>
		/// Used for reading from world storage.
		/// </summary>
		/// <param name="data">The <paramref name="data"/> array to be transformed.</param>
		/// <param name="existing">The transformed <paramref name="data"/>, as a <seealso cref="TileInfo"/> array.</param>
		private static void TransformData(byte[] data, TileInfo[] existing)
		{
			if (data.Length != existing.Length)
			{
				//SerousMachines.Instance.Logger.Warn($"Saved data length ({data.Length}) did not match the world data length ({existing.Length}).  Data will not be loaded.");
				return;
			}

			unsafe
			{
				fixed (byte* fixedPtr = data) fixed (TileInfo* fixedConvPtr = existing)
				{
					byte* ptr = fixedPtr;
					TileInfo* convPtr = fixedConvPtr;
					int length = data.Length;

					for (int i = 0; i < length; i++, ptr++, convPtr++)
						convPtr->mergeData = *ptr;
				}
			}
		}
	}

	public static class IOHelper
	{
		/// <summary>
		/// Compresses <paramref name="data"/> at the provided <paramref name="level"/>.
		/// </summary>
		/// <param name="data">The decompressed byte array.</param>
		/// <param name="level">The compression level.</param>
		/// <returns>The compressed byte array.</returns>
		public static byte[] Compress(byte[] data, CompressionLevel level)
		{
			using MemoryStream decompressed = new(data);
			using DeflateStream compression = new(decompressed, CompressionMode.Compress, level);
			using MemoryStream compressed = new();
			compression.CopyTo(compressed);
			return compressed.ToArray();
		}

		/// <summary>
		/// Decompresses <paramref name="data"/> at the provided <paramref name="level"/>.
		/// </summary>
		/// <param name="data">The compressed byte array.</param>
		/// <param name="level">The compression level.</param>
		/// <returns>The decompressed byte array.</returns>
		public static byte[] Decompress(byte[] data, CompressionLevel level)
		{
			using MemoryStream compressed = new(data);
			using DeflateStream decompression = new(compressed, CompressionMode.Decompress, level);
			using MemoryStream decompressed = new();
			decompression.CopyTo(decompressed);
			return decompressed.ToArray();
		}
	}
}