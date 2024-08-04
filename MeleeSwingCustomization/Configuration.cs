using BepInEx.Configuration;
using BepInEx;
using System.IO;
using GTFO.API.Utilities;
using Gear;

namespace MSC
{
    internal static class Configuration
    {
        public static bool ImproveBatHitbox = true;

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
            MeleeWeaponFirstPerson.DEBUG_TARGETING_ENABLED = (bool)configFile[section, "Improve Bat Hitbox"].BoxedValue;

            section = "Test Settings";
            MeleeWeaponFirstPerson.DEBUG_TARGETING_ENABLED = (bool)configFile[section, "Show Debug Targeting"].BoxedValue;
        }

        private static void BindAll(ConfigFile config)
        {
            string section = "Base Settings";
            ImproveBatHitbox = config.Bind(section, "Improve Bat Hitbox", ImproveBatHitbox, "Improves the hitbox position of bat melee weapons.").Value;

            section = "Test Settings";
            MeleeWeaponFirstPerson.DEBUG_TARGETING_ENABLED = config.Bind(section, "Show Debug Targeting", false, "Enables the base game debug melee hitbox visuals.").Value;
        }
    }
}
