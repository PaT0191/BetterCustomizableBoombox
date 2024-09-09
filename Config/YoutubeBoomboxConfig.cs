using BepInEx.Configuration;
using UnityEngine;
using BetterYoutubeBoombox.Inputs;
using BetterYoutubeBoombox.Utils;

namespace BetterYoutubeBoombox.Config
{
    public class YoutubeBoomboxConfig
    {
        internal static ConfigEntry<int> MaxCachedDownloads { get; private set; }
        internal static ConfigEntry<bool> DeleteDownloadsOnRestart { get; private set; }
        internal static ConfigEntry<float> MaxSongDuration { get; private set; }

        internal static YoutubeBoomboxInputs InputActionInstance { get; private set; }

        public YoutubeBoomboxConfig(ConfigFile cfg)
        {
            MaxCachedDownloads = cfg.Bind(new ConfigDefinition("General", "Max Cached Downloads"), 10, new ConfigDescription("The maximum number of downloaded songs that can be saved before deleting.", new ConfigNumberClamper(1, 100)));
            DeleteDownloadsOnRestart = cfg.Bind("General", "Delete Downloads On Restart", true, "Whether or not to delete downloads when your game starts again.");
            MaxSongDuration = cfg.Bind("General", "Max Song Duration", 600f, "Maximum song duration in seconds. Any video longer than this will not be downloaded.");

            InputActionInstance = new YoutubeBoomboxInputs();
        }
    }
}