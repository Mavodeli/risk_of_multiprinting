using HarmonyLib;
using UnityEngine;
using EntityStates;
using Object = UnityEngine.Object;
using EntityStates.Duplicator;
using RoR2;
using RoR2.UI;
using UnityEngine.UI;
using TMPro;
using R2API.Networking.Interfaces;
using UnityEngine.Networking;

namespace risk_of_multiprinting.patches.duplicating
{
    public static class amountSelectorAsset
    {
        //You will load the assetbundle and assign it to here.
        public static AssetBundle mainBundle;
        //A constant of the AssetBundle's name.
        public const string bundleName = "multiprintingamountselectorpanel";
        // Uncomment this if your assetbundle is in its own folder. Of course, make sure the name of the folder matches this.
        public const string assetBundleFolder = "assets";

        //The direct path to your AssetBundle
        public static string AssetBundlePath
        {
            get
            {
                //This returns the path to your assetbundle assuming said bundle is on the same folder as your DLL. If you have your bundle in a folder, you can instead uncomment the statement below this one.
                return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Plugin.PInfo.Location), bundleName);
                // return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Plugin.PInfo.Location), assetBundleFolder, bundleName);

                // using same folder because thunderstore flattens the plugin
            }
        }

        public static void Init()
        {
            //Loads the assetBundle from the Path, and stores it in the static field.
            mainBundle = AssetBundle.LoadFromFile(AssetBundlePath);
        }
    }

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
            PurchaseInteraction purchaseInteraction = __instance.GetComponent<PurchaseInteraction>();
            for (int i = 0; i < purchaseInteraction.cost; i++)
            {
                __instance.GetComponent<ShopTerminalBehavior>().DropPickup();
            }
            // reset cost to duplicator default value of 1
            purchaseInteraction.cost = 1;

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

        [HarmonyPatch(typeof(Interactor), nameof(Interactor.AttemptInteraction))]
        [HarmonyPrefix]
        static bool ChooseCustomAmount(GameObject interactableObject, Interactor __instance)
        {
            // ignore anything that isn't a duplicator
            if (!interactableObject.name.Contains("Duplicator")) { return true; }

            PurchaseInteraction purchaseInteraction = interactableObject.GetComponent<PurchaseInteraction>();
            if (!purchaseInteraction) { return true; }


            // prompt user
            UnityEngine.Debug.Log("Risk of Multiprinting: Opening duplicator prompt");
            LocalUser firstLocalUser = LocalUserManager.GetFirstLocalUser();

            HUD hud = HUD.instancesList.Find(instance => instance.localUserViewer == firstLocalUser);
            if (!hud) { UnityEngine.Debug.Log("Risk of Mutliprinting: No HUD found!"); return false; }

            MPEventSystem eventSystem = MPEventSystem.instancesList.Find(instance => instance.localUser == firstLocalUser);
            if (!eventSystem) { return false; }

            int chosenValue = 0;

            var prefab = amountSelectorAsset.mainBundle.LoadAsset<GameObject>("multiprintingAmountSelectorPanel");
            GameObject panel = GameObject.Instantiate(prefab, hud.mainContainer.transform);

            eventSystem.cursorOpenerCount += 1;

            Transform textObjectTransform = panel.transform.Find("Text");
            TextMeshProUGUI textComponent = textObjectTransform.gameObject.GetComponent<TextMeshProUGUI>();

            Slider slider = panel.GetComponentInChildren<Slider>();
            slider.onValueChanged.AddListener((value) =>
            {
                chosenValue = (int)value;
                textComponent.text = chosenValue.ToString();
            });

            Transform buttonTransform = panel.transform.Find("Button");
            Button buttonComponent = buttonTransform.gameObject.GetComponent<Button>();
            buttonComponent.onClick.AddListener(() =>
            {
                eventSystem.cursorOpenerCount -= 1;
                GameObject.Destroy(panel);

                if (chosenValue != 0)
                {
                    if (NetworkServer.active)
                    {
                        // if we're the host we don't send an RPC message
                        UnityEngine.Debug.Log("Risk of Multiprinting: Directly calling function with modified values");
                        InteractorAdditions interactorAdditions = new InteractorAdditions();
                        interactorAdditions.PerformModifiedDuplicationInteraction(__instance, interactableObject, chosenValue);
                    }
                    else
                    {
                        // if we're a client we send an RPC message
                        UnityEngine.Debug.Log("Risk of Multiprinting: Sending RPC message to host with modified values");
                        new SendCustomAmountRPC(__instance.gameObject, interactableObject, chosenValue).Send(R2API.Networking.NetworkDestination.Server);
                    }
                }
            });

            // always abort the interaction, we continue it in the button event handler
            return false;
        }
    }

    public class SendCustomAmountRPC : INetMessage
    {
        GameObject interactorParentObject;
        GameObject duplicator;
        int customAmount;

        public SendCustomAmountRPC() { }

        public SendCustomAmountRPC(GameObject interactorParentObject, GameObject duplicator, int customAmount)
        {
            this.interactorParentObject = interactorParentObject;
            this.duplicator = duplicator;
            this.customAmount = customAmount;
        }

        public void Deserialize(NetworkReader reader)
        {
            interactorParentObject = reader.ReadGameObject();
            duplicator = reader.ReadGameObject();
            customAmount = reader.ReadInt32();
        }

        public void OnReceived()
        {
            // clients ignore this message
            if (!NetworkServer.active) { return; }

            // get interactor
            Interactor interactor = interactorParentObject.GetComponent<Interactor>();

            if (!interactor) { UnityEngine.Debug.Log("Risk of Multiprinting: Restoring interactor from parent object failed!"); return; }
            if (!duplicator) { UnityEngine.Debug.Log("Risk of Multiprinting: transmitting duplicator object failed!"); return; }

            UnityEngine.Debug.Log("Risk of Multiprinting: Received RPC message for modified duplication interaction");
            InteractorAdditions interactorAdditions = new InteractorAdditions();
            interactorAdditions.PerformModifiedDuplicationInteraction(interactor, duplicator, customAmount);
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.Write(interactorParentObject);
            writer.Write(duplicator);
            writer.Write(customAmount);
        }
    }

    public class InteractorAdditions
    {
        [Server]
        public void PerformModifiedDuplicationInteraction(Interactor interactor, GameObject duplicator, int customAmount)
        {
            if (!NetworkServer.active)
            {
                UnityEngine.Debug.LogWarning((object)"[Server] function 'System.Void RoR2.InteractorAdditions::PerformModifiedDuplicationInteraction(UnityEngine.GameObject)' called on client");
                return;
            }

            PurchaseInteraction purchaseInteraction = duplicator.GetComponent<PurchaseInteraction>();
            Interactability interactability = purchaseInteraction.GetInteractability(interactor);

            if (interactability == Interactability.Available)
            {
                // set custom value
                purchaseInteraction.cost = customAmount;

                // check affordability and reset if necessary
                if (!purchaseInteraction.CanBeAffordedByInteractor(interactor)) { purchaseInteraction.cost = 1; return; }

                purchaseInteraction.OnInteractionBegin(interactor);
                GlobalEventManager.instance.OnInteractionBegin(interactor, purchaseInteraction, duplicator);
            }
        }
    }



    //
    // Code Snippets for my future self:
    //



    // Attempt using extension

    // [Serializable]
    // public class PurchaseInteractionAdditionalData
    // {
    //     public bool finishInteraction;
    //     public PurchaseInteractionAdditionalData(bool shouldFinishInteraction = false)
    //     {
    //         finishInteraction = shouldFinishInteraction;
    //     }
    // }

    // public static class PurchaseInteractionExtension
    // {
    //     private static readonly ConditionalWeakTable<PurchaseInteraction, PurchaseInteractionAdditionalData> data = new ConditionalWeakTable<PurchaseInteraction, PurchaseInteractionAdditionalData>();

    //     public static PurchaseInteractionAdditionalData GetAdditionalData(this PurchaseInteraction purchaseInteraction)
    //     {
    //         return data.GetOrCreateValue(purchaseInteraction);
    //     }

    //     public static void AddData(this PurchaseInteraction purchaseInteraction, PurchaseInteractionAdditionalData purchaseInteractionAdditionalData)
    //     {
    //         try
    //         {
    //             data.Add(purchaseInteraction, purchaseInteractionAdditionalData);
    //         }
    //         catch (Exception) { }
    //     }
    // }



    // clone of original OnInteractionBegin

    // public class PurchaseInteractionClone
    // {
    //     public void OnInteractionBegin(PurchaseInteraction __instance, Interactor activator)
    //     {
    //         if (!__instance.CanBeAffordedByInteractor(activator))
    //             return;
    //         CharacterBody component1 = activator.GetComponent<CharacterBody>();
    //         CostTypeDef costTypeDef = CostTypeCatalog.GetCostTypeDef(__instance.costType);
    //         ItemIndex avoidedItemIndex = ItemIndex.None;
    //         ShopTerminalBehavior component2 = __instance.GetComponent<ShopTerminalBehavior>();
    //         if ((bool)(UnityEngine.Object)component2)
    //         {
    //             PickupDef pickupDef = PickupCatalog.GetPickupDef(component2.CurrentPickupIndex());
    //             avoidedItemIndex = pickupDef != null ? pickupDef.itemIndex : ItemIndex.None;
    //         }
    //         int cost = __instance.cost;
    //         if (__instance.costType == CostTypeIndex.Money)
    //         {
    //             if (activator.GetComponent<CharacterBody>().GetBuffCount(DLC2Content.Buffs.FreeUnlocks) > 0 && !(bool)(UnityEngine.Object)__instance.GetComponent<MultiShopController>())
    //             {
    //                 cost = 0;
    //                 activator.GetComponent<CharacterBody>().RemoveBuff(DLC2Content.Buffs.FreeUnlocks);
    //                 int num = (int)Util.PlaySound("Play_item_proc_onLevelUpFreeUnlock_activate", __instance.gameObject);
    //                 if ((bool)(UnityEngine.Object)component1.master)
    //                     --component1.master.trackedFreeUnlocks;
    //             }
    //             else
    //                 cost = (int)((double)__instance.cost * (double)TeamManager.GetLongstandingSolitudeItemCostScale());
    //         }
    //         if (component1.inventory.GetItemCount(DLC2Content.Items.LowerPricedChests) > 0 && __instance.saleStarCompatible)
    //         {
    //             int itemCount = activator.GetComponent<CharacterBody>().inventory.GetItemCount(DLC2Content.Items.LowerPricedChests);
    //             int num1 = 0;
    //             float percentChance = 1f;
    //             if (itemCount == 1)
    //             {
    //                 num1 = 0;
    //             }
    //             else
    //             {
    //                 for (int index = 1; index < 4; ++index)
    //                 {
    //                     if (index == 1)
    //                         percentChance = 30f;
    //                     else if (index == 2)
    //                         percentChance = 15f;
    //                     else if (index == 3)
    //                         percentChance = 1f;
    //                     if (itemCount >= 3)
    //                     {
    //                         float num2 = (float)(1.0 - 1.0 / ((double)itemCount * 0.05000000074505806 + 1.0));
    //                         if (Util.CheckRoll(percentChance + num2 * 100f, activator.GetComponent<CharacterBody>().master))
    //                             num1 = index;
    //                         else
    //                             break;
    //                     }
    //                     else if (Util.CheckRoll(percentChance, activator.GetComponent<CharacterBody>().master))
    //                         num1 = index;
    //                     else
    //                         break;
    //                 }
    //             }
    //             if ((bool)(UnityEngine.Object)__instance.GetComponent<ChestBehavior>())
    //                 __instance.GetComponent<ChestBehavior>().dropCount = 2 + num1;
    //             else if ((bool)(UnityEngine.Object)__instance.GetComponent<RouletteChestController>())
    //                 __instance.GetComponent<RouletteChestController>().dropCount = 2 + num1;
    //             component1.inventory.RemoveItem(DLC2Content.Items.LowerPricedChests, activator.GetComponent<CharacterBody>().inventory.GetItemCount(DLC2Content.Items.LowerPricedChests));
    //             component1.inventory.GiveItem(DLC2Content.Items.LowerPricedChestsConsumed, itemCount);
    //             CharacterMasterNotificationQueue.SendTransformNotification(component1.master, DLC2Content.Items.LowerPricedChests.itemIndex, DLC2Content.Items.LowerPricedChestsConsumed.itemIndex, CharacterMasterNotificationQueue.TransformationType.SaleStarRegen);
    //             int num3 = (int)Util.PlaySound("Play_item_proc_lowerPricedChest", __instance.gameObject);
    //         }
    //         CostTypeDef.PayCostResults payCostResults = costTypeDef.PayCost(cost, activator, __instance.gameObject, __instance.rng, avoidedItemIndex);
    //         foreach (ItemIndex itemIndex in payCostResults.itemsTaken)
    //         {
    //             PurchaseInteraction.CreateItemTakenOrb(component1.corePosition, __instance.gameObject, itemIndex);
    //             if (itemIndex != avoidedItemIndex)
    //             {
    //                 Action<PurchaseInteraction, Interactor> itemSpentOnPurchase = PurchaseInteraction.onItemSpentOnPurchase;
    //                 if (itemSpentOnPurchase != null)
    //                     itemSpentOnPurchase(__instance, activator);
    //             }
    //         }
    //         foreach (EquipmentIndex equipmentIndex in payCostResults.equipmentTaken)
    //         {
    //             Action<PurchaseInteraction, Interactor, EquipmentIndex> equipmentSpentOnPurchase = PurchaseInteraction.onEquipmentSpentOnPurchase;
    //             if (equipmentSpentOnPurchase != null)
    //                 equipmentSpentOnPurchase(__instance, activator, equipmentIndex);
    //         }
    //         IEnumerable<StatDef> statDefsToIncrement = ((IEnumerable<string>)__instance.purchaseStatNames).Select<string, StatDef>(new Func<string, StatDef>(StatDef.Find));
    //         StatManager.OnPurchase<IEnumerable<StatDef>>(component1, __instance.costType, statDefsToIncrement);
    //         __instance.onPurchase.Invoke(activator);
    //         __instance.lastActivator = activator;
    //     }
    // }
}