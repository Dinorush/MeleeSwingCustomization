﻿using MSC.Utils;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnityEngine;

namespace MSC.JSON
{
    public sealed class Vector3Converter : JsonConverter<Vector3?>
    {
        public override bool HandleNull => false;

        public override Vector3? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var vector = new Vector3();

            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.EndObject)
                            return vector;

                        if (reader.TokenType != JsonTokenType.PropertyName)
                            throw new JsonException("Expected PropertyName token");

                        var propName = reader.GetString();
                        reader.Read();

                        switch (propName!.ToLowerInvariant())
                        {
                            case "x":
                                vector.x = reader.GetSingle();
                                break;

                            case "y":
                                vector.y = reader.GetSingle();
                                break;

                            case "z":
                                vector.z = reader.GetSingle();
                                break;
                        }
                    }
                    throw new JsonException("Expected EndObject token");

                case JsonTokenType.String:
                    var strValue = reader.GetString()!.Trim();
                    if (TryParseVector3(strValue, out var vectorOrNull))
                    {
                        return vectorOrNull;
                    }
                    throw new JsonException($"Vector3 format is not right: {strValue}");

                default:
                    throw new JsonException($"Vector3Json type: {reader.TokenType} is not implemented!");
            }
        }

        private static bool TryParseVector3(string input, out Vector3? vector)
        {
            if (!RegexUtil.TryParseVectorString(input, out var array))
            {
                vector = Vector3.zero;
                return false;
            }

            if (array.Length < 3)
            {
                vector = Vector3.zero;
                return false;
            }

            vector = new Vector3(array[0], array[1], array[2]);
            return true;
        }

        public override void Write(Utf8JsonWriter writer, Vector3? value, JsonSerializerOptions options)
        {
            if (value == null)
                writer.WriteNullValue();
            else
                writer.WriteStringValue(string.Format("({0} {1} {2})", value.Value.x, value.Value.y, value.Value.z));
        }
    }
}