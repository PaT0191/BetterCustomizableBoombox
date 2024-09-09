using UnityEngine;
using static BetterYoutubeBoombox.YoutubeBoomboxPlugin;

namespace BetterYoutubeBoombox.Utils
{
    public static class AssetLoader
    {
        public static AssetBundle AssetBundle { get; private set; }
        public static GameObject UIPrefab { get; private set; }

        public static void LoadAssetBundle(string assetBundlePath)
        {
            AssetBundle = AssetBundle.LoadFromFile(assetBundlePath);

            /*DebugLog(AssetBundle);
            string[] assetNames = AssetBundle.GetAllAssetNames();
            for (int i = 0; i < assetNames.Length; i++)
                DebugLog(assetNames[i]);*/

            UIPrefab = AssetBundle.LoadAsset<GameObject>("BoomboxMenu");

            //Instance.PrintChildren(UIPrefab.transform);
        }

    }
}