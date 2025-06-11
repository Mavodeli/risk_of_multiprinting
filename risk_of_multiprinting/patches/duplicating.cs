using HarmonyLib;
using UnityEngine;
using EntityStates;
using Object = UnityEngine.Object;
using EntityStates.Duplicator;
using RoR2;

namespace risk_of_multiprinting.patches.duplicating
{
    [HarmonyPatch]
    public class DuplicatingPatch
    {
        [HarmonyPatch(typeof(Duplicating), nameof(Duplicating.DropDroplet))]
        [HarmonyPrefix]
        static bool PatchedDropDroplet(Duplicating __instance)
        {
            if (__instance.hasDroppedDroplet) { return false; }

            __instance.hasDroppedDroplet = true;

            // drop custom amount
            int customAmount = 10;
            if (customAmount != null)
            {
                for (int i = 0; i < customAmount; i++)
                {
                    __instance.GetComponent<ShopTerminalBehavior>().DropPickup();
                }
            }

            if (!(bool)(Object)__instance.muzzleTransform) { return false; }
            if ((bool)(Object)__instance.bakeEffectInstance)
            {
                if ((Object)__instance._emh_bakeEffectInstance != (Object)null && __instance._emh_bakeEffectInstance.OwningPool != null)
                    __instance._emh_bakeEffectInstance.OwningPool.ReturnObject(__instance._emh_bakeEffectInstance);
                else
                    EntityState.Destroy((Object)__instance.bakeEffectInstance);
                __instance._emh_bakeEffectInstance = (EffectManagerHelper)null;
                __instance.bakeEffectInstance = (GameObject)null;
            }

            if (!(bool)(Object)Duplicating.releaseEffectPrefab) { return false; }

            EffectManager.SimpleMuzzleFlash(Duplicating.releaseEffectPrefab, __instance.gameObject, Duplicating.muzzleString, false);

            return false;
        }

    }
}