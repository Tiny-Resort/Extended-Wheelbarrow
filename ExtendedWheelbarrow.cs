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
        
        public const string pluginGuid = "tinyresort.dinkum.extendedwheelbarrow";
        public const string pluginName = "Extended Wheelbarrow";
        public const string pluginVersion = "1.0.0";
        public static Wheelbarrow wheelbarrow;

        private void Awake() {

            #region Logging
            ManualLogSource logger = Logger;

            bool flag;
            BepInExInfoLogInterpolatedStringHandler handler = new BepInExInfoLogInterpolatedStringHandler(18, 1, out flag);
            if (flag) { handler.AppendLiteral("Plugin " + pluginGuid + " (v" + pluginVersion + ") loaded!"); }
            logger.LogInfo(handler);
            #endregion


            #region Patching
            Harmony harmony = new Harmony(pluginGuid);

            MethodInfo removeDirt = AccessTools.Method(typeof(Wheelbarrow), "removeDirt");
            MethodInfo removeDirtPatch = AccessTools.Method(typeof(ExtendedWheelbarrow), "removeDirtPatch");

            MethodInfo insertDirt = AccessTools.Method(typeof(Wheelbarrow), "insertDirt");
            MethodInfo insertDirtPatch = AccessTools.Method(typeof(ExtendedWheelbarrow), "insertDirtPatch");

            MethodInfo updateContents = AccessTools.Method(typeof(Wheelbarrow), "updateContents");
            MethodInfo updateContentsPatch = AccessTools.Method(typeof(ExtendedWheelbarrow), "updateContentsPatch");

            MethodInfo OnStartClient = AccessTools.Method(typeof(Wheelbarrow), "OnStartClient");
            MethodInfo OnStartClientPatch = AccessTools.Method(typeof(ExtendedWheelbarrow), "OnStartClientPatch");

            harmony.Patch(removeDirt, new HarmonyMethod(removeDirtPatch));
            harmony.Patch(insertDirt, new HarmonyMethod(insertDirtPatch));
            harmony.Patch(updateContents, new HarmonyMethod(updateContentsPatch));
            harmony.Patch(OnStartClient, new HarmonyMethod(OnStartClientPatch));
            #endregion

        }

        public void Update() {
            Logger.LogInfo("Dirt: " + wheelbarrow.totalDirt + "/" + wheelbarrow.layerIds.Length);
            //NotificationManager.manage.createChatNotification("Dirt: " + (string)wheelbarrow.totalDirt);
        }


        public static bool removeDirtPatch(Wheelbarrow __instance) {
            __instance.NetworktotalDirt = __instance.totalDirt - 1;
            __instance.NetworktopDirtId = __instance.layerIds[Mathf.Clamp(__instance.totalDirt - 10, 0, 20)];
            return false;
        }

        public static bool insertDirtPatch(Wheelbarrow __instance, int layerId) {
            __instance.NetworktopDirtId = layerId;
            __instance.layerIds[__instance.totalDirt] = layerId;
            __instance.NetworktotalDirt = __instance.totalDirt + 1;
            return false;
        }
        public static bool updateContentsPatch(Wheelbarrow __instance) {
            if (__instance.totalDirt == 0) {
                __instance.dirtFillUp.gameObject.SetActive(false);
                return false;
            }
            __instance.dirtFillUp.gameObject.SetActive(true);
            if ((float)__instance.totalDirt / 20f >= 0.5f) {
                __instance.dirtFillUp.transform.localScale = new Vector3(1f, (float)__instance.totalDirt / 20f, 1f);
                return false;
            }
            __instance.dirtFillUp.transform.localScale = new Vector3(0.7f, (float)__instance.totalDirt / 20f, 0.7f);
            return false;
        }

        public static bool OnStartClientPatch(Wheelbarrow __instance) {
            wheelbarrow = __instance;
            if (__instance.layerIds.Length == 10) {
                __instance.layerIds = new int[20];
            }
            return true;
        }

    }

}
