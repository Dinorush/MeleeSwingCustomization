using BepInEx.Unity.IL2CPP;
using MTFO.API;

namespace MSC.Dependencies
{
    internal static class MTFOWrapper
    {
        public const string GUID = "com.dak.MTFO";

        public static string GameDataPath => MTFOPathAPI.RundownPath;
        public static string CustomPath => MTFOPathAPI.CustomPath;
        public static bool HasCustomDatablocks => HasMTFO && UnsafeHasCustomDatablocks;
        private static bool UnsafeHasCustomDatablocks => MTFOPathAPI.HasRundownPath;
        public static bool HasMTFO { get; private set; }

        static MTFOWrapper()
        {
            HasMTFO = IL2CPPChainloader.Instance.Plugins.ContainsKey(GUID);
        }
    }
}
