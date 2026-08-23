using AssetsTools.NET.Extra;
using AssetsTools.NET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace UAFGJ
{
    partial class Program
    {
        private sealed class AssetFingerprint
        {
            public long PathId;
            public int TypeId;
            public long ByteSize;
            public ushort MonoScriptIndex;
            public string Name = "";
            public string SerializedSha256 = "";
        }

        private sealed class AssetsFileSnapshot
        {
            public string Name = "";
            public string Sha256 = "";
            public long SerializedLength;
            public List<AssetFingerprint> Assets = new List<AssetFingerprint>();
        }

        static private void HandleBundle(string ab, string input_file, string specific_pathid)
        {
            string originalBundleSha = Sha256File(ab);
            long originalBundleLength = new FileInfo(ab).Length;
            DebugStr($"[CHECK] INPUT bundle length={originalBundleLength} SHA256={originalBundleSha}");
            DebugStr($"[CHECK] INPUT replacement SHA256={Sha256File(input_file)} length={new FileInfo(input_file).Length}");
            DebugStr($"[CHECK] classdata.tpk SHA256={(File.Exists("classdata.tpk") ? Sha256File("classdata.tpk") : "MISSING")}");

            AssetsManager am = new AssetsManager();
            try
            {
                BundleFileInstance bundleInst = GetBundleInst(am, ab);
                if (bundleInst == null) return;

                string assetfile_name = GetRightAssetFileNameFromBundle(bundleInst, ab);
                if (string.IsNullOrEmpty(assetfile_name)) return;

                AssetsFileInstance assetInst = GetAssetInst(am, bundleInst, assetfile_name, ab);
                if (assetInst == null) return;

                if (!File.Exists("classdata.tpk"))
                {
                    DisplayStr("classdata.tpk not found!");
                    return;
                }

                am.LoadClassPackage("classdata.tpk");
                if (!assetInst.file.Metadata.TypeTreeEnabled)
                    am.LoadClassDatabaseFromPackage(assetInst.file.Metadata.UnityVersion);
                DebugStr("Loaded classdata.tpk");

                AssetBundleCompressionType originalCompression = bundleInst.file.GetCompressionType(bundleInst.file.BlockAndDirInfo.BlockInfos);
                var originalDirectoryNames = bundleInst.file.BlockAndDirInfo.DirectoryInfos.Select(d => d.Name).ToList();
                DebugStr($"[CHECK] INPUT bundle compression={originalCompression}; directory entries={originalDirectoryNames.Count}");

                AssetsFileSnapshot beforeSnapshot = CaptureAssetsFileSnapshot(am, assetInst, assetfile_name);
                DebugStr($"[CHECK] BEFORE assets '{assetfile_name}' SHA256={beforeSnapshot.Sha256} serializedLength={beforeSnapshot.SerializedLength} assets={beforeSnapshot.Assets.Count}");

                AssetsTools.NET.AssetTypeValueField atvf = null;
                AssetFileInfo afie = null;
                byte[] rawReplacementData = null;
                byte[] originalTargetData = null;

                bool isTextReplacement = !string.Equals(
                    Path.GetExtension(input_file),
                    ".png",
                    StringComparison.OrdinalIgnoreCase);

                if (isTextReplacement)
                {
                    if (!FindTXTFile(
                        input_file,
                        ref assetInst,
                        ref afie,
                        ref atvf,
                        ref am,
                        ab,
                        assetfile_name,
                        specific_pathid,
                        out rawReplacementData,
                        out originalTargetData))
                    {
                        DisplayStr("Failed to replace TXT!");
                        return;
                    }
                }
                else
                {
                    if (!FindPNGFile(
                        input_file,
                        ref afie,
                        ref assetInst,
                        ref atvf,
                        ref am,
                        ab,
                        assetfile_name,
                        specific_pathid))
                        return;

                    int format = atvf["m_TextureFormat"].AsInt;
                    if (!ImportTexturesCustom(ref atvf, input_file, format))
                        return;

                    rawReplacementData = atvf.WriteToByteArray();
                    originalTargetData = null;
                }

                if (afie == null || atvf == null || rawReplacementData == null || rawReplacementData.Length == 0)
                {
                    DisplayStr("[CHECK] Replacement target or data is missing; refusing to write.");
                    return;
                }

                int expectedTargetTypeId = afie.TypeId;
                DebugStr($"[CHECK] Replacement mode={(isTextReplacement ? "TXT" : "PNG/GenericAsset")}, PID={afie.PathId}, TypeID={expectedTargetTypeId}");

                AssetFingerprint targetBefore = beforeSnapshot.Assets.FirstOrDefault(a => a.PathId == afie.PathId);
                if (targetBefore == null)
                    throw new InvalidDataException("Target PathID disappeared from pre-write snapshot.");

                if (targetBefore.TypeId != expectedTargetTypeId)
                    throw new InvalidDataException($"Replacement target TypeID changed before save: {targetBefore.TypeId}->{expectedTargetTypeId}");

                DebugStr($"[CHECK] TARGET BEFORE PID={targetBefore.PathId} TypeID={targetBefore.TypeId} ByteSize={targetBefore.ByteSize} ScriptIndex={targetBefore.MonoScriptIndex} Name='{targetBefore.Name}' SHA256={targetBefore.SerializedSha256}");
                DebugStr($"[CHECK] TARGET AFTER  PID={afie.PathId} TypeID={afie.TypeId} bytes={rawReplacementData.Length} SHA256={Sha256Hex(rawReplacementData)}");

                SaveAssetBundle(
                    atvf,
                    afie,
                    assetInst,
                    bundleInst,
                    ab,
                    assetfile_name,
                    Path.GetFileNameWithoutExtension(input_file));

                string tempBundle = ab + "_temp";
                string rawTempSha = Sha256File(tempBundle);
                DebugStr($"[CHECK] RAW temp bundle length={new FileInfo(tempBundle).Length} SHA256={rawTempSha}");

                if (!am.UnloadAllAssetsFiles(true))
                    DisplayStr("Could not unload all asset files!");
                if (!am.UnloadAllBundleFiles())
                    DisplayStr("Could not unload all bundle files!");

                PackBundlePreservingFormat(
                    ab,
                    assetfile_name,
                    afie.PathId,
                    specific_pathid,
                    input_file,
                    rawReplacementData,
                    beforeSnapshot,
                    originalBundleSha,
                    originalBundleLength,
                    originalCompression,
                    originalDirectoryNames,
                    expectedTargetTypeId,
                    isTextReplacement);

                DisplayStr("Done!");
            }
            catch (Exception ex)
            {
                DisplayStr("[FATAL] Bundle handling failed: " + ex.GetType().Name + ": " + ex.Message);
                DebugStr(ex.ToString());
            }
        }

        private static void SaveAssetBundle(
            AssetsTools.NET.AssetTypeValueField modifiedBaseField,
            AssetFileInfo afie,
            AssetsFileInstance assetInst,
            BundleFileInstance bundleInst,
            string ab,
            string assetfile_name,
            string input_noext)
        {
            if (modifiedBaseField == null)
                throw new InvalidOperationException("Modified base field is null.");
            if (afie == null)
                throw new InvalidOperationException("Asset info is null.");

            ushort monoId = assetInst.file.GetScriptIndex(afie);
            byte[] bytes = modifiedBaseField.WriteToByteArray();
            DebugStr($"[SAVE] Existing-asset replacer: PID={afie.PathId}, TypeID={afie.TypeId}, MonoScriptIndex={monoId} (0x{monoId:X4}), bytes={bytes.Length}, SHA256={Sha256Hex(bytes)}");
            DebugStr("[SAVE] Using AssetsReplacerFromMemory(assetsFile, assetInfo, baseField) to preserve all original asset metadata.");

            var repl = new AssetsReplacerFromMemory(assetInst.file, afie, modifiedBaseField);

            byte[] newAssetData;
            using (var stream = new MemoryStream())
            using (var writer = new AssetsFileWriter(stream))
            {
                assetInst.file.Write(writer, 0, new List<AssetsReplacer> { repl });
                newAssetData = stream.ToArray();
            }

            DebugStr($"[SAVE] Inner assets file size={newAssetData.Length} SHA256={Sha256Hex(newAssetData)}");

            string tempBundle = ab + "_temp";
            using (var fileStream = new FileStream(tempBundle, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var bunWriter = new AssetsFileWriter(fileStream))
            {
                var bunRepl = new BundleReplacerFromMemory(assetfile_name, null, true, newAssetData, -1);
                bundleInst.file.Write(bunWriter, new List<BundleReplacer> { bunRepl });
            }

            DebugStr($"[SAVE] Temporary bundle written: {tempBundle}");
        }

        private static void PackBundlePreservingFormat(
            string realName,
            string assetfileName,
            long targetPathId,
            string specificPathId,
            string dumpPath,
            byte[] expectedTargetData,
            AssetsFileSnapshot beforeSnapshot,
            string originalBundleSha,
            long originalBundleLength,
            AssetBundleCompressionType originalCompression,
            List<string> originalDirectoryNames,
            int expectedTargetTypeId,
            bool isTextReplacement)
        {
            string fakeName = realName + "_temp";
            if (!File.Exists(fakeName))
                throw new FileNotFoundException("Temporary bundle missing.", fakeName);

            DebugStr("[CHECK] ===== PRE-PACK CONTAINER VALIDATION =====");
            ValidateBundleContainer(fakeName);

            AssetsManager am = new AssetsManager();
            string finalTemp = realName + ".new";
            try
            {
                BundleFileInstance bun = am.LoadBundleFile(fakeName);
                if (bun == null)
                    throw new InvalidDataException("Could not reopen temporary bundle.");

                using (var stream = new FileStream(finalTemp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new AssetsFileWriter(stream))
                {
                    DebugStr($"[PACK] Packing with the original project API using original compression={originalCompression}.");
                    bun.file.Pack(bun.file.Reader, writer, originalCompression);
                }

                if (!am.UnloadAllBundleFiles())
                    DisplayStr("[PACK] Could not unload temporary bundle handles.");

                string finalSha = Sha256File(finalTemp);
                DebugStr($"[CHECK] PACKED .new length={new FileInfo(finalTemp).Length} SHA256={finalSha}");

                DebugStr("[CHECK] ===== FINAL CONTAINER VALIDATION =====");
                ValidateBundleContainer(finalTemp);

                ValidateFinalBundle(
                    finalTemp,
                    assetfileName,
                    targetPathId,
                    dumpPath,
                    expectedTargetData,
                    beforeSnapshot,
                    originalCompression,
                    originalDirectoryNames,
                    expectedTargetTypeId,
                    isTextReplacement);

                if (string.Equals(finalSha, originalBundleSha, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The final bundle SHA256 is identical to the input bundle. Refusing to replace because no binary change was detected.");
                }

                DebugStr("[CHECK] ===== ALL PRE-REPLACE CHECKS PASSED =====");
                DebugStr($"[CHECK] INPUT  SHA256={originalBundleSha} length={originalBundleLength}");
                DebugStr($"[CHECK] OUTPUT SHA256={finalSha} length={new FileInfo(finalTemp).Length}");

                File.Move(finalTemp, realName, true);
                string committedSha = Sha256File(realName);
                DebugStr($"[CHECK] COMMITTED bundle SHA256={committedSha} length={new FileInfo(realName).Length}");

                if (!string.Equals(committedSha, finalSha, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Committed bundle SHA256 differs from validated .new file.");

                if (File.Exists(fakeName))
                    File.Delete(fakeName);
            }
            finally
            {
                try { am.UnloadAllAssetsFiles(true); } catch { }
                try { am.UnloadAllBundleFiles(); } catch { }
            }
        }

        private static void ValidateBundleContainer(string bundlePath)
        {
            AssetsManager validator = new AssetsManager();
            try
            {
                BundleFileInstance bundle = validator.LoadBundleFile(bundlePath, true);
                if (bundle == null)
                    throw new InvalidDataException("Bundle could not be reopened: " + bundlePath);

                AssetBundleCompressionType compression = bundle.file.GetCompressionType(bundle.file.BlockAndDirInfo.BlockInfos);
                int dirCount = bundle.file.BlockAndDirInfo.DirectoryInfos.Length;
                DebugStr($"[CHECK] Container OK: path={bundlePath}");
                DebugStr($"[CHECK] Signature={bundle.file.Header.Signature}, UnityVersion={bundle.file.Header.EngineVersion}, compression={compression}, dirs={dirCount}, blocks={bundle.file.BlockAndDirInfo.BlockInfos.Length}");

                int assetsCount = 0;
                for (int i = 0; i < dirCount; i++)
                {
                    var dir = bundle.file.BlockAndDirInfo.DirectoryInfos[i];
                    int fileIndex = bundle.file.GetFileIndex(dir.Name);
                    if (fileIndex < 0 || !bundle.file.IsAssetsFile(fileIndex)) continue;

                    AssetsFileInstance inst = validator.LoadAssetsFileFromBundle(bundle, fileIndex, true);
                    if (inst == null)
                        throw new InvalidDataException("Could not load assets entry: " + dir.Name);

                    assetsCount++;
                    DebugStr($"[CHECK]   assets file '{dir.Name}' loaded; asset count={inst.file.AssetInfos.Count}, unity={inst.file.Metadata.UnityVersion}");
                }

                DebugStr($"[CHECK] Serialized asset files successfully loaded: {assetsCount}");
            }
            finally
            {
                try { validator.UnloadAllAssetsFiles(true); } catch { }
                try { validator.UnloadAllBundleFiles(); } catch { }
            }
        }

        private static AssetsFileSnapshot CaptureAssetsFileSnapshot(AssetsManager am, AssetsFileInstance inst, string name)
        {
            var snapshot = new AssetsFileSnapshot { Name = name };

            using (var stream = new MemoryStream())
            using (var writer = new AssetsFileWriter(stream))
            {
                inst.file.Write(writer, 0, new List<AssetsReplacer>());
                byte[] serializedFile = stream.ToArray();
                snapshot.SerializedLength = serializedFile.Length;
                snapshot.Sha256 = Sha256Hex(serializedFile);
            }

            foreach (var inf in inst.file.AssetInfos)
            {
                var fp = new AssetFingerprint
                {
                    PathId = inf.PathId,
                    TypeId = inf.TypeId,
                    ByteSize = inf.ByteSize,
                    MonoScriptIndex = inst.file.GetScriptIndex(inf)
                };

                try
                {
                    var bf = am.GetBaseField(inst, inf);
                    fp.Name = TryGetName(bf);
                    fp.SerializedSha256 = Sha256Hex(bf.WriteToByteArray());
                }
                catch (Exception ex)
                {
                    fp.SerializedSha256 = "UNAVAILABLE:" + ex.GetType().Name;
                    DebugStr($"[CHECK] Could not fingerprint asset PID={fp.PathId}: {ex.Message}");
                }

                snapshot.Assets.Add(fp);
            }

            return snapshot;
        }

        private static void ValidateFinalBundle(
            string bundlePath,
            string assetfileName,
            long targetPathId,
            string dumpPath,
            byte[] expectedTargetData,
            AssetsFileSnapshot beforeSnapshot,
            AssetBundleCompressionType originalCompression,
            List<string> originalDirectoryNames,
            int expectedTargetTypeId,
            bool isTextReplacement)
        {
            AssetsManager am = new AssetsManager();
            try
            {
                BundleFileInstance bundle = am.LoadBundleFile(bundlePath, true);
                if (bundle == null)
                    throw new InvalidDataException("Final bundle cannot be reopened.");

                AssetBundleCompressionType finalCompression = bundle.file.GetCompressionType(bundle.file.BlockAndDirInfo.BlockInfos);
                var finalDirectoryNames = bundle.file.BlockAndDirInfo.DirectoryInfos.Select(d => d.Name).ToList();

                if (finalCompression != originalCompression)
                    throw new InvalidDataException($"Compression changed: original={originalCompression}, final={finalCompression}");

                if (!originalDirectoryNames.SequenceEqual(finalDirectoryNames, StringComparer.Ordinal))
                    throw new InvalidDataException("Bundle directory entry names/order changed after repack.");

                DebugStr($"[CHECK] Final compression={finalCompression}; directory layout identical ({finalDirectoryNames.Count} entries).");

                int fileIndex = bundle.file.GetFileIndex(assetfileName);
                if (fileIndex < 0)
                    throw new InvalidDataException("Expected assets file entry is missing: " + assetfileName);

                AssetsFileInstance inst = am.LoadAssetsFileFromBundle(bundle, fileIndex, true);
                if (inst == null)
                    throw new InvalidDataException("Expected assets file could not be reopened: " + assetfileName);

                AssetsFileSnapshot afterSnapshot = CaptureAssetsFileSnapshot(am, inst, assetfileName);
                DebugStr($"[CHECK] AFTER assets '{assetfileName}' SHA256={afterSnapshot.Sha256} serializedLength={afterSnapshot.SerializedLength} assets={afterSnapshot.Assets.Count}");

                if (afterSnapshot.Assets.Count != beforeSnapshot.Assets.Count)
                    throw new InvalidDataException($"Asset count changed: before={beforeSnapshot.Assets.Count} after={afterSnapshot.Assets.Count}");

                foreach (var before in beforeSnapshot.Assets)
                {
                    var after = afterSnapshot.Assets.FirstOrDefault(a => a.PathId == before.PathId);
                    if (after == null)
                        throw new InvalidDataException("PathID disappeared after rewrite: " + before.PathId);

                    if (after.TypeId != before.TypeId)
                        throw new InvalidDataException($"TypeID changed for PID {before.PathId}: {before.TypeId}->{after.TypeId}");

                    if (after.MonoScriptIndex != before.MonoScriptIndex)
                        throw new InvalidDataException($"MonoScriptIndex changed for PID {before.PathId}: {before.MonoScriptIndex}->{after.MonoScriptIndex}");

                    if (before.PathId != targetPathId &&
                        !string.Equals(before.SerializedSha256, after.SerializedSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            $"UNEXPECTED ASSET CHANGE: PID={before.PathId} " +
                            $"name='{before.Name}' " +
                            $"SHA {before.SerializedSha256}->{after.SerializedSha256}");
                    }
                }

                var targetBefore = beforeSnapshot.Assets.FirstOrDefault(a => a.PathId == targetPathId);
                if (targetBefore == null)
                    throw new InvalidDataException("Target PathID was not present in the original snapshot: " + targetPathId);

                var targetInfo = inst.file.AssetInfos.FirstOrDefault(a => a.PathId == targetPathId);
                if (targetInfo == null)
                    throw new InvalidDataException("Target PathID missing after repack: " + targetPathId);

                // The target type must remain exactly the same as before the replacement.
                if (targetInfo.TypeId != expectedTargetTypeId)
                    throw new InvalidDataException($"Target TypeID changed: original={expectedTargetTypeId}, final={targetInfo.TypeId}");

                DebugStr($"[CHECK] Target type preserved: PID={targetInfo.PathId}, TypeID={targetInfo.TypeId}");

                ushort finalMonoId = inst.file.GetScriptIndex(targetInfo);

                // Only MonoBehaviour (114) requires a valid MonoScript index.
                if (expectedTargetTypeId == 114)
                {
                    if (finalMonoId == 0xFFFF)
                        throw new InvalidDataException("Target MonoBehaviour lost its MonoScript index.");

                    DebugStr($"[CHECK] Target MonoScriptIndex={finalMonoId} (0x{finalMonoId:X4})");
                }
                else
                {
                    DebugStr($"[CHECK] Non-MonoBehaviour target; MonoScriptIndex={finalMonoId} (0x{finalMonoId:X4}) accepted.");
                }

                var targetField = am.GetBaseField(inst, targetInfo);
                if (targetField == null || targetField.IsDummy)
                    throw new InvalidDataException("Final target base field is null/dummy.");

                byte[] finalTargetData = targetField.WriteToByteArray();
                string finalTargetSha = Sha256Hex(finalTargetData);
                string expectedTargetSha = Sha256Hex(expectedTargetData);

                DebugStr($"[CHECK] Target payload SHA expected={expectedTargetSha} actual={finalTargetSha}, bytes expected={expectedTargetData.Length} actual={finalTargetData.Length}");

                if (!string.Equals(finalTargetSha, expectedTargetSha, StringComparison.OrdinalIgnoreCase) ||
                    finalTargetData.Length != expectedTargetData.Length)
                {
                    throw new InvalidDataException("Final target payload does not match the in-memory replacement payload.");
                }

                // TXT is valid for MonoBehaviour (114) and TextAsset (49).
                if (isTextReplacement)
                {
                    if (expectedTargetTypeId != 114 && expectedTargetTypeId != 49)
                    {
                        throw new InvalidDataException(
                            "TXT replacement requested, but target TypeID " +
                            expectedTargetTypeId +
                            " is not supported (114=MonoBehaviour, 49=TextAsset).");
                    }

                    ValidateDumpAgainstBaseField(
                        dumpPath,
                        targetField);

                    if (expectedTargetTypeId == 114)
                        DebugStr("[CHECK] Final MonoBehaviour TXT dump/value validation PASSED.");
                    else
                        DebugStr("[CHECK] Final TextAsset TXT dump/value validation PASSED.");
                }
                else
                {
                    DebugStr("[CHECK] Generic asset replacement payload validation PASSED.");
                }
            }
            finally
            {
                try { am.UnloadAllAssetsFiles(true); } catch { }
                try { am.UnloadAllBundleFiles(); } catch { }
            }
        }

        private static string TryGetName(AssetsTools.NET.AssetTypeValueField field)
        {
            try
            {
                if (field == null || field.IsDummy)
                    return "<dummy>";

                var name = field["m_Name"];
                return name?.AsString ?? "";
            }
            catch
            {
                return "";
            }
        }

        private static string Sha256File(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(sha.ComputeHash(stream));
        }

        private static string Sha256Hex(byte[] data)
        {
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(data ?? Array.Empty<byte>()));
        }
    }
}
