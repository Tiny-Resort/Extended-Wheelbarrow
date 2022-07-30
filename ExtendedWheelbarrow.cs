using System;
using System.Collections;
using System.Collections.Generic;
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
        public const string pluginVersion = "2.0.0";
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<int> nexusID;
        
        public static void Dbgl(string str = "", bool pref = true) {
            if (isDebug.Value) { StaticLogger.LogInfo(str); }
        }
        
        private void Awake() {

            // Configuration
            isDebug = Config.Bind<bool>("General", "DebugMode", false, "If true, the BepinEx console will print out debug messages related to this mod.");
            nexusID = Config.Bind<int>("General", "NexusID", 11, "Nexus Mod ID. You can find it on the mod's page on nexusmods.com");

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
            
            MethodInfo insertDirt = AccessTools.Method(typeof(Wheelbarrow), "insertDirt");
            MethodInfo insertDirtPrefix = AccessTools.Method(typeof(ExtendedWheelbarrow), "insertDirtPrefix");

            harmony.Patch(removeDirt, new HarmonyMethod(removeDirtPatch));
            harmony.Patch(updateContents, new HarmonyMethod(updateContentsPatch));
            harmony.Patch(insertDirt, new HarmonyMethod(insertDirtPrefix));
            #endregion

        }
        
        // Makes the dirt either empty or full
        public static bool updateContentsPatch(Wheelbarrow __instance) {
            __instance.dirtFillUp.gameObject.SetActive(__instance.totalDirt != 0);
            __instance.dirtFillUp.transform.localScale = new Vector3(1, 1, 1);
            return false;
        }

        // When inserting dirt, keep it at 1 but still change the dirt type
        [HarmonyPrefix]
        public static void insertDirtPrefix(Wheelbarrow __instance, int layerId) {
            __instance.totalDirt = 0;
            Dbgl($"Adding Dirt (Time: {Time.time})"); 
            Dbgl($"New Infinite Dirt Type: { layerId }");
        }

        // Makes sure dirt is never less than 1 when removing dirt
        public static bool removeDirtPatch(Wheelbarrow __instance) {
            __instance.NetworktotalDirt = 1;
            Dbgl($"Removing Dirt (Time: {Time.time})");
            return false;
        }

    }

}
