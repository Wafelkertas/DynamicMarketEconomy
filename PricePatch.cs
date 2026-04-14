using HarmonyLib;
using StardewValley.Objects;

namespace DynamicEconomy
{
    [HarmonyPatch(typeof(SObject), nameof(SObject.salePrice))]
    public class SalePricePatch
    {
        public static void Postfix(ref int __result, SObject __instance)
        {
            __result = PriceModel.Adjust(__instance, __result);
        }
    }

    [HarmonyPatch(typeof(SObject), nameof(SObject.sellToStorePrice))]
    public class StorePricePatch
    {
        public static void Postfix(ref int __result, SObject __instance)
        {
            __result = PriceModel.Adjust(__instance, __result);
        }
    }
}