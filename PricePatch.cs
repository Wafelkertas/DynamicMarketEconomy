using HarmonyLib;
using StardewValley;

namespace DynamicEconomy
{
    [HarmonyPatch(typeof(Object), nameof(Object.sellToStorePrice))]
    public class PricePatch
    {
        public static bool Prefix(Object __instance, ref int __result)
        {
            int basePrice = __instance.Price;

            __result = ModEntry.Instance.GetPrice(
                __instance.ParentSheetIndex,
                basePrice
            );

            return false; // skip original method
        }
    }
}