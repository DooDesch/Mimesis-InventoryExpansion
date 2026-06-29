using InventoryExpansion.Config;
using MelonLoader;

[assembly: MelonInfo(typeof(InventoryExpansion.Core), "InventoryExpansion", "1.4.4", "DooDesch", null)]
[assembly: MelonGame("ReLUGames", "MIMESIS")]

namespace InventoryExpansion
{
	public sealed class Core : MelonMod
	{
		public override void OnInitializeMelon()
		{
			InventoryExpansionPreferences.Initialize();
			// MelonLoader auto-applies this assembly's Harmony patches via HarmonyInit(); calling PatchAll()
			// here too would double-apply every patch (each prefix/postfix runs twice). Do NOT add it back.
			// (See FakePlayers/Core.cs.)
			MelonLogger.Msg("InventoryExpansion initialized. Enabled={0}", InventoryExpansionPreferences.Enabled);
		}
	}
}

