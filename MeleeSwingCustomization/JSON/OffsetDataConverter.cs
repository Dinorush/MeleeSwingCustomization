﻿using MSC.CustomMeleeData;
using MSC.Utils;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnityEngine;

namespace MSC.JSON
{
    public sealed class OffsetDataConverter : JsonConverter<OffsetData>
    {
        public const string NullStr = "Unchanged";
        public const string NullStrLower = "unchanged";

        public override bool HandleNull => false;

        public override OffsetData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            OffsetData offsetData = new();

            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return offsetData;

                case JsonTokenType.StartObject:
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonTokenType.EndObject)
                            return offsetData;

                        if (reader.TokenType != JsonTokenType.PropertyName)
                            throw new JsonException("Expected PropertyName token");

                        var propName = reader.GetString();
                        reader.Read();

                        offsetData.DeserializeProperty(propName!.ToLowerInvariant().Replace(" ", null), ref reader);
                    }
                    throw new JsonException("Expected EndObject token");

                case JsonTokenType.String:
                    var strValue = reader.GetString()!.Trim();
                    if (offsetData.ParseVectorPair(strValue))
                        return offsetData;

                    throw new JsonException($"Bad vector formats detected for OffsetData: {strValue}");

                default:
                    throw new JsonException($"OffsetData Json type: {reader.TokenType} is not implemented!");
            }
        }

        public override void Write(Utf8JsonWriter writer, OffsetData value, JsonSerializerOptions options)
        {
            value.Serialize(writer);
        }
    }
}