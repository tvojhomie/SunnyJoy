using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AlienProject.WebWrapper
{
    public static class JsonObjectWriter
    {
        public static string Serialize(object value)
        {
            var builder = new StringBuilder(256);
            WriteValue(builder, value);
            return builder.ToString();
        }

        private static void WriteValue(StringBuilder builder, object value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            if (value is string text)
            {
                WriteString(builder, text);
                return;
            }

            if (value is bool boolean)
            {
                builder.Append(boolean ? "true" : "false");
                return;
            }

            if (value is IDictionary dictionary)
            {
                WriteDictionary(builder, dictionary);
                return;
            }

            if (value is IEnumerable enumerable && !(value is string))
            {
                WriteArray(builder, enumerable);
                return;
            }

            if (IsNumber(value))
            {
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            WriteString(builder, Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        private static void WriteDictionary(StringBuilder builder, IDictionary dictionary)
        {
            builder.Append('{');
            var first = true;
            foreach (DictionaryEntry item in dictionary)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                WriteString(builder, Convert.ToString(item.Key, CultureInfo.InvariantCulture));
                builder.Append(':');
                WriteValue(builder, item.Value);
            }

            builder.Append('}');
        }

        private static void WriteArray(StringBuilder builder, IEnumerable values)
        {
            builder.Append('[');
            var first = true;
            foreach (var value in values)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                WriteValue(builder, value);
            }

            builder.Append(']');
        }

        private static void WriteString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (var character in value ?? string.Empty)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 32)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4"));
                        }
                        else
                        {
                            builder.Append(character);
                        }
                        break;
                }
            }

            builder.Append('"');
        }

        private static bool IsNumber(object value)
        {
            return value is byte || value is sbyte || value is short || value is ushort ||
                   value is int || value is uint || value is long || value is ulong ||
                   value is float || value is double || value is decimal;
        }
    }
}
