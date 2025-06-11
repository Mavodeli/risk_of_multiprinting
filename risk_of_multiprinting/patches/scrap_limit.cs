using HarmonyLib;
using RoR2;

namespace risk_of_multiprinting.patches.scraplimit
{
    [HarmonyPatch]
    public class ScrapLimitPatch
    {
        [HarmonyPatch(typeof(ScrapperController), nameof(ScrapperController.Start))]
        [HarmonyPostfix]
        static void AlterScrapLimit(ScrapperController __instance)
        {
            __instance.maxItemsToScrapAtATime = 50;
        }
    }
}