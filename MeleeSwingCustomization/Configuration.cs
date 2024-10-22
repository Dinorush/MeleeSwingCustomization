using BepInEx.Configuration;
using BepInEx;
using System.IO;
using GTFO.API.Utilities;
using Gear;
using MSC.Utils;

namespace MSC
{
    internal static class Configuration
    {
        public static bool ImproveBatHitbox = true;
        public static bool DrawDebugHitbox = false;
        public static float DebugSphereSize = 0.1f;

        private readonly static ConfigFile configFile;

        static Configuration()
        {
            configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, EntryPoint.MODNAME + ".cfg"), saveOnInit: true);
            BindAll(configFile);
        }

        internal static void Init()
        {
            LiveEditListener listener = LiveEdit.CreateListener(Paths.ConfigPath, EntryPoint.MODNAME + ".cfg", false);
            listener.FileChanged += OnFileChanged;
        }

        private static void OnFileChanged(LiveEditEventArgs _)
        {
            configFile.Reload();
            string section = "Base Settings";
            ImproveBatHitbox = (bool)configFile[section, "Improve Bat Hitbox"].BoxedValue;

            section = "Test Settings";
            MeleeWeaponFirstPerson.DEBUG_TARGETING_ENABLED = (bool)configFile[section, "Show Vanilla Debug Swing Hitbox"].BoxedValue;
            DrawDebugHitbox = (bool)configFile[section, "Show Debug Hitbox Positions"].BoxedValue;
            DebugSphereSize = (float)configFile[section, "Debug Hitbox Size"].BoxedValue;
            DebugUtil.DrawDebugSpheres();
        }

        private static void BindAll(ConfigFile config)
        {
            string section = "Base Settings";
            ImproveBatHitbox = config.Bind(section, "Improve Bat Hitbox", ImproveBatHitbox, "Improves the hitbox position of bat melee weapons.").Value;

            section = "Test Settings";
            MeleeWeaponFirstPerson.DEBUG_TARGETING_ENABLED = config.Bind(section, "Show Vanilla Debug Swing Hitbox", false, "Enables the base game debug melee swing hitbox visuals.").Value;
            DrawDebugHitbox = config.Bind(section, "Show Debug Hitbox Positions", DrawDebugHitbox, "Shows visuals of the held melee weapon's attack offset.\nAlso shows the capsule endpoint if it exists").Value;
            DebugSphereSize = config.Bind(section, "Debug Hitbox Size", DebugSphereSize, "Size of the rendered Hitbox Positions.").Value;
        }
    }
}
