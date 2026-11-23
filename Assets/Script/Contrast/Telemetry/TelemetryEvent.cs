using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Contrast.Telemetry
{
    /// <summary>
    /// Runtime accessor backed by <see cref="TelemetrySettings"/> ScriptableObject
    /// plus optional runtime overrides.
    /// </summary>
    public static class TelemetryConfig
    {
        private static TelemetrySettings activeSettings;

        public static TelemetrySettings Settings
        {
            get
            {
                if (activeSettings == null)
                    activeSettings = TelemetrySettings.LoadOrDefault();
                return activeSettings;
            }
        }

        public static string EndpointUrl => Settings.endpointUrl;
        public static string GameId => Settings.gameId;
        public static string Collection => Settings.collection;
        public static bool Enabled => Settings.enabled;
        public static bool LogToConsole => Settings.logToConsole;
        public static bool OmitZeroOptionalFields => Settings.omitZeroOptionalFields;
        public static bool SuppressNetworkInTests = false;

        /// <summary>Optional runtime action mute set (merged with TelemetrySettings.disabledActions).</summary>
        public static readonly HashSet<string> DisabledActions = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Optional runtime field strip set (merged with TelemetrySettings.disabledFields).</summary>
        public static readonly HashSet<string> DisabledFields = new HashSet<string>(StringComparer.Ordinal);

        public static void ApplySettings(TelemetrySettings settings)
        {
            if (settings != null)
            {
                settings.RebuildCaches();
                activeSettings = settings;
            }
        }

        public static bool IsActionEnabled(string action)
        {
            return Settings.IsActionAllowed(action) && !DisabledActions.Contains(action);
        }

        public static bool IsFieldEnabled(string key)
        {
            return Settings.IsFieldAllowed(key) && !DisabledFields.Contains(key);
        }
    }

    /// <summary>
    /// Mutable, fluent JSON payload builder.
    /// Supports Set (add/edit), Remove (delete), and Merge before serialization.
    /// </summary>
    public sealed class TelemetryEvent
    {
        private enum ValueKind { String, Int, Long, Float, Bool }

        private struct FieldEntry
        {
            public string Key;
            public ValueKind Kind;
            public string StringVal;
            public long IntVal;
            public float FloatVal;
            public bool BoolVal;
        }

        private readonly List<FieldEntry> entries = new List<FieldEntry>(24);
        private readonly Dictionary<string, int> keyIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly StringBuilder sharedSb = new StringBuilder(384);

        public string Action { get; private set; }
        public string EventName => Action;

        public TelemetryEvent() : this(string.Empty)
        {
        }

        public TelemetryEvent(string action)
        {
            Action = action ?? string.Empty;
        }

        public TelemetryEvent Clear(string action)
        {
            Action = action ?? string.Empty;
            entries.Clear();
            keyIndex.Clear();
            return this;
        }

        public TelemetryEvent Set(string key, string value)
        {
            Upsert(new FieldEntry { Key = key, Kind = ValueKind.String, StringVal = value });
            return this;
        }

        public TelemetryEvent Set(string key, int value)
        {
            Upsert(new FieldEntry { Key = key, Kind = ValueKind.Int, IntVal = value });
            return this;
        }

        public TelemetryEvent Set(string key, long value)
        {
            Upsert(new FieldEntry { Key = key, Kind = ValueKind.Long, IntVal = value });
            return this;
        }

        public TelemetryEvent Set(string key, float value)
        {
            Upsert(new FieldEntry { Key = key, Kind = ValueKind.Float, FloatVal = value });
            return this;
        }

        public TelemetryEvent Set(string key, bool value)
        {
            Upsert(new FieldEntry { Key = key, Kind = ValueKind.Bool, BoolVal = value });
            return this;
        }

        public TelemetryEvent Set(string key, int? value)
        {
            return value.HasValue ? Set(key, value.Value) : SetNull(key);
        }

        public TelemetryEvent Set(string key, long? value)
        {
            return value.HasValue ? Set(key, value.Value) : SetNull(key);
        }

        public TelemetryEvent SetNull(string key)
        {
            Upsert(new FieldEntry { Key = key, Kind = ValueKind.String, StringVal = null });
            return this;
        }

        public TelemetryEvent SetOptional(string key, string value)
        {
            if (TelemetryConfig.OmitZeroOptionalFields && string.IsNullOrEmpty(value))
                return this;
            return Set(key, value);
        }

        public TelemetryEvent SetOptional(string key, int value)
        {
            if (TelemetryConfig.OmitZeroOptionalFields && value == 0)
                return this;
            return Set(key, value);
        }

        public TelemetryEvent SetOptional(string key, float value)
        {
            if (TelemetryConfig.OmitZeroOptionalFields &&
                (float.IsNaN(value) || float.IsInfinity(value) || (value > -0.05f && value < 0.05f)))
                return this;
            return Set(key, value);
        }

        public TelemetryEvent SetOptional(string key, bool value)
        {
            if (TelemetryConfig.OmitZeroOptionalFields && !value)
                return this;
            return Set(key, value);
        }

        public TelemetryEvent Remove(string key)
        {
            if (string.IsNullOrEmpty(key) || !keyIndex.TryGetValue(key, out int idx))
                return this;

            entries.RemoveAt(idx);
            keyIndex.Remove(key);
            for (int i = idx; i < entries.Count; i++)
                keyIndex[entries[i].Key] = i;

            return this;
        }

        public TelemetryEvent Merge(Action<TelemetryEvent> mutator)
        {
            if (mutator != null)
                mutator(this);
            return this;
        }

        public string ToJson()
        {
            sharedSb.Length = 0;
            StringBuilder sb = sharedSb;
            sb.Append('{');
            bool wroteAny = false;

            for (int i = 0; i < entries.Count; i++)
            {
                FieldEntry e = entries[i];
                if (!TelemetryConfig.IsFieldEnabled(e.Key))
                    continue;

                if (wroteAny)
                    sb.Append(',');
                wroteAny = true;

                sb.Append('"').Append(e.Key).Append("\":");
                WriteValue(sb, e);
            }

            sb.Append('}');
            return sb.ToString();
        }

        private void Upsert(FieldEntry entry)
        {
            if (string.IsNullOrEmpty(entry.Key))
                return;

            if (keyIndex.TryGetValue(entry.Key, out int idx))
            {
                entries[idx] = entry;
            }
            else
            {
                keyIndex[entry.Key] = entries.Count;
                entries.Add(entry);
            }
        }

        private static void WriteValue(StringBuilder sb, FieldEntry e)
        {
            switch (e.Kind)
            {
                case ValueKind.String:
                    WriteEscapedString(sb, e.StringVal);
                    break;
                case ValueKind.Int:
                case ValueKind.Long:
                    sb.Append(e.IntVal.ToString(CultureInfo.InvariantCulture));
                    break;
                case ValueKind.Float:
                    float safe = (float.IsNaN(e.FloatVal) || float.IsInfinity(e.FloatVal)) ? 0f : e.FloatVal;
                    if (safe > -0.0005f && safe < 0.0005f)
                        safe = 0f;
                    sb.Append(safe.ToString("0.###", CultureInfo.InvariantCulture));
                    break;
                case ValueKind.Bool:
                    sb.Append(e.BoolVal ? "true" : "false");
                    break;
            }
        }

        private static void WriteEscapedString(StringBuilder sb, string value)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            sb.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < ' ')
                            sb.AppendFormat(CultureInfo.InvariantCulture, "\\u{0:X4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
