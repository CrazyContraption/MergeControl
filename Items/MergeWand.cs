using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace MergeControl.Items
{
    internal class MergeWand : ModItem
    {
        /* AltFunctionUse
         * 
         * Player player - The player using the item in question
         * 
         * Called when the game wants to know if we can left-clicked with this given item.
         */
        public override bool? UseItem(Player player)
        {
            if (Main.mouseLeft)
            {
                if (Main.mouseRight)
                    Main.NewText($"RESET [{Main.mouseX},{Main.mouseY}]");
                else
                    Main.NewText($"Main Action [{Main.mouseX},{Main.mouseY}]");
            }
            else
            {
                Main.NewText($"Alt Action [{Main.mouseX},{Main.mouseY}]");
            }
            return true;
        }

        /* AltFunctionUse
         * 
         * Player player - The player using the item in question
         * 
         * Called when the game wants to know if we can right-click with this given item.
         */
        public override bool AltFunctionUse(Player player)
        {
            player.controlUseItem = true; // Yes, we're using the item normally, even though we're really not
            player.ItemCheck(player.selectedItem); // Run the item's normal actions on whatever we're holding
            return true;
        }

        public override void SetStaticDefaults()
        {
            Tooltip.SetDefault("0% hammer power?");
        }

        public override void SetDefaults()
        {
            Item.damage = 1;
            Item.DamageType = DamageClass.Default;
            Item.width = 40;
            Item.height = 40;
            Item.useTime = 15;
            Item.useAnimation = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6;
            Item.value = 0;
            Item.rare = ItemRarityID.Quest;
            Item.autoReuse = true;
        }

        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ItemID.Wood, 6);
            recipe.AddIngredient(ItemID.WoodPlatform, 2);
            recipe.AddTile(TileID.WorkBenches);
            recipe.Register();
        }
    }
}
