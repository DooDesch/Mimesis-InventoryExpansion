using InventoryExpansion.Config;
using MelonLoader;

[assembly: MelonInfo(typeof(InventoryExpansion.Core), "InventoryExpansion", "1.1.0", "DooDesch", null)]
[assembly: MelonGame("ReLUGames", "MIMESIS")]

namespace InventoryExpansion
{
	public sealed class Core : MelonMod
	{
		public override void OnInitializeMelon()
		{
			InventoryExpansionPreferences.Initialize();
			HarmonyInstance.PatchAll();
			MelonLogger.Msg("InventoryExpansion initialized. Enabled={0}", InventoryExpansionPreferences.Enabled);
		}
	}
}

