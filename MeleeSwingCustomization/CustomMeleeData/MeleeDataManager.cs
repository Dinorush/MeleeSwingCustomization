using GameData;
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
            for (int i = _listeners.Count - 1; i >= 0; --i)
            {
                if (_listeners[i] != null)
                {
                    SetMeleeData(_listeners[i]);
                    DebugUtil.DrawDebugSpheres(_listeners[i]);
                }
                else
                    _listeners.RemoveAt(i);
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
        }

        internal void Init() { }

        public bool RegisterMelee(MeleeWeaponFirstPerson melee)
        {
            if (!MTFOWrapper.HasCustomDatablocks) return false;

            CacheData(melee);
            AddMeleeListener(melee);
            return SetMeleeData(melee);
        }

        public bool HasData(uint id) => _idToData.ContainsKey(id);
        public MeleeData? GetData(uint id) => _idToData.GetValueOrDefault(id);

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
        }

        private void FillDataWithCache(uint id)
        {
            if (!_idToData.TryGetValue(id, out var customData) || !_cachedData.TryGetValue(id, out var cachedData)) return;

            if (!customData.AttackOffset.HasOffset)
                customData.AttackOffset.Offset = cachedData.AttackOffset.Offset;
            if (customData.PushOffset == null)
                customData.PushOffset = cachedData.PushOffset;
        }

        private void AddMeleeListener(MeleeWeaponFirstPerson melee)
        {
            // Prevent duplicates (not using IL2CPP list so don't trust Contains)
            if (_listeners.Any(listener => listener?.GetInstanceID() == melee.GetInstanceID())) return;

            _listeners.Add(melee);
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
                refAttack.localPosition = customData.AttackOffset.Offset;
                refPush.localPosition = customData.PushOffset!.Value;
                melee.m_attackDamageSphereDotScale = customData.AttackSphereCenterMod - 1f;
                SetMeleeAttackTimings(melee, customData);
                return true;
            }
            else
            {
                refAttack.localPosition = cachedData.AttackOffset.Offset;
                refPush.localPosition = cachedData.PushOffset!.Value;
                melee.m_attackDamageSphereDotScale = cachedData.AttackSphereCenterMod - 1f;
                SetMeleeAttackTimings(melee, cachedData);
                return false;
            }
        }

        private void SetMeleeAttackTimings(MeleeWeaponFirstPerson melee, MeleeData data)
        {
            float mod = 1f / data.LightAttackSpeed;
            var states = melee.m_states;
            var animData = melee.MeleeAnimationData;
            CopyMeleeData(states[(int)eMeleeWeaponState.AttackMissLeft].AttackData, animData.FPAttackMissLeft, mod);
            CopyMeleeData(states[(int)eMeleeWeaponState.AttackMissRight].AttackData, animData.FPAttackMissRight, mod);
            CopyMeleeData(states[(int)eMeleeWeaponState.AttackHitLeft].AttackData, animData.FPAttackHitLeft, mod);
            CopyMeleeData(states[(int)eMeleeWeaponState.AttackHitRight].AttackData, animData.FPAttackHitRight, mod);

            mod = 1f / data.ChargedAttackSpeed;
            CopyMeleeData(states[(int)eMeleeWeaponState.AttackChargeReleaseLeft].AttackData, animData.FPAttackChargeUpReleaseLeft, mod);
            CopyMeleeData(states[(int)eMeleeWeaponState.AttackChargeReleaseRight].AttackData, animData.FPAttackChargeUpReleaseRight, mod);
            CopyMeleeData(states[(int)eMeleeWeaponState.AttackChargeHitLeft].AttackData, animData.FPAttackChargeUpHitLeft, mod);
            CopyMeleeData(states[(int)eMeleeWeaponState.AttackChargeHitRight].AttackData, animData.FPAttackChargeUpHitRight, mod);

            mod = 1f / data.PushSpeed;
            CopyMeleeData(states[(int)eMeleeWeaponState.Push].AttackData, animData.FPAttackPush, mod);
        }

        private static void CopyMeleeData(MeleeAttackData data, MeleeAnimationSetDataBlock.MeleeAttackData animData, float mod = 1f)
        {
            data.m_attackLength = animData.AttackLengthTime * mod;
            data.m_attackHitTime = animData.AttackHitTime * mod;
            data.m_damageStartTime = animData.DamageStartTime * mod;
            data.m_damageEndTime = animData.DamageEndTime * mod;
            data.m_attackCamFwdHitTime = animData.AttackCamFwdHitTime * mod;
            data.m_comboEarlyTime = animData.ComboEarlyTime * mod;
        }
    }
}
