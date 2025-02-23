using GameData;
using Gear;
using MSC.JSON;
using MSC.Utils;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using UnityEngine;

namespace MSC.CustomMeleeData
{
    public sealed class OffsetData
    {
        private Vector3? _offset = null;
        public Vector3 Offset { get => _offset!.Value; set => _offset = value; }

        public Vector3? CapsuleOffset { get; set; } = null;
        public Vector3? CapsuleOffsetEnd { get; set; } = null;
        public bool CapsuleUseCamFwd { get; set; } = false;
        public float CapsuleCamFwdAdd { get; set; } = 0f;
        public float CapsuleSize { get; set; } = 0f;
        public bool CapsuleUseCenterMod { get; set; } = false;
        public float CapsuleDelay { get; set; } = 0f;
        public Dictionary<eMeleeWeaponState, float>? CapsuleStateDelay { get; private set; } = null;

        public OffsetData(Vector3? offset = null)
        {
            _offset = offset;
        }

        public OffsetData(float x, float y, float z)
        {
            _offset = new(x, y, z);
        }

        public OffsetData((Vector3?, Vector3?) offsets)
        {
            _offset = offsets.Item1;
            CapsuleOffset = offsets.Item2;
        }

        public bool HasOffset => _offset != null;
        public bool HasCapsule => CapsuleUseCamFwd || CapsuleOffset != null;

        public float GetCapsuleDelay(eMeleeWeaponState state)
        {
            float delay = CapsuleDelay;
            if (CapsuleStateDelay != null && CapsuleStateDelay.ContainsKey(state))
                delay = CapsuleStateDelay[state];
            return delay;
        }

        public float GetCapsuleSize(MeleeArchetypeDataBlock data, float dotScale = 0)
        {
            float size = CapsuleSize > 0 ? CapsuleSize : data.AttackSphereRadius;
            return CapsuleUseCenterMod ? size * (1f + dotScale) : size;
        }

        public (Vector3 start, Vector3 end) GetCapsuleOffsets(Transform transform, MeleeArchetypeDataBlock data)
        {
            Vector3 localPos = transform.localPosition;
            Vector3 parentPos = transform.parent.position;
            Quaternion parentRot = transform.parent.rotation;
            if (CapsuleUseCamFwd)
            {
                float camFwdLength = CapsuleCamFwdAdd + data.CameraDamageRayLength;
                
                if (CapsuleOffset == null)
                {
                    return ( // Zero pos -> pos direction * cam length
                            parentPos,
                            parentPos + parentRot * localPos.normalized * camFwdLength
                            );
                }

                if (CapsuleOffsetEnd == null)
                {
                    return ( // Capsule pos -> (pos - capsule pos) direction * cam length
                        parentPos + parentRot * CapsuleOffset.Value,
                        parentPos + parentRot * (localPos - CapsuleOffset.Value).normalized * camFwdLength
                        );
                }

                return ( // Capsule pos -> (pos - capsule pos) direction * cam length
                        parentPos + parentRot * CapsuleOffset.Value,
                        parentPos + parentRot * (CapsuleOffsetEnd.Value - CapsuleOffset.Value).normalized * camFwdLength
                        );
            }

            return ( // Capsule pos -> capsule pos end if exists, otherwise pos
                    parentPos + parentRot * CapsuleOffset!.Value,
                    CapsuleOffsetEnd != null ? parentPos + parentRot * CapsuleOffsetEnd.Value : transform.position
                    );
        }

        public void Serialize(Utf8JsonWriter writer)
        {
            if (_offset == null)
            {
                writer.WriteNullValue();
                return;
            }

            if (CapsuleOffset == null)
            {
                MSCJson.Serialize(_offset, writer);
                return;
            }

            writer.WriteStartObject();
            writer.WritePropertyName(nameof(Offset));
            if (_offset != null)
            {
                StringBuilder builder = new(MSCJson.Serialize(_offset)[1..^1]);
                if (CapsuleOffset != null)
                    builder.Append(" " + MSCJson.Serialize(CapsuleOffset)[1..^1]);
                if (CapsuleOffsetEnd != null)
                    builder.Append(" " + MSCJson.Serialize(CapsuleOffsetEnd)[1..^1]);
                writer.WriteStringValue(builder.ToString());
            }
            else
                writer.WriteNullValue();

            writer.WritePropertyName(nameof(CapsuleOffset));
            if (CapsuleOffset != null && _offset == null)
            {
                StringBuilder builder = new(MSCJson.Serialize(CapsuleOffset)[1..^1]);
                if (CapsuleOffsetEnd != null)
                    builder.Append(" " + MSCJson.Serialize(CapsuleOffsetEnd)[1..^1]);
                writer.WriteStringValue(builder.ToString());
            }
            else
                writer.WriteNullValue();

            writer.WriteBoolean(nameof(CapsuleUseCamFwd), CapsuleUseCamFwd);
            writer.WriteNumber(nameof(CapsuleCamFwdAdd), CapsuleCamFwdAdd);
            writer.WriteNumber(nameof(CapsuleSize), CapsuleSize);
            writer.WriteBoolean(nameof(CapsuleUseCenterMod), CapsuleUseCenterMod);
            writer.WriteNumber(nameof(CapsuleDelay), CapsuleDelay);
            writer.WriteEndObject();
        }

        public void DeserializeProperty(string propertyName, ref Utf8JsonReader reader)
        {
            switch (propertyName)
            {
                case "offsets":
                case "offset":
                    ParseOffsetTriplet(reader.GetString()!.Trim());
                    break;
                case "capsuleoffset":
                    ParseCapsuleOffset(reader.GetString()!.Trim());
                    break;
                case "capsuleusecamfwd":
                    CapsuleUseCamFwd = reader.GetBoolean();
                    break;
                case "capsulecamfwdadd":
                    CapsuleCamFwdAdd = reader.GetSingle();
                    break;
                case "capsulesize":
                    CapsuleSize = reader.GetSingle();
                    break;
                case "capsuleusecentermod":
                    CapsuleUseCenterMod = reader.GetBoolean();
                    break;
                case "capsuledelay":
                    ParseDamageDelays(ref reader);
                    break;
            }
        }

        public bool ParseOffsetTriplet(string text)
        {
            if (TryParseVector3OffsetTriplet(text, out var vectors))
            {
                _offset = vectors.Item1;
                CapsuleOffset = vectors.Item2;
                CapsuleOffsetEnd = vectors.Item3;
                return true;
            }
            return false;
        }

        private bool ParseCapsuleOffset(string text)
        {
            if (TryParseVector3OffsetTriplet(text, out var vectors))
            {
                CapsuleOffset = vectors.Item1;
                CapsuleOffsetEnd = vectors.Item2;
                return true;
            }
            return false;
        }

        public static bool TryParseVector3OffsetTriplet(string input, out (Vector3?, Vector3?, Vector3?) vectors)
        {
            if (!RegexUtil.TryParseVectorString(input, out var array))
            {
                vectors = (null, null, null);
                return false;
            }

            if (array.Length < 3)
            {
                vectors = (null, null, null);
                return false;
            }

            vectors.Item1 = new Vector3(array[0], array[1], array[2]);
            vectors.Item2 = array.Length >= 6 ? new Vector3(array[3], array[4], array[5]) : null;
            vectors.Item3 = array.Length >= 9 ? new Vector3(array[6], array[7], array[8]) : null;

            return true;
        }

        // Returns true if parsing a state dictionary
        public bool ParseDamageDelays(ref Utf8JsonReader reader)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                CapsuleDelay = reader.GetSingle();
                return false;
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                CapsuleStateDelay = new();
                while(reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject) return true;

                    if (reader.TokenType != JsonTokenType.PropertyName) return false;

                    string state = reader.GetString()!.ToLowerInvariant().Replace(" ", null);
                    reader.Read();
                    if (reader.TokenType != JsonTokenType.Number) return false;
                    float delay = reader.GetSingle();
                    switch (state)
                    {
                        case "light":
                            CapsuleStateDelay[eMeleeWeaponState.AttackMissLeft] = delay;
                            CapsuleStateDelay[eMeleeWeaponState.AttackMissRight] = delay;
                            break;
                        case "charged":
                            CapsuleStateDelay[eMeleeWeaponState.AttackChargeReleaseLeft] = delay;
                            CapsuleStateDelay[eMeleeWeaponState.AttackChargeReleaseRight] = delay;
                            break;
                        case "lightleft" or "attackmissleft":
                            CapsuleStateDelay[eMeleeWeaponState.AttackMissLeft] = delay;
                            break;
                        case "lightright" or "attackmissright":
                            CapsuleStateDelay[eMeleeWeaponState.AttackMissRight] = delay;
                            break;
                        case "chargedleft" or "attackchargereleaseleft":
                            CapsuleStateDelay[eMeleeWeaponState.AttackChargeReleaseLeft] = delay;
                            break;
                        case "chargedright" or "attackchargereleaseright":
                            CapsuleStateDelay[eMeleeWeaponState.AttackChargeReleaseRight] = delay;
                            break;
                    }
                }
            }

            return false;
        }
    }
}
