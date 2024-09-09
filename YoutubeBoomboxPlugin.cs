using BepInEx;
using GameNetcodeStuff;
using HarmonyLib;
using LethalCompanyInputUtils.Components;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using BetterYoutubeBoombox.Providers;
using BetterYoutubeBoombox.Config;
using BetterYoutubeBoombox.Utils;
using BetterYoutubeBoombox.Managers;
using YoutubeDLSharp;
using static BetterYoutubeBoombox.Config.YoutubeBoomboxConfig;

namespace BetterYoutubeBoombox
{
    public class InfoCache : IProgress<string>
    {
        public class Info
        {
            public string id { get; set; }
            public float duration { get; set; }
        }

        public string Id { get; set; }

        public InfoCache(string id)
        {
            Id = id;

            PlaylistCache.Add(id, new List<string>());
        }

        public static Dictionary<string, float> DurationCache = new Dictionary<string, float>();
        public static Dictionary<string, List<string>> PlaylistCache = new Dictionary<string, List<string>>();

        public void Report(string value)
        {
            try
            {
                Info json = JsonConvert.DeserializeObject<Info>(value);

                if (!DurationCache.ContainsKey(json.id))
                {
                    DurationCache.Add(json.id, json.duration);
                }

                PlaylistCache[Id].Add(json.id);

            }
            catch { }
        }
    }


    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("com.rune580.LethalCompanyInputUtils", BepInDependency.DependencyFlags.HardDependency)]
    public class YoutubeBoomboxPlugin : BaseUnityPlugin
    {
        public static YoutubeBoomboxPlugin Instance { get; private set; }

        public static new YoutubeBoomboxConfig Config { get; private set; }

        private static Harmony Harmony { get; set; }

        internal static string DirectoryPath { get; private set; }

        internal static string DownloadsPath { get; private set; }

        public YoutubeDL YoutubeDL { get; private set; } = new YoutubeDL();

        internal static List<string> PathsThisSession { get; private set; } = new List<string>();

        internal static List<Provider> Providers { get; } = new List<Provider>();

        static PlayerControllerB PlayerControllerBInstance;

        public static string AssetsPath => Path.Combine(Paths.PluginPath, PluginInfo.PLUGIN_NAME, "Assets");

        public static void LogInfo(object data)
        {
            Instance.Logger.LogInfo(data);
        }

        public static void LogError(object data)
        {
            Instance.Logger.LogError(data);
        }

        public static void DebugLog(object data)
        {
            Instance.Logger.LogInfo(data);
        }

        //-----------------debug
        public void PrintChildren(Transform t, string addBefore = "-")
        {
            foreach (Transform child in t)
            {
                DebugLog($"{addBefore} {child.name}");
                Component[] components = child.gameObject.GetComponents(typeof(Component));
                foreach (Component component in components)
                {
                    DebugLog($"{addBefore} Component: {component}");
                }
                if (child.childCount > 0)
                    PrintChildren(child, $"{addBefore}-");
            }
        }
        //------------------------

        async void Awake()
        {
            Instance = this;
            Config = new(base.Config);

            AssetLoader.LoadAssetBundle(Path.Combine(AssetsPath, "boomboxuiasset"));

            string oldDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), "Youtube-Boombox");

            if (Directory.Exists(oldDirectoryPath))
            {
                Directory.Delete(oldDirectoryPath, true);
            }

            DirectoryPath = Path.Combine(Paths.PluginPath, PluginInfo.PLUGIN_NAME, "data");
            DownloadsPath = Path.Combine(DirectoryPath, "Downloads");

            if (!Directory.Exists(DirectoryPath)) 
                Directory.CreateDirectory(DirectoryPath);

            if (!Directory.Exists(DownloadsPath)) 
                Directory.CreateDirectory(DownloadsPath);

            if (DeleteDownloadsOnRestart.Value)
            {
                foreach (string file in Directory.GetFiles(DownloadsPath))
                {
                    File.Delete(file);
                }
            }

            if (!Directory.GetFiles(DirectoryPath).Any(file => file.Contains("yt-dl")))
                await YoutubeDLSharp.Utils.DownloadYtDlp(DirectoryPath);

            if (!Directory.GetFiles(DirectoryPath).Any(file => file.Contains("ffmpeg")))
                await YoutubeDLSharp.Utils.DownloadFFmpeg(DirectoryPath);

            YoutubeDL.YoutubeDLPath = Directory.GetFiles(DirectoryPath).First(file => file.Contains("yt-dl"));
            YoutubeDL.FFmpegPath = Directory.GetFiles(DirectoryPath).First(file => file.Contains("ffmpeg"));

            YoutubeDL.OutputFolder = DownloadsPath;
            YoutubeDL.OutputFileTemplate = "%(id)s.%(ext)s";

            Harmony = new Harmony($"{PluginInfo.PLUGIN_NAME}_{PluginInfo.PLUGIN_VERSION}");
            Harmony.PatchAll(typeof(Patches));
            Harmony.PatchAll();

            SetupNetworking();

            var method = new StackTrace().GetFrame(0).GetMethod();
            var assembly = method.ReflectedType.Assembly;

            foreach (Type t in AccessTools.GetTypesFromAssembly(assembly))
            {
                if (t.IsSubclassOf(typeof(Provider)))
                {
                    Providers.Add(Activator.CreateInstance(t) as Provider);
                }
            }
        }

        private void SetupNetworking()
        {
            var types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(GameNetworkManager), "Start")]
        class GameNetworkManagerPatch
        {
            public static void Postfix(GameNetworkManager __instance)
            {
                foreach (NetworkPrefab prefab in __instance.GetComponent<NetworkManager>().NetworkConfig.Prefabs.Prefabs)
                {
                    if (prefab.Prefab.GetComponent<BoomboxItem>() != null)
                    {
                        prefab.Prefab.AddComponent<BoomboxController>();

                        break;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(GrabbableObject), "UseItemOnClient")]
        class PreventRpc
        {
            public static bool IsBoomboxAndGUIShowing(GrabbableObject obj)
            {
                return obj is BoomboxItem && obj.gameObject.TryGetComponent(out BoomboxController controller) && controller.IsUIShowing();
            }

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                List<CodeInstruction> newInstructions = new List<CodeInstruction>(instructions);
                int index = newInstructions.FindLastIndex(i => i.opcode == OpCodes.Ldarg_0) - 1;

                System.Reflection.Emit.Label skipLabel = generator.DefineLabel();

                newInstructions[index].labels.Add(skipLabel);

                index = newInstructions.FindLastIndex(i => i.opcode == OpCodes.Brfalse_S) + 1;

                newInstructions.InsertRange(index, new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PreventRpc), nameof(PreventRpc.IsBoomboxAndGUIShowing))),
                    new CodeInstruction(OpCodes.Brtrue_S, skipLabel)
                });

                for (int z = 0; z < newInstructions.Count; z++) yield return newInstructions[z];
            }
        }

        [HarmonyPatch(typeof(BoomboxItem))]
        public class BoomboxPatch
        {
            internal static bool ShowingGUI { get; set; } = false;
            //internal static YoutubeBoomboxGUI ShownGUI { get; set; }
            internal static BoomboxItem CurrentBoombox { get; set; }

            [HarmonyPatch("StartMusic")]
            public static bool Prefix(BoomboxItem __instance, bool startMusic, bool pitchDown)
            {
                if (__instance.TryGetComponent(out BoomboxController controller))
                {
                    DebugLog($"Start music {startMusic}");
                    controller.ToggleBoombox(startMusic, pitchDown);
                    return false;
                }

                return true;
            }

            [HarmonyPatch("PocketItem")]
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var patchedInstructions = instructions.ToList();
                var skippedFirstCall = false;
                for (int i = 0; i < patchedInstructions.Count; i++)
                {
                    if (!skippedFirstCall)
                    {
                        if (patchedInstructions[i].opcode == OpCodes.Call)
                            skippedFirstCall = true;

                        continue;
                    }

                    if (patchedInstructions[i].opcode == OpCodes.Ret) break;
                    patchedInstructions[i].opcode = OpCodes.Nop;
                }
                return patchedInstructions;
            }

            [HarmonyPatch("Start")]
            public static void Prefix(BoomboxItem __instance)
            {
                DebugLog("A");

                if (!__instance.gameObject.GetComponent<BoomboxController>())
                {

                    __instance.gameObject.AddComponent<BoomboxController>();
                    __instance.itemProperties.syncInteractLRFunction = false;
                }

                DebugLog(__instance.TryGetComponent(out BoomboxController b));
            }
        }

        [HarmonyPatch(typeof(RemapContainerController))]
        public class RemapContainerControllerPatch
        {
            [HarmonyPatch("SaveOverrides")]
            public static void Prefix(RemapContainerController __instance)
            {
                if (StartOfRound.Instance != null && BoomboxController.Instance != null && StartOfRound.Instance.localPlayerController.currentlyHeldObjectServer == BoomboxController.Instance.Boombox)
                {
                    Instance.UpdateBoomBoxTips(BoomboxController.Instance.Boombox);
                }
            }
        }


        [HarmonyPatch(typeof(GrabbableObject))]
        internal class GrabbableObjectPatch
        {
            [HarmonyPatch("EquipItem")]
            public static void Postfix(GrabbableObject __instance)
            {
                if (__instance.GetType() == typeof(BoomboxItem))
                {
                    BoomboxItem boombox = (BoomboxItem) __instance;
                    Instance.UpdateBoomBoxTips(boombox);
                }
            }
        }

        void UpdateBoomBoxTips(BoomboxItem boombox)
        {
            List<string> newToolTips = new(boombox.itemProperties.toolTips);
            int index = newToolTips.FindIndex(x => x.Contains("Open YT GUI"));
            if (index == -1)
            {
                newToolTips.Add($"Open YT GUI: [{InputActionInstance.OpenBoomboxMenu.bindings[0].ToDisplayString()}]");
            }
            else
            {
                newToolTips[index] = $"Open YT GUI: [{InputActionInstance.OpenBoomboxMenu.bindings[0].ToDisplayString()}]";
            }

            boombox.itemProperties.toolTips = newToolTips.ToArray();

            boombox.SetControlTipsForItem();
        }


        [HarmonyPatch(typeof(PlayerControllerB))]
        public class PlayerControllerBPatch
        {
            [HarmonyPatch("ConnectClientToPlayerObject")]
            public static void Prefix(PlayerControllerB __instance)
            {
                PlayerControllerBInstance = __instance;
                InputActionInstance.OpenBoomboxMenu.performed += OpenBoomboxMenu;
            }

            [HarmonyPatch("OnDestroy")]
            public static void Postfix(PlayerControllerB __instance)
            {
                PlayerControllerBInstance = __instance;
                InputActionInstance.OpenBoomboxMenu.performed -= OpenBoomboxMenu;
            }
        }

        private static void OpenBoomboxMenu(InputAction.CallbackContext callbackContext)
        {
            if (StartOfRound.Instance != null && BoomboxController.Instance != null && StartOfRound.Instance.localPlayerController.currentlyHeldObjectServer == BoomboxController.Instance.Boombox && !BoomboxController.Instance.IsUIShowing())
            {
                DebugLog($"{callbackContext.performed} || {PlayerControllerBInstance != GameNetworkManager.Instance.localPlayerController} || {PlayerControllerBInstance.isPlayerDead}");

                if (!callbackContext.performed
                    || PlayerControllerBInstance != GameNetworkManager.Instance.localPlayerController
                    || PlayerControllerBInstance.isPlayerDead)
                    return;

                DebugLog("Opening boombox menu");
                BoomboxController.Instance.OpenMenu();
            }
        }
    }
}