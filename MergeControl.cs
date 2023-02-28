using Terraria;
using Terraria.ModLoader;

namespace MergeControl
{
	public class MergeControl : Mod
	{
		internal static bool? GetMerging(int x, int y)
        {
			return GetMerging(Main.tile[x,y]);
        }

		internal static bool? GetMerging(Tile tile)
		{
            return tile.Get<TileInfo>().Merging switch
            {
                TileInfo.MergeType.Alt => true,
                TileInfo.MergeType.None => false,
                _ => null,
            };
		}

		internal static void SetMerging(int x, int y)
		{
			SetMerging(Main.tile[x, y]);
		}

		internal static void SetMerging(Tile tile)
		{
			ref TileInfo info = ref tile.Get<TileInfo>();
			info.Merging = TileInfo.MergeType.Default;
		}
	}

	public class ReaverPlayer : ModPlayer
	{
		/// <summary>
		/// Runs when a player joins a world. Only called on the client that joined, and nobody else.
		/// </summary>
		/// <param name="player">The player that joined.</param>
		public override void OnEnterWorld(Player player)
		{
			
		}
	}

	public struct TileInfo : ITileData
	{
		public enum MergeType : byte
		{
			/// <summary>
			/// The tile merges normall
			/// </summary>
			Default = 0x0,
			/// <summary>
			/// The tile can merge with other merge tiles
			/// </summary>
			Alt = 0x1,
			/// <summary>
			/// The tile cannot merge with any other tiles
			/// </summary>
			None = 0x2,
		}

		public byte mergeData;

		public MergeType Merging
		{
			get => (MergeType)TileDataPacking.Unpack(mergeData, 0, 8);
			set => mergeData = (byte)TileDataPacking.Pack((byte)value, mergeData, 0, 8);
		}
	}
}