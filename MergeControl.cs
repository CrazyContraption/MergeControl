using System.IO;
using System.Threading.Tasks;			// Tasks
using MergeControl.Items;				// Chisel
using Microsoft.Xna.Framework;			// Color
using Terraria;							// Terraria, mostly Main
using Terraria.ID;						// TileIDs
using Terraria.ModLoader;				// Hooks n'stuff
using static MergeControl.Enumerations;	// Non-Class public Enumerations

namespace MergeControl
{
	public class MergeControl : Mod
	{
		/// <summary>
		/// Selection to be drawn by <seealso cref="WorldIO.PostDrawTiles"/> when using the <seealso cref="Chisel"/>'s main action.
		/// </summary>
		internal static System.Drawing.Rectangle Selection;

		/// <summary>
		/// Static tModLoader instance of <seealso cref="MergeControl"/>
		/// </summary>
		internal static MergeControl MyMod = ModContent.GetInstance<MergeControl>();

		/// <summary>
		/// Called when the mod it loaded by tModLoader - used to mount our <seealso cref="WorldGen_TileFrame"/> detour.
		/// </summary>
		public override void Load()
			=> On.Terraria.WorldGen.TileFrame += WorldGen_TileFrame;

		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			MergeType actionType = (MergeType)reader.ReadByte();

			if (actionType is MergeType.Handshake && Main.netMode is NetmodeID.Server)
            {
				MyMod.Logger.Info($"Handshake requested by Client {whoAmI}. Starting thread.");
				new Task(delegate // Create a new thread
				{
					System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
					Log($"Thread started, now parsing/sending world data...", LogType.Info, Color.Yellow);
					uint updates = 0;
					for (ushort row = 0; row <= Main.maxTilesY; row++)
						for (ushort col = 0; col <= Main.maxTilesX; col++)
						{
							Tile tile = Framing.GetTileSafely(col, row);
							if (tile.HasTile || tile.IsActuated)
							{
								MergeType type = GetMerging(tile);
								if (type is MergeType.Default)
									continue;

								ModPacket packet = MyMod.GetPacket();
								packet.Write((byte)type);
								packet.Write(col);
								packet.Write(row);
								packet.Send(whoAmI);
								updates++;
							}
						}
					timer.Stop();
					Log($"World data parsed and sent to Client {whoAmI}!", LogType.Info, Color.Yellow);
					Log($"Thread took {timer.ElapsedMilliseconds}ms, {updates} tiles were accounted for.", LogType.Info, Color.AliceBlue);

				}).Start();
				return;
			}

			/*
				byte origin = (byte)whoAmI;
				if (Main.netMode != NetmodeID.Server)
					origin = reader.ReadByte();
			*/

			ushort x = reader.ReadUInt16();
			ushort y = reader.ReadUInt16();


			Tile tile = Framing.GetTileSafely(x, y);
			if (actionType is not MergeType.Actuation)
				SetMerging(tile, actionType);
			else
				tile.IsActuated = !tile.IsActuated;

			if (Main.netMode is NetmodeID.Server)
            {
				ModPacket packet = GetPacket();
				//packet.Write((byte)whoAmI);
				packet.Write((byte)actionType);
				packet.Write(x);
				packet.Write(y);
				packet.Send(-1, whoAmI);
			}
			else
				WorldGen.TileFrame(x, y, false, true);
		}

		/// <summary>
		/// Detour for vanilla handling of tile framing.<br/>
		/// See <seealso cref="WorldGen.TileFrame"/> for syntax and purpose.
		/// </summary>
		private void WorldGen_TileFrame(On.Terraria.WorldGen.orig_TileFrame orig, int x, int y, bool resetFrame, bool noBreak)
		{
			if (Main.PlayerLoaded)
			{
				Tile tile = Framing.GetTileSafely(x, y);
				if (tile.HasTile is false && tile.IsActuated is false)
					return;

				MergeType type = GetMerging(tile);
				switch (type)
                {
					case MergeType.Default:
						orig(x, y, resetFrame, noBreak);
						return;

					case MergeType.Alone:
						SetFraming(tile, 9 + Main.rand.Next(3), 3); // Same as below, but less overhead per-tile
						return;

					case MergeType.Group11
					case MergeType.Group2:
					case MergeType.Group3:
					case MergeType.Group4:
						bool?[] merges =
						{
							GetMergeType(tile.TileType, x, y - 1, type),     // N
#if DEBUG
							GetMergeType(tile.TileType, x + 1, y - 1, type), // NE
#else
							null,
#endif
							GetMergeType(tile.TileType, x + 1, y, type),     // E
#if DEBUG
							GetMergeType(tile.TileType, x + 1, y + 1, type), // SE
#else
							null,
#endif
							GetMergeType(tile.TileType, x, y + 1, type),	 // S
#if DEBUG
							GetMergeType(tile.TileType, x - 1, y + 1, type), // SW
#else
							null,
#endif
							GetMergeType(tile.TileType, x - 1, y, type),     // W
#if DEBUG
							GetMergeType(tile.TileType, x - 1, y - 1, type)  // NW
#else
							null
#endif
						};
						SetMerges(tile,
							merges[0], // N
							merges[1], // NE
							merges[2], // E
							merges[3], // SE
							merges[4], // S
							merges[5], // SW
							merges[6], // W
							merges[7]  // NW
						);

						// Prevents recursive framing, but allows manual updates
						if (resetFrame)
							return;
						
						WorldGen.TileFrame(x, y - 1, true, true);
						WorldGen.TileFrame(x + 1, y, true, true);
						WorldGen.TileFrame(x, y + 1, true, true);
						WorldGen.TileFrame(x - 1, y, true, true);
						return;
                }
				Point framing = GetFramingFromMergeData((byte)GetMerging(tile));
				tile.TileFrameX = (short)framing.X;
				tile.TileFrameY = (short)framing.Y;
				return;
			}
			orig(x, y, resetFrame, noBreak);
		}

		/// <summary>
		/// Gets the sprite pixel framing repspective to the grouping byte.<br/>
		/// Used to painting tiles from memory.
		/// </summary>
		/// <param name="mergeType">The frame index of the tile.<br/>
		/// Value is offset, See <seealso cref="MergeType"/> for assignment information.</param>
		/// <returns><seealso cref="Point"/> of the X,Y location that the tile's framing should be set to.</returns>
		internal static Point GetFramingFromMergeData(byte mergeType)
        {
            int x, y;
			mergeType--;

			if (mergeType <= 71)
            {
                if (mergeType >= 60)
                    mergeType++;

                x = mergeType % 16;
                y = (mergeType / 16);
            }
            else if (mergeType <= 161)
            {
				mergeType -= 72;
				x = mergeType % 13;
                y = (mergeType / 13) + 5;
            }
            else if (mergeType <= 183)
            {
				mergeType -= 163;
				x = mergeType % 7;
                y = (mergeType / 7) + 12;
            }
            else
                return new Point(18, 18);

			return new Point(x * 18, y * 18);
		}

		/// <summary>
		/// Gets how a tile at x,y should merge with the provided type and grouping.
		/// </summary>
		/// <param name="originType">Tile type of the original tile we're trying to merge to the query.</param>
		/// <param name="x">X value of the query tile's location.</param>
		/// <param name="y">Y value of the query tile's location.</param>
		/// <param name="originGroup">Grouping of the tile we're trying to merge to the x,y tile.</param>
		/// <returns>Nullable boolean. True is same type, should merge. False is dirt or other alt-merge. Null is air/non-group (no merge).</returns>
		private static bool? GetMergeType(int originType, int x, int y, MergeType originGroup = MergeType.Default)
        {
			Tile tile = Framing.GetTileSafely(x, y);
			if (tile.HasTile || tile.IsActuated)
				if (originGroup == GetMerging(tile)) // Exists and part of same grouping
					if (tile.TileType > TileID.Dirt || tile.TileType == originType) // Not dirt, or same as origin
						return true; // Should merge
#if DEBUG
					else
						return false; // Dirt
#endif
			return null; // Air or other
        }

		/// <summary>
		/// Sets the framing of a tile based on the <seealso cref="GetMergeType(int, int, int, MergeType)"/> status of each of the adjacent 8 tiles.
		/// </summary>
		/// <param name="tile"></param>
		/// <param name="N"></param>
		/// <param name="NE"></param>
		/// <param name="E"></param>
		/// <param name="SE"></param>
		/// <param name="S"></param>
		/// <param name="SW"></param>
		/// <param name="W"></param>
		/// <param name="NW"></param>
		internal static void SetMerges(Tile tile,
			bool? N = null,
			bool? NE = null,
			bool? E = null,
			bool? SE = null,
			bool? S = null,
			bool? SW = null,
			bool? W = null,
			bool? NW = null
		) { // 45 states
			// TODO: Change "true" to checking if random frame variations should be used or not.
			int randomVariation = TileID.Sets.AllBlocksWithSmoothBordersToResolveHalfBlockIssue[tile.TileType] is false ? Main.rand.Next(3) : tile.TileFrameNumber;
			switch ((N, NE, E, SE, S, SW, W, NW))
			{
				case (null, null, null, null, null, null, null, null): // None
				case (false, _, false, _, null, _, null, _): // NE
				case (false, _, null, _, null, _, false, _): // NW
				case (null, _, false, _, false, _, null, _): // SE
				case (null, _, null, _, false, _, false, _): // SW
				case (false, _, false, _, null, _, false, _): // NEW dirt
				case (false, _, false, _, false, _, null, _): // NES dirt
				case (null, _, false, _, false, _, false, _): // SEW dirt
				case (false, _, null, _, false, _, false, _): // NWS dirt
					SetFraming(tile, 9 + randomVariation, 3);
					return;

				case (true, _, null, _, null, _, null, _): // N
				case (true, _, false, _, null, _, null, _): // NE Edirt
				case (true, _, null, _, null, _, false, _): // NW Wdirt
				//case (true, _, true, _, null, _, false, _): // NEW Wdirt
				//case (true, _, false, _, null, _, true, _): // NEW Edirt
				//case (true, _, false, _, null, _, false, _): // NEW EWdirt
					SetFraming(tile, 6 + randomVariation, 3);
					return;
				case (false, null, null, null, null, null, null, null): // N dirt
					SetFraming(tile, 6 + randomVariation, 8);
					return;

				case (null, _, true, _, null, _, null, _): // E
				case (false, _, true, _, null, _, null, _): // EN
				case (null, _, true, _, false, _, null, _): // ES
					SetFraming(tile, 9, 0 + randomVariation);
					return;
				case (null, _, false, _, null, _, null, _): // E
					SetFraming(tile, 3 + randomVariation, 13);
					return;

				case (null, _, null, _, true, _, null, _): // S
				case (null, _, false, _, true, _, null, _): // SE
				case (null, _, null, _, true, _, false, _): // SW
					SetFraming(tile, 6 + randomVariation, 0);
					return;
				case (null, null, null, null, false, null, null, null): // S
					SetFraming(tile, 6, 5 + randomVariation);
					return;

				case (null, _, null, _, null, _, true, _): // W
				case (false, _, null, _, null, _, true, _): // WN
				case (null, _, null, _, false, _, true, _): // WS
					SetFraming(tile, 12, 0 + randomVariation);
					return;
				case (null, null, null, null, null, null, false, null): // W
					SetFraming(tile, 0, 13 + randomVariation);
					return;

				case (true, _, null, _, true, _, null, _): // NS same
					SetFraming(tile, 5, 0 + randomVariation);
					return;
				case (false, _, null, _, false, _, null, _): // NS dirt
					SetFraming(tile, 6, 12 + randomVariation);
					return;
				case (true, _, null, _, false, _, null, _): // NS diff
					SetFraming(tile, 7, 5 + randomVariation);
					return;
				case (false, _, null, _, true, _, null, _): // NS diff
					SetFraming(tile, 7, 8 + randomVariation);
					return;

				case (null, _, true, _, null, _, true, _): // EW same
					SetFraming(tile, 6 + randomVariation, 4);
					return;
				case (null, _, false, _, null, _, false, _): // EW dirt
					SetFraming(tile, 9 + randomVariation, 11);
					return;
				case (null, _, true, _, null, _, false, _): // EW diff
					SetFraming(tile, 0 + randomVariation, 14);
					return;
				case (null, _, false, _, null, _, true, _): // EW diff
					SetFraming(tile, 3 + randomVariation, 14);
					return;

				case (true, _, true, _, null, _, null, _): // NE same
					SetFraming(tile, 0 + randomVariation * 2, 4);
					return;
				case (null, _, true, _, true, _, null, _): // SE same
					SetFraming(tile, 0 + randomVariation * 2, 3);
					return;
				case (null, _, null, _, true, _, true, _): // SW same
					SetFraming(tile, 1 + randomVariation * 2, 3);
					return;
				case (true, _, null, _, null, _, true, _): // NW same
					SetFraming(tile, 1 + randomVariation * 2, 4);
					return;

				case (true, _, true, _, null, _, true, _): // NEW same
					SetFraming(tile, 1 + randomVariation, 2);
					return;
				case (true, _, true, _, true, _, null, _): // NES same
					SetFraming(tile, 0, 0 + randomVariation);
					return;
				case (null, _, true, _, true, _, true, _): // SEW same
					SetFraming(tile, 1 + randomVariation, 0);
					return;
				case (true, _, null, _, true, _, true, _): // NWS same
					SetFraming(tile, 4, 0 + randomVariation);
					return;

				case (false, _, false, _, true, _, true, _): // N(NE)E
					SetFraming(tile, 3, 5 + randomVariation * 2);
					return;
				case (true, _, false, _, false, _, true, _): // S(SE)E
					SetFraming(tile, 3, 6 + randomVariation * 2);
					return;
				case (true, _, true, _, false, _, false, _): // S(SW)W
					SetFraming(tile, 2, 6 + randomVariation * 2);
					return;
				case (false, _, true, _, true, _, false, _): // N(NW)W
					SetFraming(tile, 0, 0);
					return;

				case (true, _, true, _, true, _, true, _): // NESW same
					SetFraming(tile, 1 + randomVariation, 1);
					return;

				case (false, _, false, _, false, _, false, _): // NESW dirt
					SetFraming(tile, 6 + randomVariation, 11);
					return;

#if DEBUG
				case (true, null, true, null, null, null, true, null): // NEW
					SetFraming(tile, 0, 0);
					return;
				case (false, null, false, null, null, null, false, null): // NEW
					SetFraming(tile, 0, 0);
					return;
				case (true, null, false, null, null, null, false, null): // NEW
					SetFraming(tile, 0, 0);
					return;
				case (null, null, null, null, null, null, null, null): // NEW
					SetFraming(tile, 0, 0);
					return;
				case (null, null, null, null, null, null, null, null): // NEW
					SetFraming(tile, 0, 0);
					return;
				case (null, null, null, null, null, null, null, null): // NEW
					SetFraming(tile, 0, 0);
					return;

				case (null, null, null, null, null, null, null, null): // NES
					SetFraming(tile, 0, 0);
					return;
				case (null, null, null, null, null, null, null, null): // NES
					SetFraming(tile, 0, 0);
					return;
				case (null, null, null, null, null, null, null, null): // NES
					SetFraming(tile, 0, 0);
					return;
				case (null, null, null, null, null, null, null, null): // NES
					SetFraming(tile, 0, 0);
					return;
				case (null, null, null, null, null, null, null, null): // NES
					SetFraming(tile, 0, 0);
					return;
				case (null, null, null, null, null, null, null, null): // NES
					SetFraming(tile, 0, 0);
					return;

				case (null, null, null, null, null, null, null, null): // NES
					SetFraming(tile, 0, 0);
					return;
				case (null, null, null, null, null, null, null, null): // NES
					SetFraming(tile, 0, 0);
					return;
#endif
				default:
					return;
			}
		}

		/// <summary>
		/// Updates a <paramref name="tile"/>'s sprite framing.<br/>
		/// Handles the type and pixel coordinate conversions.
		/// </summary>
		/// <param name="tile">The <paramref name="tile"/> that is to have its sprite updated.</param>
		/// <param name="x">The <paramref name="x"/> value of the sprite frame, before pixel conversion.</param>
		/// <param name="y">The <paramref name="y"/> value of the sprite frame, before pixel conversion.</param>
		internal static void SetFraming(Tile tile, int x, int y)
		{
			tile.TileFrameX = (short)(x * 18);
			tile.TileFrameY = (short)(y * 18);
		}

		/// <summary>
		/// Checks modded world memory for the merge grouping of a specific tile.
		/// </summary>
		/// <param name="tile"><paramref name="tile"/> that should checked in storage.</param>
		/// <returns>Grouping type of the tile, see <seealso cref="MergeType"/> for assignments.</returns>
		internal static MergeType GetMerging(Tile tile)
			=> tile.Get<TileInfo>().Merging;

		/// <summary>
		/// Same as <seealso cref="GetMerging(Tile)", but safely grabs the <seealso cref="Tile"/> instance for you./>
		/// </summary>
		/// <param name="x">The <paramref name="x"/> value for the <seealso cref="Tile"/> instance to grab.</param>
		/// <param name="y">The <paramref name="y"/> value for the <seealso cref="Tile"/> instance to grab.</param>
		/// <returns>Grouping type of the tile, see <seealso cref="MergeType"/> for assignments.</returns>
		internal static MergeType GetMerging(int x, int y)
			=> GetMerging(Framing.GetTileSafely(x, y));

		/// <summary>
		/// Sets modded world memory for the merge grouping of a specified <paramref name="tile"/>.
		/// </summary>
		/// <param name="tile">The <paramref name="tile"/> that should updated to storage.</param>
		/// <param name="mergeType">Grouping type to be set, see <seealso cref="MergeType"/> for assignments.</param>
		internal static void SetMerging(Tile tile, MergeType mergeType = MergeType.Default)
		{
			ref TileInfo info = ref tile.Get<TileInfo>();
			info.Merging = mergeType;
		}

		/// <summary>
		/// Same as <seealso cref="SetMerging(Tile, MergeType)", but safely grabs the <seealso cref="Tile"/> instance for you./>
		/// </summary>
		/// <param name="x">The <paramref name="x"/> value for the <seealso cref="Tile"/> instance to grab.</param>
		/// <param name="y">The <paramref name="y"/> value for the <seealso cref="Tile"/> instance to grab.</param>
		/// <param name="mergeType">Grouping type to be set, see <seealso cref="MergeType"/> for assignments.</param>
		internal static void SetMerging(int x, int y, MergeType mergeType = MergeType.Default)
			=> SetMerging(Framing.GetTileSafely(x, y), mergeType);

		internal static void Log(string text, LogType logType = LogType.None, Color color = default)
        {
            switch (logType)
            {
                case LogType.Info: MyMod.Logger.Info(text); break;
                case LogType.Warning: MyMod.Logger.Warn(text); break;
                case LogType.Error: MyMod.Logger.Error(text); break;
				case LogType.Fatal: MyMod.Logger.Fatal(text); break;
				case LogType.Debug: MyMod.Logger.Debug(text); break;
            }

            if (Main.netMode is not NetmodeID.Server && color != default)
				Main.NewText(text, color);
        }
	}

	public class MergeTile : GlobalTile
    {
        public override void KillTile(int x, int y, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
			Tile tile = Framing.GetTileSafely(x, y);
			if (MergeControl.GetMerging(tile) is not MergeType.Default)
            {
				MergeControl.SetMerging(tile, MergeType.Default);

				if (Main.netMode == NetmodeID.MultiplayerClient)
				{
					ModPacket packet = MergeControl.MyMod.GetPacket();
					packet.Write((byte)MergeType.Default);
					packet.Write((ushort)x);
					packet.Write((ushort)y);
					packet.Send(-1, Main.myPlayer);
				}

				WorldGen.TileFrame(x, y, noBreak: true);
				fail = true;
            }
		}
    }

	public class MergePlayer : ModPlayer
    {
		public override void OnEnterWorld(Player player)
		{
			if (Main.netMode is NetmodeID.MultiplayerClient)
				new Task(delegate // Create a new thread
				{
					while (Main.PlayerLoaded is false)
						System.Threading.Thread.Sleep(100);

					ModPacket packet = MergeControl.MyMod.GetPacket();
					packet.Write((byte)MergeType.Handshake);
					packet.Send(-1, Main.myPlayer);
				}).Start();
		}
    }

	internal struct TileInfo : ITileData
	{
		/// <summary>
		/// Temporary storage of byte data for writing and reading to/from memory.
		/// </summary>
		internal byte mergeData;

		/// <summary>
		/// Converts between byte tile memory, and our <seealso cref="MergeType"/> enumeration.
		/// </summary>
		internal MergeType Merging
		{
			get => (MergeType)TileDataPacking.Unpack(mergeData, 0, 8);
			set => mergeData = (byte)TileDataPacking.Pack((byte)value, mergeData, 0, 8);
		}
	}
}