using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using MSC.CustomMeleeData;
using MSC.Dependencies;

namespace MSC
{
    [BepInPlugin("Dinorush." + MODNAME, MODNAME, "1.0.2")]
    [BepInDependency(MTFOWrapper.GUID, BepInDependency.DependencyFlags.SoftDependency)]
    internal sealed class EntryPoint : BasePlugin
    {
        public const string MODNAME = "MeleeSwingCustomization";

        public override void Load()
        {
            Log.LogMessage("Loading " + MODNAME);
            Configuration.Init();
            MeleeDataManager.Current.Init();
            new Harmony(MODNAME).PatchAll();
            Log.LogMessage("Loaded " + MODNAME);
        }
    }
}