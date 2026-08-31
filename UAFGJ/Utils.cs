using AssetsTools.NET;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace UAFGJ
{
    partial class Program
    {
        private static bool StartsWithSpace(string str, string value)
        {
            return str.StartsWith(
                value + " ",
                StringComparison.Ordinal);
        }

        private static string UnescapeDumpString(string str)
        {
            StringBuilder sb = new StringBuilder(str.Length);
            bool escaping = false;

            foreach (char c in str)
            {
                if (!escaping && c == '\\')
                {
                    escaping = true;
                    continue;
                }

                if (escaping)
                {
                    switch (c)
                    {
                        case '\\':
                            sb.Append('\\');
                            break;

                        case 'r':
                            sb.Append('\r');
                            break;

                        case 'n':
                            sb.Append('\n');
                            break;

                        case 't':
                            sb.Append('\t');
                            break;

                        default:
                            sb.Append(c);
                            break;
                    }

                    escaping = false;
                }
                else
                {
                    sb.Append(c);
                }
            }

            if (escaping)
                sb.Append('\\');

            return sb.ToString();
        }

        private static int LeadingSpaces(string line)
        {
            int depth = 0;

            while (depth < line.Length && line[depth] == ' ')
                depth++;

            return depth;
        }

        private static string ParseDumpString(string valueStr)
        {
            int firstQuote = valueStr.IndexOf('"');
            int lastQuote = valueStr.LastIndexOf('"');

            if (firstQuote < 0 || lastQuote <= firstQuote)
            {
                throw new FormatException(
                    "String field does not contain a valid quoted value: " +
                    valueStr);
            }

            return UnescapeDumpString(
                valueStr.Substring(
                    firstQuote + 1,
                    lastQuote - firstQuote - 1));
        }

        private static int ParseInt32(string s)
        {
            return int.Parse(
                s,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture);
        }

        private static long ParseInt64(string s)
        {
            return long.Parse(
                s,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture);
        }

        private static uint ParseUInt32(string s)
        {
            return uint.Parse(
                s,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture);
        }

        private static ulong ParseUInt64(string s)
        {
            return ulong.Parse(
                s,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture);
        }

        // ------------------------------------------------------------
        // Numeric normalization.
        //
        // Supports both:
        //   0.5
        //   0,5
        //
        // We deliberately do NOT use AllowThousands when parsing
        // floating-point values. Otherwise "0,5" with InvariantCulture
        // can be interpreted as 5.
        // ------------------------------------------------------------

        private static string NormalizeNumericLiteral(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            value = value.Trim();

            if (value.Length == 0)
            {
                throw new FormatException(
                    "Numeric literal is empty.");
            }

            bool hasComma = value.Contains(',');
            bool hasDot = value.Contains('.');

            // 0,5 -> 0.5
            if (hasComma && !hasDot)
            {
                return value.Replace(',', '.');
            }

            // 0.5
            if (!hasComma && hasDot)
            {
                return value;
            }

            // 5
            if (!hasComma && !hasDot)
            {
                return value;
            }

            // Both separators occur.
            // Treat the LAST separator as the decimal separator.
            int commaIndex = value.LastIndexOf(',');
            int dotIndex = value.LastIndexOf('.');

            if (commaIndex > dotIndex)
            {
                // 1.234,5 -> 1234.5
                value = value.Replace(".", "");
                value = value.Replace(',', '.');
                return value;
            }
            else
            {
                // 1,234.5 -> 1234.5
                value = value.Replace(",", "");
                return value;
            }
        }

        private static float ParseSingle(string s)
        {
            string normalized = NormalizeNumericLiteral(s);

            return float.Parse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture);
        }

        private static double ParseDouble(string s)
        {
            string normalized = NormalizeNumericLiteral(s);

            return double.Parse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture);
        }

        private sealed class DumpScalar
        {
            public int LineNumber;
            public string Type = "";
            public string FieldName = "";
            public string Value = "";
        }

        private static List<AssetsTools.NET.AssetTypeValueField> CollectScalarFields(
            AssetsTools.NET.AssetTypeValueField field)
        {
            var result =
                new List<AssetsTools.NET.AssetTypeValueField>();

            CollectScalarFieldsRecursive(
                field,
                result);

            return result;
        }

        private static void CollectScalarFieldsRecursive(
            AssetsTools.NET.AssetTypeValueField field,
            List<AssetsTools.NET.AssetTypeValueField> result)
        {
            if (field == null || field.IsDummy)
                return;

            if (field.Children != null && field.Children.Count > 0)
            {
                foreach (var child in field.Children)
                {
                    CollectScalarFieldsRecursive(
                        child,
                        result);
                }

                return;
            }

            if (field.Value == null)
                return;

            if (field.Value.ValueType == AssetValueType.ByteArray ||
                field.Value.ValueType == AssetValueType.ManagedReferencesRegistry)
            {
                return;
            }

            result.Add(field);
        }

        private static List<DumpScalar> ReadDumpScalars(
            string inputFile)
        {
            var result = new List<DumpScalar>();

            using (var reader =
                new StreamReader(
                    inputFile,
                    Encoding.UTF8,
                    true))
            {
                int lineNumber = 0;

                while (true)
                {
                    string line = reader.ReadLine();

                    if (line == null)
                        break;

                    lineNumber++;

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    int depth = LeadingSpaces(line);

                    if (depth >= line.Length || line[depth] == '[')
                        continue;

                    int eq = line.IndexOf('=');

                    if (eq < 0)
                        continue;

                    if (depth + 2 >= eq)
                        continue;

                    string left =
                        line.Substring(
                            depth + 2,
                            eq - (depth + 2)).Trim();

                    string value =
                        line.Substring(eq + 1).Trim();

                    int split = left.IndexOf(' ');

                    if (split <= 0)
                        continue;

                    string type =
                        left.Substring(0, split).Trim();

                    string fieldName =
                        left.Substring(split + 1).Trim();

                    if (string.Equals(
                        fieldName,
                        "size",
                        StringComparison.Ordinal))
                    {
                        continue;
                    }

                    result.Add(
                        new DumpScalar
                        {
                            LineNumber = lineNumber,
                            Type = type,
                            FieldName = fieldName,
                            Value = value
                        });
                }
            }

            return result;
        }

        private static string RuntimeTypeToDumpType(
            AssetValueType t)
        {
            switch (t)
            {
                case AssetValueType.Bool:
                    return "bool";

                case AssetValueType.UInt8:
                    return "UInt8";

                case AssetValueType.Int8:
                    return "SInt8";

                case AssetValueType.UInt16:
                    return "UInt16";

                case AssetValueType.Int16:
                    return "SInt16";

                case AssetValueType.UInt32:
                    return "unsigned int";

                case AssetValueType.Int32:
                    return "int";

                case AssetValueType.UInt64:
                    return "UInt64";

                case AssetValueType.Int64:
                    return "SInt64";

                case AssetValueType.Float:
                    return "float";

                case AssetValueType.Double:
                    return "double";

                case AssetValueType.String:
                    return "string";

                default:
                    return t.ToString();
            }
        }

        private static void ApplyDumpValue(
            AssetsTools.NET.AssetTypeValueField field,
            DumpScalar dump)
        {
            if (field == null || field.Value == null)
            {
                throw new InvalidOperationException(
                    $"Target field '{dump.FieldName}' has no scalar value.");
            }

            switch (field.Value.ValueType)
            {
                case AssetValueType.Bool:
                    field.AsBool = bool.Parse(dump.Value);
                    break;

                case AssetValueType.UInt8:
                    field.AsUInt =
                        byte.Parse(
                            dump.Value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture);
                    break;

                case AssetValueType.Int8:
                    field.AsInt =
                        sbyte.Parse(
                            dump.Value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture);
                    break;

                case AssetValueType.UInt16:
                    field.AsUInt =
                        ushort.Parse(
                            dump.Value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture);
                    break;

                case AssetValueType.Int16:
                    field.AsInt =
                        short.Parse(
                            dump.Value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture);
                    break;

                case AssetValueType.UInt32:
                    field.AsUInt =
                        uint.Parse(
                            dump.Value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture);
                    break;

                case AssetValueType.Int32:
                    field.AsInt =
                        int.Parse(
                            dump.Value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture);
                    break;

                case AssetValueType.UInt64:
                    field.AsULong =
                        ulong.Parse(
                            dump.Value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture);
                    break;

                case AssetValueType.Int64:
                    field.AsLong =
                        long.Parse(
                            dump.Value,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture);
                    break;

                case AssetValueType.Float:
                    field.AsFloat =
                        ParseSingle(dump.Value);
                    break;

                case AssetValueType.Double:
                    field.AsDouble =
                        ParseDouble(dump.Value);
                    break;

                case AssetValueType.String:
                    field.AsString =
                        ParseDumpString(dump.Value);
                    break;

                default:
                    throw new NotSupportedException(
                        $"Unsupported runtime scalar type " +
                        $"'{field.Value.ValueType}' " +
                        $"for dump line {dump.LineNumber} " +
                        $"({dump.Type} {dump.FieldName}).");
            }
        }

        private static byte[] ApplyTextDumpToBaseField(
            string inputFile,
            AssetsTools.NET.AssetTypeValueField baseField)
        {
            DebugStr(
                "[TXT] Applying dump to existing AssetsTools.NET.AssetTypeValueField; " +
                "Unity serialization will be handled by AssetsTools.NET.");

            var dumpScalars =
                ReadDumpScalars(inputFile);

            var targetScalars =
                CollectScalarFields(baseField);

            DebugStr(
                $"[TXT] Dump scalar count: {dumpScalars.Count}; " +
                $"target scalar count: {targetScalars.Count}.");

            if (dumpScalars.Count != targetScalars.Count)
            {
                throw new InvalidDataException(
                    $"Dump/tree scalar count mismatch: " +
                    $"dump={dumpScalars.Count}, " +
                    $"target={targetScalars.Count}. " +
                    "Refusing to write.");
            }

            for (int i = 0; i < dumpScalars.Count; i++)
            {
                var dump = dumpScalars[i];
                var target = targetScalars[i];

                string targetName =
                    target.TemplateField?.Name ?? "<unnamed>";

                string targetType =
                    target.Value == null
                        ? "<null>"
                        : RuntimeTypeToDumpType(
                            target.Value.ValueType);

                if (!string.Equals(
                    dump.FieldName,
                    targetName,
                    StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Field name/order mismatch at scalar #{i + 1}: " +
                        $"dump='{dump.FieldName}', " +
                        $"target='{targetName}'.");
                }

                if (!string.Equals(
                    dump.Type,
                    targetType,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"Field type mismatch at scalar #{i + 1} '{targetName}': " +
                        $"dump='{dump.Type}', " +
                        $"target='{targetType}'.");
                }

                ApplyDumpValue(target, dump);

                if (i < 8 || i == dumpScalars.Count - 1)
                {
                    DebugStr(
                        $"[TXT] Applied #{i + 1}: " +
                        $"{targetName} ({target.Value.ValueType})");
                }
            }

            byte[] data =
                baseField.WriteToByteArray();

            DebugStr(
                $"[TXT] BaseField reserialized: " +
                $"{data.Length} bytes " +
                $"SHA256={Sha256Hex(data)}");

            return data;
        }

        private static bool AreFloatsEqual(
            float expected,
            float actual)
        {
            if (float.IsNaN(expected) &&
                float.IsNaN(actual))
            {
                return true;
            }

            if (float.IsPositiveInfinity(expected) &&
                float.IsPositiveInfinity(actual))
            {
                return true;
            }

            if (float.IsNegativeInfinity(expected) &&
                float.IsNegativeInfinity(actual))
            {
                return true;
            }

            if (float.IsNaN(expected) ||
                float.IsNaN(actual))
            {
                return false;
            }

            float tolerance =
                Math.Max(
                    1e-6f,
                    Math.Abs(expected) * 1e-6f);

            return Math.Abs(expected - actual) <= tolerance;
        }

        private static bool AreDoublesEqual(
            double expected,
            double actual)
        {
            if (double.IsNaN(expected) &&
                double.IsNaN(actual))
            {
                return true;
            }

            if (double.IsPositiveInfinity(expected) &&
                double.IsPositiveInfinity(actual))
            {
                return true;
            }

            if (double.IsNegativeInfinity(expected) &&
                double.IsNegativeInfinity(actual))
            {
                return true;
            }

            if (double.IsNaN(expected) ||
                double.IsNaN(actual))
            {
                return false;
            }

            double tolerance =
                Math.Max(
                    1e-12,
                    Math.Abs(expected) * 1e-12);

            return Math.Abs(expected - actual) <= tolerance;
        }

        private static void ValidateDumpAgainstBaseField(
            string inputFile,
            AssetsTools.NET.AssetTypeValueField baseField)
        {
            var dumpScalars =
                ReadDumpScalars(inputFile);

            var targetScalars =
                CollectScalarFields(baseField);

            if (dumpScalars.Count != targetScalars.Count)
            {
                throw new InvalidDataException(
                    $"FINAL CHECK: dump/tree scalar count mismatch: " +
                    $"dump={dumpScalars.Count}, " +
                    $"target={targetScalars.Count}");
            }

            for (int i = 0; i < dumpScalars.Count; i++)
            {
                var dump = dumpScalars[i];
                var target = targetScalars[i];

                string targetName =
                    target.TemplateField?.Name ?? "<unnamed>";

                string targetType =
                    RuntimeTypeToDumpType(
                        target.Value.ValueType);

                if (!string.Equals(
                    dump.FieldName,
                    targetName,
                    StringComparison.Ordinal) ||
                    !string.Equals(
                        dump.Type,
                        targetType,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"FINAL CHECK: field #{i + 1} mismatch " +
                        $"dump='{dump.Type} {dump.FieldName}' " +
                        $"target='{targetType} {targetName}'.");
                }

                // ----------------------------------------------------
                // Strings
                // ----------------------------------------------------

                if (target.Value.ValueType ==
                    AssetValueType.String)
                {
                    string expectedString =
                        ParseDumpString(
                            dump.Value);

                    string actualString =
                        target.AsString ?? "";

                    if (!string.Equals(
                        expectedString,
                        actualString,
                        StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            $"FINAL CHECK: string mismatch at '{targetName}': " +
                            $"dumpLength={expectedString?.Length ?? 0}, " +
                            $"actualLength={actualString?.Length ?? 0}.");
                    }

                    continue;
                }

                // ----------------------------------------------------
                // Float
                // ----------------------------------------------------

                if (target.Value.ValueType ==
                    AssetValueType.Float)
                {
                    float expectedFloat =
                        ParseSingle(
                            dump.Value);

                    float actualFloat =
                        target.AsFloat;

                    if (!AreFloatsEqual(
                        expectedFloat,
                        actualFloat))
                    {
                        string expectedText =
                            expectedFloat.ToString(
                                "R",
                                CultureInfo.InvariantCulture);

                        string actualText =
                            actualFloat.ToString(
                                "R",
                                CultureInfo.InvariantCulture);

                        throw new InvalidDataException(
                            $"FINAL CHECK: float mismatch at '{targetName}': " +
                            $"dump='{dump.Value}' " +
                            $"parsedExpected='{expectedText}' " +
                            $"actual='{actualText}'.");
                    }

                    continue;
                }

                // ----------------------------------------------------
                // Double
                // ----------------------------------------------------

                if (target.Value.ValueType ==
                    AssetValueType.Double)
                {
                    double expectedDouble =
                        ParseDouble(
                            dump.Value);

                    double actualDouble =
                        target.AsDouble;

                    if (!AreDoublesEqual(
                        expectedDouble,
                        actualDouble))
                    {
                        string expectedText =
                            expectedDouble.ToString(
                                "R",
                                CultureInfo.InvariantCulture);

                        string actualText =
                            actualDouble.ToString(
                                "R",
                                CultureInfo.InvariantCulture);

                        throw new InvalidDataException(
                            $"FINAL CHECK: double mismatch at '{targetName}': " +
                            $"dump='{dump.Value}' " +
                            $"parsedExpected='{expectedText}' " +
                            $"actual='{actualText}'.");
                    }

                    continue;
                }

                // ----------------------------------------------------
                // Other scalar types
                // ----------------------------------------------------

                string actualValue =
                    ReadFieldAsDumpValue(
                        target);

                if (
                    target.Value.ValueType == AssetValueType.UInt8 ||
                    target.Value.ValueType == AssetValueType.Int8 ||
                    target.Value.ValueType == AssetValueType.UInt16 ||
                    target.Value.ValueType == AssetValueType.Int16 ||
                    target.Value.ValueType == AssetValueType.UInt32 ||
                    target.Value.ValueType == AssetValueType.Int32 ||
                    target.Value.ValueType == AssetValueType.UInt64 ||
                    target.Value.ValueType == AssetValueType.Int64)
                {
                    string normalizedDump =
                        NormalizeNumericLiteral(
                            dump.Value);

                    string normalizedActual =
                        NormalizeNumericLiteral(
                            actualValue);

                    if (!string.Equals(
                        normalizedDump,
                        normalizedActual,
                        StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            $"FINAL CHECK: numeric mismatch at '{targetName}': " +
                            $"dump='{dump.Value}' " +
                            $"actual='{actualValue}'.");
                    }
                }
                else if (!string.Equals(
                    actualValue,
                    dump.Value,
                    StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"FINAL CHECK: value mismatch at '{targetName}': " +
                        $"dump='{dump.Value}' " +
                        $"actual='{actualValue}'.");
                }
            }
        }

        private static string ReadFieldAsDumpValue(
            AssetsTools.NET.AssetTypeValueField field)
        {
            switch (field.Value.ValueType)
            {
                case AssetValueType.Bool:
                    return field.AsBool
                        ? "true"
                        : "false";

                case AssetValueType.UInt8:
                    return field.AsUInt.ToString(
                        CultureInfo.InvariantCulture);

                case AssetValueType.Int8:
                    return field.AsInt.ToString(
                        CultureInfo.InvariantCulture);

                case AssetValueType.UInt16:
                    return field.AsUInt.ToString(
                        CultureInfo.InvariantCulture);

                case AssetValueType.Int16:
                    return field.AsInt.ToString(
                        CultureInfo.InvariantCulture);

                case AssetValueType.UInt32:
                    return field.AsUInt.ToString(
                        CultureInfo.InvariantCulture);

                case AssetValueType.Int32:
                    return field.AsInt.ToString(
                        CultureInfo.InvariantCulture);

                case AssetValueType.UInt64:
                    return field.AsULong.ToString(
                        CultureInfo.InvariantCulture);

                case AssetValueType.Int64:
                    return field.AsLong.ToString(
                        CultureInfo.InvariantCulture);

                case AssetValueType.Float:
                    return field.AsFloat.ToString(
                        "R",
                        CultureInfo.InvariantCulture);

                case AssetValueType.Double:
                    return field.AsDouble.ToString(
                        "R",
                        CultureInfo.InvariantCulture);

                case AssetValueType.String:
                    return field.AsString;

                default:
                    throw new NotSupportedException(
                        "Unsupported field type in final validation: " +
                        field.Value.ValueType);
            }
        }
    }
}