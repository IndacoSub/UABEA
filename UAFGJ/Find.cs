using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace UAFGJ
{
    partial class Program
    {
        private static bool TryParsePathId(
            string specificPathId,
            out long pathId)
        {
            if (string.IsNullOrWhiteSpace(specificPathId))
            {
                pathId = 0;
                return false;
            }

            return long.TryParse(
                specificPathId,
                out pathId);
        }

        private static string GetAssetName(
            AssetsTools.NET.AssetTypeValueField field)
        {
            try
            {
                if (field == null ||
                    field.IsDummy)
                {
                    return "";
                }

                var nameField = field["m_Name"];

                if (nameField == null ||
                    nameField.IsDummy)
                {
                    return "";
                }

                return nameField.AsString ?? "";
            }
            catch
            {
                return "";
            }
        }

        private static bool ImportTextAssetRaw(
            string inputFile,
            AssetsTools.NET.AssetTypeValueField baseField,
            AssetFileInfo afie,
            out byte[] originalSerializedData,
            out byte[] replacementData) {

            originalSerializedData =
                Array.Empty<byte>();

            replacementData =
                Array.Empty<byte>();

            if (baseField == null ||
                baseField.IsDummy)
            {
                DebugStr(
                    "[TXT] TextAsset BaseField is null/dummy.");

                return false;
            }

            // --------------------------------------------------------
            // Preserve the original serialized asset.
            // --------------------------------------------------------

            try
            {
                originalSerializedData =
                    baseField.WriteToByteArray();
            }
            catch (Exception ex)
            {
                DebugStr(
                    $"[TXT] Could not serialize original TextAsset " +
                    $"PID={afie.PathId}: {ex}");

                return false;
            }

            DebugStr(
                $"[TXT] Original TextAsset serialized size=" +
                $"{originalSerializedData.Length} " +
                $"SHA256={Sha256Hex(originalSerializedData)}");

            // --------------------------------------------------------
            // TextAsset structure:
            //
            //   m_Name
            //   m_Script
            // --------------------------------------------------------

            AssetsTools.NET.AssetTypeValueField nameField;
            AssetsTools.NET.AssetTypeValueField scriptField;

            try
            {
                nameField = baseField["m_Name"];
                scriptField = baseField["m_Script"];
            }
            catch (Exception ex)
            {
                DebugStr(
                    $"[TXT] Could not access TextAsset fields " +
                    $"for PID={afie.PathId}: {ex}");

                return false;
            }

            if (nameField == null ||
                nameField.IsDummy ||
                scriptField == null ||
                scriptField.IsDummy)
            {
                DebugStr(
                    $"[TXT] TextAsset PID={afie.PathId} " +
                    "does not have valid m_Name/m_Script fields.");

                return false;
            }

            // --------------------------------------------------------
            // Read input.
            // --------------------------------------------------------

            string text;

            try
            {
                text = File.ReadAllText(
                    inputFile,
                    new UTF8Encoding(
                        encoderShouldEmitUTF8Identifier: false,
                        throwOnInvalidBytes: false));
            }
            catch (Exception ex)
            {
                DebugStr(
                    $"[TXT] Could not read TextAsset source " +
                    $"'{inputFile}': {ex}");

                return false;
            }

            // Remove BOM.
            if (text.Length > 0 &&
                text[0] == '\uFEFF')
            {
                text = text.Substring(1);
            }

            // --------------------------------------------------------
            // Detect UABEA Export Dump.
            //
            // Supported:
            //
            //   0 TextAsset Base
            //    1 string m_Name = "English"
            //    1 string m_Script = "...."
            //
            // Otherwise the file is treated as raw TextAsset text.
            // --------------------------------------------------------

            bool looksLikeExportDump =
                text.StartsWith(
                    "0 TextAsset Base",
                    StringComparison.Ordinal);

            if (looksLikeExportDump)
            {
                DebugStr(
                    "[TXT] Detected UABEA TextAsset Export Dump.");

                string[] lines =
                    text.Replace("\r\n", "\n")
                        .Replace("\r", "\n")
                        .Split('\n');

                string dumpedName = null;
                string dumpedScript = null;

                foreach (string rawLine in lines)
                {
                    string line = rawLine;

                    // ----------------------------------------------------
                    // m_Name
                    // ----------------------------------------------------

                    const string namePrefix =
                        " 1 string m_Name = ";

                    if (line.StartsWith(
                        namePrefix,
                        StringComparison.Ordinal))
                    {
                        string value =
                            line.Substring(
                                namePrefix.Length).Trim();

                        dumpedName =
                            ParseDumpString(value);

                        continue;
                    }

                    // ----------------------------------------------------
                    // m_Script
                    // ----------------------------------------------------

                    const string scriptPrefix =
                        " 1 string m_Script = ";

                    if (line.StartsWith(
                        scriptPrefix,
                        StringComparison.Ordinal))
                    {
                        string value =
                            line.Substring(
                                scriptPrefix.Length).Trim();

                        dumpedScript =
                            ParseDumpString(value);

                        continue;
                    }
                }

                if (dumpedScript == null)
                {
                    DisplayStr(
                        "[TXT] File looks like a UABEA TextAsset Export Dump " +
                        "but m_Script could not be parsed.");

                    return false;
                }

                if (dumpedName != null)
                {
                    try
                    {
                        nameField.AsString = dumpedName;

                        DebugStr(
                            $"[TXT] Imported m_Name from dump: '{dumpedName}'");
                    }
                    catch (Exception ex)
                    {
                        DebugStr(
                            $"[TXT] Failed assigning m_Name from dump: {ex}");

                        return false;
                    }
                }

                text = dumpedScript;

                DebugStr(
                    $"[TXT] Extracted m_Script from UABEA dump. " +
                    $"Length={text.Length}");
            }
            else
            {
                DebugStr(
                    "[TXT] Input is raw TextAsset text; no dump wrapper detected.");
            }

            // --------------------------------------------------------
            // Assign m_Script.
            // --------------------------------------------------------

            DebugStr(
                $"[TXT] Original TextAsset m_Script length=" +
                $"{scriptField.AsString?.Length ?? 0}");

            DebugStr(
                $"[TXT] New TextAsset m_Script length=" +
                $"{text.Length}");

            try
            {
                scriptField.AsString = text;
            }
            catch (Exception ex)
            {
                DebugStr(
                    $"[TXT] Failed assigning TextAsset.m_Script " +
                    $"for PID={afie.PathId}: {ex}");

                return false;
            }

            // --------------------------------------------------------
            // Serialize modified asset.
            // --------------------------------------------------------

            try
            {
                replacementData =
                    baseField.WriteToByteArray();
            }
            catch (Exception ex)
            {
                DebugStr(
                    $"[TXT] Could not serialize modified TextAsset " +
                    $"PID={afie.PathId}: {ex}");

                return false;
            }

            if (replacementData.Length == 0)
            {
                DebugStr(
                    $"[TXT] Modified TextAsset PID={afie.PathId} " +
                    "serialized to zero bytes.");

                return false;
            }

            DebugStr(
                $"[TXT] Modified TextAsset serialized size=" +
                $"{replacementData.Length} " +
                $"SHA256={Sha256Hex(replacementData)}");

            return true;
        }

        private static bool FindTXTFile(
            string inputFile,
            ref AssetsFileInstance assetInst,
            ref AssetFileInfo afie,
            ref AssetsTools.NET.AssetTypeValueField atvf,
            ref AssetsManager am,
            string asset,
            string assetfile_name,
            string specific_pathid,
            out byte[] rawReplacementData,
            out byte[] originalSerializedData)
        {
            rawReplacementData =
                Array.Empty<byte>();

            originalSerializedData =
                Array.Empty<byte>();

            if (assetInst == null)
            {
                DebugStr(
                    "[TXT] AssetsFileInstance is null.");

                return false;
            }

            if (am == null)
            {
                DebugStr(
                    "[TXT] AssetsManager is null.");

                return false;
            }

            if (!File.Exists(inputFile))
            {
                DebugStr(
                    $"[TXT] Replacement file does not exist: " +
                    $"{inputFile}");

                return false;
            }

            string targetName =
                Path.GetFileNameWithoutExtension(
                    inputFile).Trim();

            long wantedPathId;
            bool hasWantedPathId =
                TryParsePathId(
                    specific_pathid,
                    out wantedPathId);

            // ========================================================
            // PATH ID SEARCH
            //
            // If a PathID was supplied, search ALL assets.
            // This is the important fix for TextAsset PID 122.
            // ========================================================

            if (hasWantedPathId)
            {
                DebugStr(
                    $"[TXT] Searching assets in '{assetfile_name}' " +
                    $"for exact PID {wantedPathId}");

                AssetFileInfo exactMatch =
                    assetInst.file.AssetInfos.FirstOrDefault(
                        a => a.PathId == wantedPathId);

                if (exactMatch == null)
                {
                    DisplayStr(
                        $"[TXT] Could not find any asset " +
                        $"with path ID {wantedPathId}.");

                    return false;
                }

                afie = exactMatch;

                AssetsTools.NET.AssetTypeValueField candidate;

                try
                {
                    candidate =
                        am.GetBaseField(
                            assetInst,
                            afie);
                }
                catch (Exception ex)
                {
                    DisplayStr(
                        $"[TXT] Failed reading asset PID " +
                        $"{wantedPathId}: " +
                        $"{ex.GetType().Name}: {ex.Message}");

                    DebugStr(ex.ToString());

                    return false;
                }

                if (candidate == null ||
                    candidate.IsDummy)
                {
                    DisplayStr(
                        $"[TXT] Asset PID {wantedPathId} " +
                        "returned a null/dummy BaseField.");

                    return false;
                }

                string name =
                    GetAssetName(candidate);

                DebugStr(
                    $"Found TXT target: " +
                    $"PID={afie.PathId}, " +
                    $"Name='{name}', " +
                    $"TypeID={afie.TypeId}");

                // ----------------------------------------------------
                // Supported text asset types.
                //
                // 49  = TextAsset
                // 114 = MonoBehaviour
                // ----------------------------------------------------

                if (afie.TypeId == 49)
                {
                    DebugStr(
                        "[TXT] Target is TextAsset (TypeID=49); " +
                        "MonoScript validation is not applicable.");

                    atvf = candidate;

                    return ImportTextAssetRaw(
                        inputFile,
                        atvf,
                        afie,
                        out originalSerializedData,
                        out rawReplacementData);
                }

                if (afie.TypeId == 114)
                {
                    ushort monoId =
                        assetInst.file.GetScriptIndex(
                            afie);

                    DebugStr(
                        $"[TXT] Target is MonoBehaviour " +
                        $"(TypeID=114), " +
                        $"MonoScriptIndex={monoId} " +
                        $"(0x{monoId:X4}).");

                    AssetsTools.NET.AssetTypeValueField modifiedBaseField;
                    byte[] monoOriginalData;

                    if (!ImportMonoBehaviourCustom(
                        inputFile,
                        am,
                        afie,
                        assetInst,
                        assetfile_name,
                        out modifiedBaseField,
                        out rawReplacementData,
                        out monoOriginalData))
                    {
                        return false;
                    }

                    atvf =
                        modifiedBaseField;

                    originalSerializedData =
                        monoOriginalData;

                    return true;
                }

                DisplayStr(
                    $"[TXT] Asset PID={afie.PathId}, " +
                    $"Name='{name}' has unsupported TypeID={afie.TypeId}. " +
                    "TXT import supports TextAsset (49) and " +
                    "MonoBehaviour (114).");

                return false;
            }

            // ========================================================
            // FALLBACK BY NAME
            //
            // No PathID was supplied.
            // Search supported text asset types only.
            // ========================================================

            DebugStr(
                $"[TXT] No valid PathID supplied. " +
                $"Searching supported text assets in " +
                $"'{assetfile_name}' by name '{targetName}'.");

            int candidatesScanned = 0;

            foreach (var inf in assetInst.file.AssetInfos)
            {
                // Only types for which this method knows how to import TXT.
                if (inf.TypeId != 49 &&
                    inf.TypeId != 114)
                {
                    continue;
                }

                candidatesScanned++;

                try
                {
                    var candidate =
                        am.GetBaseField(
                            assetInst,
                            inf);

                    if (candidate == null ||
                        candidate.IsDummy)
                    {
                        continue;
                    }

                    string name =
                        GetAssetName(candidate);

                    DebugStr(
                        $"[TXT] Candidate #{candidatesScanned}: " +
                        $"'{name}', PID={inf.PathId}, " +
                        $"TypeID={inf.TypeId}");

                    if (!string.Equals(
                        name.Trim(),
                        targetName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    afie = inf;
                    atvf = candidate;

                    DebugStr(
                        $"[TXT] Found TXT target by name: " +
                        $"'{name}', PID={inf.PathId}, " +
                        $"TypeID={inf.TypeId}");

                    if (inf.TypeId == 49)
                    {
                        DebugStr(
                            "[TXT] Target is TextAsset (TypeID=49); " +
                            "using raw text import.");

                        return ImportTextAssetRaw(
                            inputFile,
                            atvf,
                            afie,
                            out originalSerializedData,
                            out rawReplacementData);
                    }

                    if (inf.TypeId == 114)
                    {
                        ushort monoId =
                            assetInst.file.GetScriptIndex(
                                inf);

                        DebugStr(
                            $"[TXT] Target is MonoBehaviour; " +
                            $"MonoScriptIndex={monoId} " +
                            $"(0x{monoId:X4})");

                        AssetsTools.NET.AssetTypeValueField modifiedBaseField;
                        byte[] monoOriginalData;

                        if (!ImportMonoBehaviourCustom(
                            inputFile,
                            am,
                            afie,
                            assetInst,
                            assetfile_name,
                            out modifiedBaseField,
                            out rawReplacementData,
                            out monoOriginalData))
                        {
                            return false;
                        }

                        atvf =
                            modifiedBaseField;

                        originalSerializedData =
                            monoOriginalData;

                        return true;
                    }
                }
                catch (Exception ex)
                {
                    DisplayStr(
                        $"[TXT] Failed reading candidate PID " +
                        $"{inf.PathId}: " +
                        $"{ex.GetType().Name}: {ex.Message}");

                    DebugStr(
                        ex.ToString());
                }
            }

            DisplayStr(
                $"[TXT] Could not find supported text asset " +
                $"'{targetName}' in '{assetfile_name}'. " +
                $"Candidates scanned: {candidatesScanned}.");

            return false;
        }

        private static bool FindTXTFile(
            string inputFile,
            ref AssetsFileInstance assetInst,
            ref AssetFileInfo afie,
            ref AssetsTools.NET.AssetTypeValueField atvf,
            ref AssetsManager am,
            string asset,
            string assetfile_name,
            string specific_pathid)
        {
            byte[] ignored;
            byte[] ignoredOriginal;

            return FindTXTFile(
                inputFile,
                ref assetInst,
                ref afie,
                ref atvf,
                ref am,
                asset,
                assetfile_name,
                specific_pathid,
                out ignored,
                out ignoredOriginal);
        }

        private static bool FindPNGFile(
            string inputFile,
            ref AssetFileInfo afie,
            ref AssetsFileInstance assetInst,
            ref AssetsTools.NET.AssetTypeValueField atvf,
            ref AssetsManager am,
            string asset,
            string assetfile_name,
            string specific_pathid)
        {
            string targetName =
                Path.GetFileNameWithoutExtension(
                    inputFile).Trim();

            long wantedPathId;
            bool hasWantedPathId =
                TryParsePathId(
                    specific_pathid,
                    out wantedPathId);

            foreach (var inf in assetInst.file.GetAssetsOfType(
                (int)AssetClassID.Texture2D))
            {
                var candidate =
                    am.GetBaseField(
                        assetInst,
                        inf);

                if (candidate == null ||
                    candidate.IsDummy)
                {
                    continue;
                }

                string name =
                    GetAssetName(candidate);

                if (!string.Equals(
                    name?.Trim(),
                    targetName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (hasWantedPathId &&
                    inf.PathId != wantedPathId)
                {
                    continue;
                }

                afie = inf;
                atvf = candidate;

                DebugStr(
                    $"Found equivalent Texture2D: " +
                    $"{name}, path ID: {inf.PathId}");

                return true;
            }

            DisplayStr(
                $"Couldn't find equivalent image for {asset} " +
                $"(Asset: {assetfile_name}, Texture: {targetName})");

            return false;
        }
    }
}