namespace DynamicMarketEconomy;

using HarmonyLib;
using StardewObject = StardewValley.Object;

[HarmonyPatch(typeof(StardewObject), nameof(StardewObject.salePrice))]
public class SalePricePatch
{
    public static void Postfix(ref int __result, StardewObject __instance)
    {
        if (__instance == null || __result <= 0)
            return;

        __result = ModEntry.Instance.GetPrice(__instance.ParentSheetIndex, __result);
    }
}
