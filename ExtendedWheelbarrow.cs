using System;
using System.Collections;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using Mirror;
using UnityEngine;
using HarmonyLib;
using System.Reflection;


namespace ExtendedWheelbarrow {
    
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    public class ExtendedWheelbarrow : BaseUnityPlugin {

        public static ManualLogSource StaticLogger;
        public const string pluginGuid = "tinyresort.dinkum.extendedwheelbarrow";
        public const string pluginName = "Extended Wheelbarrow";
        public const string pluginVersion = "1.1.1";
        public static ConfigEntry<bool> debugMode;
        public static int maxDirt;
        public static int realTotalDirt;
        public static float dirtTime;
        public static bool setRealTotalDirt;

        private void Awake() {

            // Configuration
            var maxDirtEntry = Config.Bind<int>("General", "MaxDirt", 100, "The maximum number of shovels of dirt that can be emptied into the wheelbarrow before its full. RANGE: 10 - 10000");
            maxDirt = Mathf.Clamp(maxDirtEntry.Value, 10, 10000);
            debugMode = Config.Bind<bool>("General", "DebugMode", false, "If true, the BepinEx console will print out debug messages related to this mod.");

            #region Logging
            StaticLogger = Logger;
            BepInExInfoLogInterpolatedStringHandler handler = new BepInExInfoLogInterpolatedStringHandler(18, 1, out var flag);
            if (flag) { handler.AppendLiteral("Plugin " + pluginGuid + " (v" + pluginVersion + ") loaded!"); }
            StaticLogger.LogInfo(handler);
            #endregion

            #region Patching
            Harmony harmony = new Harmony(pluginGuid);

            MethodInfo removeDirt = AccessTools.Method(typeof(Wheelbarrow), "removeDirt");
            MethodInfo removeDirtPatch = AccessTools.Method(typeof(ExtendedWheelbarrow), "removeDirtPatch");

            MethodInfo updateContents = AccessTools.Method(typeof(Wheelbarrow), "updateContents");
            MethodInfo updateContentsPatch = AccessTools.Method(typeof(ExtendedWheelbarrow), "updateContentsPatch");

            MethodInfo OnStartClient = AccessTools.Method(typeof(Wheelbarrow), "OnStartClient");
            MethodInfo OnStartClientPatch = AccessTools.Method(typeof(ExtendedWheelbarrow), "OnStartClientPatch");
            
            MethodInfo isHoldingAShovel = AccessTools.Method(typeof(Wheelbarrow), "isHoldingAShovel");
            MethodInfo isHoldingAShovelPrefix = AccessTools.Method(typeof(ExtendedWheelbarrow), "isHoldingAShovelPrefix");
            
            MethodInfo insertDirt = AccessTools.Method(typeof(Wheelbarrow), "insertDirt");
            MethodInfo insertDirtPrefix = AccessTools.Method(typeof(ExtendedWheelbarrow), "insertDirtPrefix");

            harmony.Patch(removeDirt, new HarmonyMethod(removeDirtPatch));
            harmony.Patch(updateContents, new HarmonyMethod(updateContentsPatch));
            harmony.Patch(OnStartClient, new HarmonyMethod(OnStartClientPatch));
            harmony.Patch(isHoldingAShovel, new HarmonyMethod(isHoldingAShovelPrefix));
            harmony.Patch(insertDirt, new HarmonyMethod(insertDirtPrefix));
            #endregion

        }
        
        // Overrides the updateContents method to use our maxDirt setting instead of 10
        public static bool updateContentsPatch(Wheelbarrow __instance) {
            
            // If there's no dirt, hide the dirt model entirely
            if (__instance.totalDirt == 0) {
                __instance.dirtFillUp.gameObject.SetActive(false);
                return false;
            }
            
            // Otherwise, show the dirt model and size it appropriately
            __instance.dirtFillUp.gameObject.SetActive(true);
            float fillPercentage = 0.05f + (__instance.totalDirt / (float) maxDirt) * 0.95f;
            __instance.dirtFillUp.transform.localScale = new Vector3(fillPercentage >= 0.5f ? 1 : 0.7f, fillPercentage, fillPercentage >= 0.5f ? 1 : 0.7f);
            return false;
            
        }

        // When this method is called to make a check, we create a lie about the total dirt to allow more dirt to exist in the wheelbarrow
        [HarmonyPrefix]
        public static void isHoldingAShovelPrefix(Wheelbarrow __instance) {
            
            // This method is called twice if the player is adding dirt, but we only want to save the total dirt once
            if (Time.realtimeSinceStartup - dirtTime <= 0.005f) {
                if (debugMode.Value) { StaticLogger.LogWarning("Skipping setting real total dirt (Time: " + Time.time + ")"); }
                return;
            }
            dirtTime = Time.realtimeSinceStartup;
            
            // Logs the current amount of dirt
            if (debugMode.Value) 
                StaticLogger.LogInfo("Previous Total Dirt: " + __instance.totalDirt + "/" + __instance.layerIds.Length + " (Time: " + Time.time + ")");
            
            // Saves the true total amount of dirt so that its value can be used later instead of the game's value
            realTotalDirt = __instance.totalDirt;
            setRealTotalDirt = true;
            
            // If we have more than 9 dirt, temporarily set it to 9 to allow more dirt to be added
            if (__instance.totalDirt >= 10 && __instance.totalDirt < maxDirt) { __instance.totalDirt = 9; }
            
        }

        // When inserting dirt, this make sure the new total dirt is based on the true amount that we have saved previously
        [HarmonyPrefix]
        public static void insertDirtPrefix(Wheelbarrow __instance) {
            if (!setRealTotalDirt) {
                StaticLogger.LogError("The real total dirt isn't being set properly (Time: " + Time.time + ")");
                return;
            }
            __instance.totalDirt = realTotalDirt;
            setRealTotalDirt = false;
            if (debugMode.Value) {
                StaticLogger.LogInfo("Adding Dirt (Time: " + Time.time + ")");
                StaticLogger.LogInfo("New Total Dirt: " + (__instance.totalDirt + 1) + "/" + __instance.layerIds.Length);
            }
        }

        // When dirt is removed from the wheelbarrow, this makes sure our saved value is used
        // Also fixes a bug with the base game that caused the layers of dirt to be wrong when removing dirt
        public static bool removeDirtPatch(Wheelbarrow __instance) {
            __instance.NetworktotalDirt = realTotalDirt - 1;
            __instance.NetworktopDirtId = __instance.layerIds[Mathf.Clamp(__instance.totalDirt - 1, 0, maxDirt)];
            if (debugMode.Value) {
                StaticLogger.LogInfo("Removing Dirt (Time: " + Time.time + ")");
                StaticLogger.LogInfo("New Total Dirt: " + __instance.totalDirt + "/" + __instance.layerIds.Length);
            }
            return false;
        }

        // Sets the dirt layers array to be the correct length
        public static bool OnStartClientPatch(Wheelbarrow __instance) {
            if (__instance.layerIds.Length == 10) { __instance.layerIds = new int[maxDirt]; }
            return true;
        }

    }

}
