using Gear;
using GTFO.API.Utilities;
using MSC.Dependencies;
using MSC.JSON;
using MSC.Utils;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using UnityEngine;

namespace MSC.CustomMeleeData
{
    public sealed class MeleeDataManager
    {
        public static readonly MeleeDataManager Current = new();

        private readonly Dictionary<string, List<MeleeData>> _fileData = new();
        private readonly Dictionary<uint, MeleeData> _idToData = new();
        private readonly Dictionary<uint, MeleeData> _cachedData = new();
        private readonly List<MeleeWeaponFirstPerson> _listeners = new();

        private readonly LiveEditListener? _liveEditListener;

        private void FileChanged(LiveEditEventArgs e)
        {
            DinoLogger.Warning($"LiveEdit File Changed: {e.FullPath}");
            LiveEdit.TryReadFileContent(e.FullPath, (content) =>
            {
                ReadFileContent(e.FullPath, content);
                PrintCustomIDs();
            });
        }

        private void FileDeleted(LiveEditEventArgs e)
        {
            DinoLogger.Warning($"LiveEdit File Removed: {e.FullPath}");

            RemoveFile(e.FullPath);
            PrintCustomIDs();
        }

        private void FileCreated(LiveEditEventArgs e)
        {
            DinoLogger.Warning($"LiveEdit File Created: {e.FullPath}");
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
                DinoLogger.Error($"Error parsing json " + file);
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
            for (int i = _listeners.Count - 1; i >= 0; --i)
            {
                if (_listeners[i] != null)
                    SetMeleeData(_listeners[i]);
                else
                    _listeners.RemoveAt(i);
            }
        }

        private bool SetMeleeData(MeleeWeaponFirstPerson melee)
        {
            uint id = melee.MeleeArchetypeData.persistentID;
            if (!_cachedData.TryGetValue(id, out var cachedData))
            {
                DinoLogger.Error($"Unable to get original data for melee {melee.name}, archetype ID {id}");
                return false;
            }

            Transform refAttack = melee.ModelData.m_damageRefAttack;
            Transform refPush = melee.ModelData.m_damageRefPush;
            if (_idToData.TryGetValue(id, out var customData))
            {
                refAttack.localPosition = customData.AttackOffset!.Value;
                refPush.localPosition = customData.PushOffset!.Value;
                return true;
            }
            else
            {
                refAttack.localPosition = cachedData.AttackOffset!.Value;
                refPush.localPosition = cachedData.PushOffset!.Value;
                return false;
            }
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
                DinoLogger.Log("No MTFO datablocks detected. Restricting to bat changes...");
                return;
            }

            string basePath = Path.Combine(MTFOWrapper.CustomPath, EntryPoint.MODNAME);
            if (!Directory.Exists(basePath))
            {
                DinoLogger.Log($"No directory detected. Creating {basePath}/Template.json");
                Directory.CreateDirectory(basePath);
                var file = File.CreateText(Path.Combine(basePath, "Template.json"));
                file.WriteLine(MSCJson.Serialize(new List<MeleeData>(MeleeDataTemplate.Template)));
                file.Flush();
                file.Close();
            }
            else
                DinoLogger.Log($"Directory detected. {basePath}");

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
        }

        internal void Init() { }

        public bool RegisterMelee(MeleeWeaponFirstPerson melee)
        {
            if (!MTFOWrapper.HasCustomDatablocks) return false;

            CacheData(melee);
            AddMeleeListener(melee);
            return SetMeleeData(melee);
        }

        public MeleeData? GetData(uint id) => _idToData.GetValueOrDefault(id);

        private void CacheData(MeleeWeaponFirstPerson melee)
        {
            uint id = melee.MeleeArchetypeData.persistentID;
            if (_cachedData.ContainsKey(id)) return;

            MeleeData cachedData = new()
            {
                ArchetypeID = id,
                AttackOffset = melee.ModelData.m_damageRefAttack.position,
                PushOffset = melee.ModelData.m_damageRefPush.position
            };
            _cachedData.Add(id, cachedData);

            FillDataWithCache(id);
        }

        private void FillDataWithCache(uint id)
        {
            if (!_idToData.TryGetValue(id, out var customData) || !_cachedData.TryGetValue(id, out var cachedData)) return;

            if (customData.AttackOffset == null)
                customData.AttackOffset = cachedData.AttackOffset;
            if (customData.PushOffset == null)
                customData.PushOffset = cachedData.PushOffset;
        }

        private void AddMeleeListener(MeleeWeaponFirstPerson melee)
        {
            // Prevent duplicates (not using IL2CPP list so don't trust Contains)
            if (_listeners.Any(listener => listener?.GetInstanceID() == melee.GetInstanceID())) return;

            _listeners.Add(melee);
        }
    }
}
