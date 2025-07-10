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
}