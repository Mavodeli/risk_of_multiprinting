using BepInEx;
using BepInEx.Logging;
using EntityStates.Duplicator;
using EntityStates.Scrapper;
using RoR2;
using HarmonyLib;
using risk_of_multiprinting.patches.duplicating;
using risk_of_multiprinting.patches.scraplimit;

namespace risk_of_multiprinting;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("Risk of Rain 2.exe")]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    private Harmony harmonyPatcher;

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        RoR2.Stage.onStageStartGlobal += delegate (Stage self)
        {
            Logger.LogInfo("Patching scrapper duration values");
            WaitToBeginScrapping.duration = 0.1f;
            Scrapping.duration = 0.05f;
            ScrappingToIdle.duration = 0.1f;
        };

        Logger.LogInfo("Patching DLLs:");
        harmonyPatcher = new Harmony(MyPluginInfo.PLUGIN_GUID);

        Logger.LogInfo("Patching Duplicating");
        harmonyPatcher.CreateClassProcessor(typeof(DuplicatingPatch)).Patch();

        Logger.LogInfo("Patching Scrap Limit");
        harmonyPatcher.CreateClassProcessor(typeof(ScrapLimitPatch)).Patch();
    }
}
