using System.Drawing;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using static MergeControl.Enumerations;

namespace MergeControl.Items
{
    internal class Chisel : ModItem
    {
        /// <summary>
        /// The current mode of all <seealso cref="Chisel"/>s on the current player.<br/>
        /// See <seealso cref="Mode"/> for assignments.
        /// </summary>
        private static Mode ToolMode;

        public override bool? UseItem(Player player)
        {
            if (Main.netMode == NetmodeID.Server)
                return null;

            if (MergeControl.Selection.IsEmpty is false)
                return false;

            if (Main.mouseLeft)
            {
                new System.Threading.Tasks.Task(delegate // Create a new thread
                {
                    int x = Player.tileTargetX;
                    int y = Player.tileTargetY;
                    Tile tile = Framing.GetTileSafely(x, y);
                    bool changedFromStartPoint = false;

                    MergeType? type = null;

                    for (ushort iterations = 0; Main.mouseLeft; iterations++)
                    {
                        if (changedFromStartPoint is false && (x != Player.tileTargetX || y != Player.tileTargetY))
                            changedFromStartPoint = true;

                        MergeControl.Selection.X = System.Math.Min(x, Player.tileTargetX);
                        MergeControl.Selection.Y = System.Math.Min(y, Player.tileTargetY);
                        MergeControl.Selection.Width = System.Math.Max(x, Player.tileTargetX) - MergeControl.Selection.X + 1;
                        MergeControl.Selection.Height = System.Math.Max(y, Player.tileTargetY) - MergeControl.Selection.Y + 1;

                        if (player.HasEnoughPickPowerToHurtTile(x, y) is false)
                            continue;

                        if (ToolMode is Mode.Chisel && changedFromStartPoint is false && iterations >= 16 && iterations % 2 == 0)
                            if (tile.HasTile || tile.IsActuated)
                                type = DoAlteration(tile);

                        System.Threading.Thread.Sleep(32);
                    }

                    if (type != null && changedFromStartPoint is false && Main.netMode == NetmodeID.MultiplayerClient)
                    {
                        ModPacket packet = MergeControl.MyMod.GetPacket();
                        packet.Write((byte)type);
                        packet.Write((ushort)x);
                        packet.Write((ushort)y);
                        packet.Send(-1, Main.myPlayer);
                    }

                    for (int row = MergeControl.Selection.Top; row < MergeControl.Selection.Bottom; row++)
                        for (int col = MergeControl.Selection.Left; col < MergeControl.Selection.Right; col++)
                        {
                            if (player.HasEnoughPickPowerToHurtTile(col, row) is false)
                                continue;

                            Tile subTile = Framing.GetTileSafely(col, row);
                            if (subTile.HasTile && (WorldGen.SolidTile(col, row, true) || subTile.IsActuated || changedFromStartPoint is false) || ToolMode is Mode.Reset)
                            {
                                type = DoAlteration(subTile);

                                if (Main.netMode == NetmodeID.MultiplayerClient)
                                {
                                    ModPacket packet = MergeControl.MyMod.GetPacket();
                                    packet.Write((byte)type);
                                    packet.Write((ushort)col);
                                    packet.Write((ushort)row);
                                    packet.Send(-1, Main.myPlayer);
                                }

                                WorldGen.TileFrame(col, row, false, true);
                            }
                        }

                    _ = SoundEngine.PlaySound(SoundID.Tink, new Microsoft.Xna.Framework.Vector2(Player.tileTargetX, Player.tileTargetY));
                    MergeControl.Selection = Rectangle.Empty;
                }).Start();
            }
            else if (Main.mouseRight)
            {
                ToolMode = ToolMode >= Mode.Group4 ? 0 : ToolMode + 1;

                if (ToolMode == Mode.Actuate && NPC.downedBoss3 is false) // Actuate and skeletron is not dead
                    ToolMode++; // Skip the mode

                MergeControl.Log(GetSettingText(), LogType.Info, Microsoft.Xna.Framework.Color.Orange);
                player.altFunctionUse = ItemAlternativeFunctionID.ActivatedAndUsed;
            }
            
            return true;
        }

        public override bool CanUseItem(Player player)
            => Main.netMode == NetmodeID.Server || MergeControl.Selection.IsEmpty;

        public override void UpdateInventory(Player player)
        {
            if (Main.netMode == NetmodeID.Server)
                return;

            if (player.HeldItem is not null && player.HeldItem.Name.Equals("Chisel", System.StringComparison.OrdinalIgnoreCase))
            {
                Tile tile = Framing.GetTileSafely(Player.tileTargetX, Player.tileTargetY);
                if (tile.HasTile || tile.TileType > TileID.Dirt)
                {
                    player.cursorItemIconText = $"[{MergeControl.GetMerging(tile)}]";
                    player.cursorItemIconEnabled = true;
                }
            }
        }

        /// <summary>
        /// Performs manipulations based on the <seealso cref="ToolMode"/> of the <seealso cref="Chisel"/>.
        /// </summary>
        /// <param name="tile"><paramref name="tile"/> instance to be altered.</param>
        /// <param name="mergeType">The <paramref name="mergeType"/> to be applied to the <paramref name="tile"/>.</param>
        /// <param name="mode">Optionally include a mode to override the tool's current mode.<br/>
        /// <returns>Grouping type of the tile that was set, see <seealso cref="MergeType"/> for assignments.</returns>
        /// Can be used for multiplayer puppetting.</param>
        private static MergeType DoAlteration(Tile tile, Mode? mode = null)
        {
            MergeType mergeType = MergeType.Default;
            switch (mode ?? ToolMode)
            {
                case Mode.Chisel:
                    mergeType = MergeControl.GetMerging(tile);
                    if ((byte)mergeType >= 1 && (byte)mergeType <= 183)
                        MergeControl.SetMerging(tile, ++mergeType);
                    else
                        MergeControl.SetMerging(tile, mergeType = (MergeType)1);
                    break;
                case Mode.None:
                    MergeControl.SetMerging(tile, mergeType = MergeType.Alone);
                    break;
                case Mode.Group1:
                    MergeControl.SetMerging(tile, mergeType = MergeType.Group1);
                    break;
                case Mode.Group2:
                    MergeControl.SetMerging(tile, mergeType = MergeType.Group2);
                    break;
                case Mode.Group3:
                    MergeControl.SetMerging(tile, mergeType = MergeType.Group3);
                    break;
                case Mode.Group4:
                    MergeControl.SetMerging(tile, mergeType = MergeType.Group4);
                    break;
                case Mode.Actuate:
                    tile.IsActuated = !tile.IsActuated;
                    mergeType = MergeType.Actuation;
                    break;
                default:
                    MergeControl.SetMerging(tile, MergeType.Default);
                    break;
            }
            return mergeType;
        }

        /// <summary>
        /// Gets the text to be announced based on the current <seealso cref="ToolMode"/>.<br/>
        /// Used when Swapping modes of a <seealso cref="Chisel"/>.
        /// </summary>
        /// <returns>Text associated with the current <seealso cref="ToolMode"/></returns>
        private static string GetSettingText()
        {
            return ToolMode switch
            {
                Mode.Reset => "Chisel is set to default framing.",
                Mode.Chisel => "Chisel is set to cycle framing.",
                Mode.Actuate => "Chisel is set to toggle actuation.",
                Mode.Group1 => "Chisel is set to Group 1 framing.",
                Mode.Group2 => "Chisel is set to Group 2 framing.",
                Mode.Group3 => "Chisel is set to Group 3 framing.",
                Mode.Group4 => "Chisel is set to Group 4 framing.",
                Mode.None => "Chisel is set to disable all framing.",
                _ => "",
            };
        }

        public override bool AltFunctionUse(Player player)
        {
            player.altFunctionUse = ItemAlternativeFunctionID.ShouldBeActivated;
            player.controlUseItem = true; // Yes, we're using the item normally, even though we're really not
            player.ItemCheck(player.selectedItem); // Run the item's normal actions on whatever we're holding
            return true;
        }

        public override void SetStaticDefaults()
            => Tooltip.SetDefault("0% hammer power?");

        public override void SetDefaults()
        {
            Item.damage = 1;
            Item.DamageType = DamageClass.Default;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 10;
            Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Thrust;
            Item.UseSound = SoundID.Tink;
            Item.knockBack = 6;
            Item.value = 0;
            Item.rare = ItemRarityID.Quest;
            Item.autoReuse = false;
        }

        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ItemID.IronBar, 6);
            recipe.AddIngredient(ItemID.WoodPlatform, 2);
            recipe.AddTile(TileID.WorkBenches);
            recipe.Register();
        }

        private enum Mode : byte
        {
            /// <summary>Resets tiles back to vanilla framing.</summary>
            Reset = 0,
            /// <summary>Cycles framing incrementally for manual touch-ups.</summary>
            Chisel,
            /// <summary>Toggles the actuation state, rather than re-framing.</summary>
            Actuate,
            /// <summary>Sets the grouping to group 1.</summary>
            Group1,
            /// <summary>Sets the grouping to group 2.</summary>
            Group2,
            /// <summary>Sets the grouping to group 3.</summary>
            Group3,
            /// <summary>Sets the grouping to group 4.</summary>
            Group4,
            /// <summary>Removes all adjacency framing.</summary>
            None,
        }
    }
}
