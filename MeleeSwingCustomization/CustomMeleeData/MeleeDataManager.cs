using Gear;
using GTFO.API.Utilities;
using ModifierAPI;
using MSC.Dependencies;
using MSC.JSON;
using MSC.Utils;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Text.Json;
using UnityEngine;

namespace MSC.CustomMeleeData
{
    public sealed class MeleeDataManager
    {
        public static readonly MeleeDataManager Current = new();

        public MeleeData? ActiveData { get; private set; } = null;
        public float CurrentRangeMod { get; private set; } = 1f;

        private readonly Dictionary<string, List<MeleeData>> _fileData = new();
        private readonly Dictionary<uint, MeleeData> _idToData = new();
        private readonly Dictionary<uint, MeleeData> _cachedData = new();
        private readonly Dictionary<string, MeleeData> _prefabToCachedData = new();

        private MeleeWeaponFirstPerson? _activeMelee = null;

        // Added by external plugins
        private readonly Dictionary<string, MeleeData> _prefabToData = new();

        private readonly (IStatModifier light, IStatModifier charged, IStatModifier push) _speedModifiers = (
            MeleeAttackSpeedAPI.AddLightModifier(1f, group: "MSC"),
            MeleeAttackSpeedAPI.AddChargedAnimationModifier(1f, group: "MSC"),
            MeleeAttackSpeedAPI.AddPushModifier(1f, group: "MSC")
            );

        private readonly LiveEditListener? _liveEditListener;

        private void FileChanged(LiveEditEventArgs e)
        {
            DinoLogger.Warning($"LiveEdit File Changed: {e.FileName}");
            LiveEdit.TryReadFileContent(e.FullPath, (content) =>
            {
                ReadFileContent(e.FullPath, content);
                PrintCustomIDs();
            });
        }

        private void FileDeleted(LiveEditEventArgs e)
        {
            DinoLogger.Warning($"LiveEdit File Removed: {e.FileName}");

            RemoveFile(e.FullPath);
            PrintCustomIDs();
        }

        private void FileCreated(LiveEditEventArgs e)
        {
            DinoLogger.Warning($"LiveEdit File Created: {e.FileName}");
            LiveEdit.TryReadFileContent(e.FullPath, (content) =>
            {
                ReadFileContent(e.FullPath, content);
                PrintCustomIDs();
            });
        }

        private void ReadFileContent(string file, string content)
        {
            RemoveFile(file);

            List<MeleeData>? dataList = null;
            try
            {
                dataList = MSCJson.Deserialize<List<MeleeData>>(content);
            }
            catch (JsonException ex)
            {
                DinoLogger.Error($"Error parsing json " + Path.GetFileName(file));
                DinoLogger.Error(ex.Message);
            }
            if (dataList == null) return;

            AddFile(file, dataList);
        }

        private void RemoveFile(string file)
        {
            if (!_fileData.ContainsKey(file)) return;

            foreach (MeleeData data in _fileData[file])
            {
                _idToData.Remove(data.ArchetypeID);
            }
            _fileData.Remove(file);

            ResetListeners();
        }

        private void AddFile(string file, List<MeleeData> dataList)
        {
            _fileData.Add(file, dataList);
            foreach (MeleeData data in dataList)
            {
                if (data.ArchetypeID != 0)
                {
                    if (_idToData.ContainsKey(data.ArchetypeID))
                        DinoLogger.Warning($"Duplicate archetype ID {data.ArchetypeID} detected. Previous name: {_idToData[data.ArchetypeID].Name}, new name: {data.Name}");
                    _idToData[data.ArchetypeID] = data;
                    FillDataWithCache(data.ArchetypeID);
                }
            }

            ResetListeners();
        }

        private void ResetListeners()
        {
            if (_activeMelee == null) return;

            SetActiveMelee(_activeMelee);
            DebugUtil.DrawDebugSpheres(_activeMelee);
        }

        private void PrintCustomIDs()
        {
            if (_idToData.Count > 0)
            {
                StringBuilder builder = new($"Found custom blocks for archetype IDs: ");
                builder.AppendJoin(", ", _idToData.Keys.ToImmutableSortedSet());
                DinoLogger.Log(builder.ToString());
            }
        }

        private MeleeDataManager()
        {
            if (!MTFOWrapper.HasCustomDatablocks)
            {
                DinoLogger.Log("No MTFO datablocks detected. Restricting to API...");
                return;
            }

            string basePath = Path.Combine(MTFOWrapper.CustomPath, EntryPoint.MODNAME);
            if (!Directory.Exists(basePath))
            {
                DinoLogger.Log($"No directory detected. Creating template.");
                Directory.CreateDirectory(basePath);
                var file = File.CreateText(Path.Combine(basePath, "Template.json"));
                file.WriteLine(MSCJson.Serialize(new List<MeleeData>(MeleeDataTemplate.Template)));
                file.Flush();
                file.Close();
            }
            else
                DinoLogger.Log($"Directory detected.");

            foreach (string confFile in Directory.EnumerateFiles(basePath, "*.json", SearchOption.AllDirectories))
            {
                string content = File.ReadAllText(confFile);
                ReadFileContent(confFile, content);
            }
            PrintCustomIDs();

            _liveEditListener = LiveEdit.CreateListener(basePath, "*.json", true);
            _liveEditListener.FileCreated += FileCreated;
            _liveEditListener.FileChanged += FileChanged;
            _liveEditListener.FileDeleted += FileDeleted;

            MeleeRangeAPI.SetRangeOverride((baseRange, mod) =>
            {
                CurrentRangeMod = mod;
                if (ActiveData == null) return true;
                
                return !ActiveData.AttackOffset.HasEntityRay;
            });
        }

        internal void Init() { }

        public bool RegisterMelee(MeleeWeaponFirstPerson melee)
        {
            CacheData(melee);
            return SetActiveMelee(melee);
        }

        private bool SetActiveMelee(MeleeWeaponFirstPerson melee)
        {
            _activeMelee = melee;
            if (SetMeleeData(melee))
            {
                ActiveData = GetData(melee);
                MeleeRangeAPI.RefreshRange();
                return true;
            }
            else
            {
                ActiveData = null;
                MeleeRangeAPI.RefreshRange();
                return false;
            }
        }

        public bool HasData(uint id) => _idToData.ContainsKey(id);
        public MeleeData? GetData(MeleeWeaponFirstPerson melee)
        {
            if (!_idToData.TryGetValue(melee.MeleeArchetypeData.persistentID, out var data))
            {
                var prefabs = melee.ItemDataBlock.FirstPersonPrefabs;
                if (prefabs?.Count > 0)
                    return _prefabToData.GetValueOrDefault(prefabs[0]);
            }
            return data;
        }

        public void AddDataForPrefab(string prefab, MeleeData data)
        {
            if (_prefabToData.ContainsKey(prefab))
                DinoLogger.Warning($"Duplicate prefab data {prefab} detected. Previous name: {_prefabToData[prefab].Name}, new name: {data.Name}");
            _prefabToData[prefab] = data;
            FillDataWithCache(prefab);
        }

        private void CacheData(MeleeWeaponFirstPerson melee)
        {
            uint id = melee.MeleeArchetypeData.persistentID;
            if (_cachedData.ContainsKey(id)) return;

            MeleeData cachedData = new()
            {
                ArchetypeID = id,
                AttackOffset = new(melee.ModelData.m_damageRefAttack.localPosition),
                PushOffset = melee.ModelData.m_damageRefPush.localPosition
            };
            _cachedData.Add(id, cachedData);
            FillDataWithCache(id);

            var prefabs = melee.ItemDataBlock.FirstPersonPrefabs;
            if (prefabs?.Count > 0)
            {
                _prefabToCachedData.TryAdd(prefabs[0], cachedData);
                FillDataWithCache(prefabs[0]);
            }
        }

        private void FillDataWithCache(string prefab)
        {
            if (_prefabToData.TryGetValue(prefab, out var customData) && _prefabToCachedData.TryGetValue(prefab, out var cachedData))
                FillDataWithCache(customData, cachedData);
        }
        private void FillDataWithCache(uint id)
        {
            if (_idToData.TryGetValue(id, out var customData) && _cachedData.TryGetValue(id, out var cachedData))
                FillDataWithCache(customData, cachedData);
        }
        private void FillDataWithCache(MeleeData customData, MeleeData cachedData)
        {
            if (!customData.AttackOffset.HasOffset)
                customData.AttackOffset.Offset = cachedData.AttackOffset.Offset;
            if (customData.PushOffset == null)
                customData.PushOffset = cachedData.PushOffset;
        }

        private bool SetMeleeData(MeleeWeaponFirstPerson melee)
        {
            uint id = melee.MeleeArchetypeData.persistentID;
            if (!_cachedData.TryGetValue(id, out var cachedData))
            {
                DinoLogger.Error($"Unable to get original data for melee {melee.name}, archetype ID {id}");
                return false;
            }

            if (!_idToData.TryGetValue(id, out MeleeData? customData))
            {
                var prefabs = melee.ItemDataBlock.FirstPersonPrefabs;
                if (prefabs.Count > 0)
                    customData = _prefabToData.GetValueOrDefault(prefabs[0]);
            }

            Transform refAttack = melee.ModelData.m_damageRefAttack;
            Transform refPush = melee.ModelData.m_damageRefPush;
            if (customData != null)
            {
                refAttack.localPosition = customData.AttackOffset.Offset;
                refPush.localPosition = customData.PushOffset!.Value;
                melee.m_attackDamageSphereDotScale = customData.AttackSphereCenterMod - 1f;
                _speedModifiers.light.Enable(customData.LightAttackSpeed);
                _speedModifiers.charged.Enable(customData.ChargedAttackSpeed);
                _speedModifiers.push.Enable(customData.PushSpeed);
                return true;
            }
            else
            {
                refAttack.localPosition = cachedData.AttackOffset.Offset;
                refPush.localPosition = cachedData.PushOffset!.Value;
                melee.m_attackDamageSphereDotScale = cachedData.AttackSphereCenterMod - 1f;
                _speedModifiers.light.Disable();
                _speedModifiers.charged.Disable();
                _speedModifiers.push.Disable();
                return false;
            }
        }
    }
}
