namespace DynamicMarketEconomy;

using HarmonyLib;
using StardewValley.Objects;

[HarmonyPatch(typeof(SObject), nameof(SObject.salePrice))]
public class SalePricePatch
{
    public static void Postfix(ref int __result, SObject __instance)
    {
        if (__instance == null || __result <= 0)
            return;

        __result = ModEntry.Instance.GetPrice(__instance.ParentSheetIndex, __result);
    }
}