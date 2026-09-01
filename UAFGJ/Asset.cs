using AssetsTools.NET.Extra;
using AssetsTools.NET;
using System;
using System.IO;
using System.Collections.Generic;

namespace UAFGJ
{
    partial class Program
    {
        static private void HandleAsset(string asset, string input_file, string specific_pathid, string fileKind)
        {
            AssetsManager am = new AssetsManager();
            AssetsFileInstance assetInst = null;

            try
            {
                assetInst = am.LoadAssetsFile(asset, true);
                if (assetInst == null)
                {
                    DisplayStr("Could not load assets file: " + asset);
                    return;
                }

                if (!File.Exists("classdata.tpk"))
                {
                    DisplayStr("classdata.tpk not found!");
                    return;
                }

                ClassPackageFile meta_class = am.LoadClassPackage("classdata.tpk");
                ClassDatabaseFile meta_db = null;
                if (!assetInst.file.Metadata.TypeTreeEnabled)
                    meta_db = am.LoadClassDatabaseFromPackage(assetInst.file.Metadata.UnityVersion);

                DebugStr("Loaded classdata.tpk");

                AssetsTools.NET.AssetTypeValueField atvf = null;
                AssetFileInfo afie = null;
                byte[] rawReplacementData = null;
                byte[] originalSerializedData = null;

                if (!string.Equals(Path.GetExtension(input_file), ".png", StringComparison.OrdinalIgnoreCase))
                {
                    // Despite the name, "Find" also replaces stuff
                    if (!FindTXTFile(
                        input_file,
                        ref assetInst,
                        ref afie,
                        ref atvf,
                        ref am,
                        asset,
                        assetInst.name,
                        specific_pathid,
                        fileKind,
                        out rawReplacementData,
                        out originalSerializedData))
                    {
                        DisplayStr("Failed to replace TXT!");
                        return;
                    }
                }
                else
                {
                    // Despite the name, "Find" also replaces stuff
                    if (!FindPNGFile(
                        input_file,
                        ref afie,
                        ref assetInst,
                        ref atvf,
                        ref am,
                        asset,
                        assetInst.name,
                        specific_pathid,
                        fileKind))
                    {
                        return;
                    }

                    int format = atvf["m_TextureFormat"].AsInt;

                    if (!ImportTexturesCustom(ref atvf, input_file, format, fileKind))
                    {
                        DisplayStr("Could not import PNG!");
                        return;
                    }

                    rawReplacementData = atvf.WriteToByteArray();
                }

                if (afie == null || rawReplacementData == null || rawReplacementData.Length == 0)
                {
                    DisplayStr("Invalid replacement state.");
                    return;
                }

                ushort monoId = assetInst.file.GetScriptIndex(afie);
                DebugStr($"[ASSET] Resolved MonoScript index: {monoId} (0x{monoId:X4}) for PID={afie.PathId}");

                var repl = new AssetsReplacerFromMemory(
                    afie.PathId,
                    (int)afie.TypeId,
                    monoId,
                    rawReplacementData);

                string fakeName = asset + "_temp";

                DebugStr("[ASSET] Writing replacement to temporary file: " + fakeName);

                using (var stream = new FileStream(
                    fakeName,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                using (var writer = new AssetsFileWriter(stream))
                {
                    assetInst.file.Write(
                        writer,
                        0,
                        new List<AssetsReplacer> { repl });

                }

                // The original file is still held by AssetsTools until unload.
                am.UnloadAllAssetsFiles(true);

                DebugStr("[ASSET] Handles released; replacing original file.");
                File.Move(fakeName, asset, true);

                DisplayStr("Successfully replaced asset!");
            }
            catch (Exception ex)
            {
                DisplayStr("[FATAL] Assets file handling failed: " + ex.GetType().Name + ": " + ex.Message);
                DebugStr(ex.ToString());
            }
            finally
            {
                try { am.UnloadAllAssetsFiles(true); } catch { }
            }
        }
    }
}
