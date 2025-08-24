using BepInEx;
using BepInEx.Logging;
using EntityStates.Scrapper;
using RoR2;
using HarmonyLib;
using risk_of_multiprinting.patches.duplicating;
using risk_of_multiprinting.patches.scraplimit;
using R2API.Networking;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace risk_of_multiprinting;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("Risk of Rain 2.exe")]
[BepInDependency(NetworkingAPI.PluginGUID)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    private Harmony harmonyPatcher;
    public static PluginInfo PInfo { get; private set; }
    public static InputHandler inputHandler;

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;

        PInfo = Info;
        risk_of_multiprinting.patches.duplicating.AmountSelectorAsset.Init();

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
        NetworkingAPI.RegisterMessageType<SendCustomAmountRPC>();
        harmonyPatcher.CreateClassProcessor(typeof(DuplicatingPatch)).Patch();

        Logger.LogInfo("Patching Scrap Limit");
        harmonyPatcher.CreateClassProcessor(typeof(ScrapLimitPatch)).Patch();

        inputHandler = new InputHandler();
        inputHandler.inputSystem = UnityInput.Current;
    }

    private void Update()
    {
        inputHandler.update();
    }
}

public class InputHandler
{
    public IInputSystem inputSystem;

    public static class KeyBindings
    {
        public static KeyCode[] confirmButtons = { KeyCode.F };
        public static KeyCode[] abortButtons = { KeyCode.Escape, KeyCode.Space, KeyCode.E, KeyCode.Return, KeyCode.KeypadEnter, KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D };
    }

    private List<(Action onConfirm, Action onAbort)> activeListeners = new List<(Action onConfirm, Action onAbort)>();

    public void registerListener(Action onConfirm, Action onAbort)
    {
        activeListeners.Add((onConfirm, onAbort));
    }

    public void abort()
    {
        foreach ((Action onConfirm, Action onAbort) actions in activeListeners)
        {
            actions.onAbort.Invoke();
        }
        activeListeners.Clear();
    }

    public void confirm()
    {
        foreach ((Action onConfirm, Action onAbort) actions in activeListeners)
        {
            actions.onConfirm.Invoke();
        }
        activeListeners.Clear();
    }

    public void update()
    {
        foreach (KeyCode button in KeyBindings.abortButtons)
        {
            if (inputSystem.GetKeyDown(button))
            {
                abort();
                return;
            }
        }
        foreach (KeyCode button in KeyBindings.confirmButtons)
        {
            if (inputSystem.GetKeyDown(button))
            {
                confirm();
                return;
            }
        }
    }
}