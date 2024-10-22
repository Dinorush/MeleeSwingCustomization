﻿using GameData;
using MSC.JSON;
using MSC.Utils;
using System.Text;
using System.Text.Json;
using UnityEngine;

namespace MSC.CustomMeleeData
{
    public sealed class OffsetData
    {
        private Vector3? _offset = null;
        public Vector3 Offset { get => _offset!.Value; set => _offset = value; }

        public Vector3? _capsuleOffset = null;
        private bool _capsuleUseCamFwd = false;
        private float _capsuleCamFwdAdd = 0f;
        private float _capsuleSize = -1f;
        private bool _capsuleUseCenterMod = false;

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
            _capsuleOffset = offsets.Item2;
        }

        public bool HasOffset => _offset != null;
        public bool HasCapsule => _capsuleUseCamFwd || _capsuleOffset != null;


        public float CapsuleSize(MeleeArchetypeDataBlock data, float dotScale = 0)
        {
            float size = _capsuleSize >= 0 ? _capsuleSize : data.AttackSphereRadius;
            return _capsuleUseCenterMod && dotScale > 0.5f ? size * dotScale : size;
        }

        public (Vector3 start, Vector3 end) CapsuleOffsets(Transform transform, MeleeArchetypeDataBlock data)
        {
            Vector3 start;
            Vector3 end;
            Vector3 localPos = transform.localPosition;
            if (_capsuleUseCamFwd)
            {
                float camFwdLength = _capsuleCamFwdAdd + data.CameraDamageRayLength;
                
                if (_capsuleOffset == null)
                {
                    transform.localPosition = Vector3.zero;
                    start = transform.position;
                    transform.localPosition = localPos.normalized * camFwdLength;
                    end = transform.position;
                }
                else
                {
                    transform.localPosition = _capsuleOffset.Value;
                    start = transform.position;
                    transform.localPosition = (localPos - _capsuleOffset.Value).normalized * camFwdLength;
                    end = transform.position;
                }
            }
            else
            {
                end = transform.position;
                transform.localPosition = _capsuleOffset!.Value;
                start = transform.position;
            }

            transform.localPosition = localPos;
            return (start, end);
        }

        public void Serialize(Utf8JsonWriter writer)
        {
            if (_offset == null)
            {
                writer.WriteNullValue();
                return;
            }

            if (_capsuleOffset == null)
            {
                MSCJson.Serialize(_offset, writer);
                return;
            }

            writer.WriteStartObject();
            writer.WritePropertyName(nameof(Offset));
            if (_offset != null)
            {
                StringBuilder builder = new(MSCJson.Serialize(_offset)[1..^1]);
                if (_capsuleOffset != null)
                    builder.Append(" " + MSCJson.Serialize(_capsuleOffset)[1..^1]);
                writer.WriteStringValue(builder.ToString());
                DinoLogger.Log(builder.ToString());
            }
            else
                writer.WriteNullValue();

            writer.WriteBoolean(PrettyName(nameof(_capsuleUseCamFwd)), _capsuleUseCamFwd);
            writer.WriteNumber(PrettyName(nameof(_capsuleCamFwdAdd)), _capsuleCamFwdAdd);
            writer.WriteNumber(PrettyName(nameof(_capsuleSize)), _capsuleSize);
            writer.WriteBoolean(PrettyName(nameof(_capsuleUseCenterMod)), _capsuleUseCenterMod);
            writer.WriteEndObject();
        }

        private static string PrettyName(string name) => char.ToUpper(name[1]) + name[2..];

        public void DeserializeProperty(string propertyName, ref Utf8JsonReader reader)
        {
            switch (propertyName)
            {
                case "offsets":
                case "offset":
                    ParseVectorPair(reader.GetString()!.Trim());
                    break;
                case "capsuleoffset":
                    _capsuleOffset = MSCJson.Deserialize<Vector3?>(ref reader);
                    break;
                case "capsuleusecamfwd":
                    _capsuleUseCamFwd = reader.GetBoolean();
                    break;
                case "capsulecamfwdadd":
                    _capsuleCamFwdAdd = reader.GetSingle();
                    break;
                case "capsulesize":
                    _capsuleSize = reader.GetSingle();
                    break;
                case "capsuleusecentermod":
                    _capsuleUseCamFwd = reader.GetBoolean();
                    break;
            }
        }

        public bool ParseVectorPair(string text)
        {
            if (TryParseVector3Pair(text, out var vectors))
            {
                _offset = vectors.Item1;
                _capsuleOffset = vectors.Item2;
                return true;
            }
            return false;
        }

        public static bool TryParseVector3Pair(string input, out (Vector3?, Vector3?) vectors)
        {
            if (!RegexUtil.TryParseVectorString(input, out var array))
            {
                vectors = (null, null);
                return false;
            }

            if (array.Length < 3)
            {
                vectors = (null, null);
                return false;
            }

            vectors.Item1 = new Vector3(array[0], array[1], array[2]);
            if (array.Length >= 6)
                vectors.Item2 = new Vector3(array[3], array[4], array[5]);
            else
                vectors.Item2 = null;

            return true;
        }
    }
}