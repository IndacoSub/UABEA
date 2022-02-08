using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using TexturePlugin;
using UABEAvalonia;

namespace UAFGJ
{
    class Program
    {
        StreamReader sr;
        AssetsFileWriter aw;

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                DisplayStr("Not enough arguments!");
                return;
            }

            string asset_or_ab = args[0];
            string input_file = args[1];

            if (!File.Exists(asset_or_ab))
            {
                DisplayStr(".asset/.ab/.txt file not found!");
                return;
            }

            if (!File.Exists(input_file))
            {
                DisplayStr(".png/.txt file not found!");
                return;
            }

            DoStuff(asset_or_ab, input_file);
        }

        static private bool ImportMonoBehaviourCustom(string input_file, AssetsManager am, AssetFileInfoEx afie, AssetsFileInstance assetInst, string assetname)
        {
            AssetContainer ac = new AssetContainer(afie, assetInst);
            string fake_name = assetname + "_temp";

            using (FileStream fs = File.OpenRead(input_file))
            {
                using (StreamReader sr = new StreamReader(fs))
                {
                    AssetImportExport importer = new AssetImportExport();
                    byte[]? bytes = importer.ImportTextAsset(sr);

                    if (bytes == null)
                    {
                        DisplayStr("Parse error: Something went wrong when reading the dump file.");
                        return false;
                    }

                    AssetsReplacer replacer = AssetImportExport.CreateAssetReplacer(ac, bytes);
                    using (var stream = File.OpenWrite(fake_name))
                    using (var writer = new AssetsFileWriter(stream))
                    {
                        assetInst.file.Write(writer, 0, new List<AssetsReplacer>() { replacer }, 0);
                    }
                }
            }

            am.UnloadAllAssetsFiles(true);
            File.Move(fake_name, assetname, true);

            return true;
        }

        private bool StartsWithSpace(string str, string value)
        {
            return str.StartsWith(value + " ");
        }

        public byte[]? ImportTextAsset(StreamReader sr, out string? exceptionMessage)
        {
            this.sr = sr;
            using (MemoryStream ms = new MemoryStream())
            {
                aw = new AssetsFileWriter(ms);
                aw.bigEndian = false;
                try
                {
                    ImportTextAssetLoop();
                    exceptionMessage = null;
                }
                catch (Exception ex)
                {
                    exceptionMessage = ex.Message;
                    return null;
                }
                return ms.ToArray();
            }
        }

        private string UnescapeDumpString(string str)
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
                    if (c == '\\')
                        sb.Append('\\');
                    else if (c == 'r')
                        sb.Append('\r');
                    else if (c == 'n')
                        sb.Append('\n');
                    else
                        sb.Append(c);

                    escaping = false;
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        private void ImportTextAssetLoop()
        {
            Stack<bool> alignStack = new Stack<bool>();
            while (true)
            {
                string? line = sr.ReadLine();
                if (line == null)
                    return;

                int thisDepth = 0;
                while (line[thisDepth] == ' ')
                    thisDepth++;

                if (line[thisDepth] == '[') //array index, ignore
                    continue;

                if (thisDepth < alignStack.Count)
                {
                    while (thisDepth < alignStack.Count)
                    {
                        if (alignStack.Pop())
                            aw.Align();
                    }
                }

                bool align = line.Substring(thisDepth, 1) == "1";
                int typeName = thisDepth + 2;
                int eqSign = line.IndexOf('=');
                string valueStr = line.Substring(eqSign + 1).Trim();

                if (eqSign != -1)
                {
                    string check = line.Substring(typeName);
                    //this list may be incomplete
                    if (StartsWithSpace(check, "bool"))
                    {
                        aw.Write(bool.Parse(valueStr));
                    }
                    else if (StartsWithSpace(check, "UInt8"))
                    {
                        aw.Write(byte.Parse(valueStr));
                    }
                    else if (StartsWithSpace(check, "SInt8"))
                    {
                        aw.Write(sbyte.Parse(valueStr));
                    }
                    else if (StartsWithSpace(check, "UInt16"))
                    {
                        aw.Write(ushort.Parse(valueStr));
                    }
                    else if (StartsWithSpace(check, "SInt16"))
                    {
                        aw.Write(short.Parse(valueStr));
                    }
                    else if (StartsWithSpace(check, "unsigned int"))
                    {
                        aw.Write(uint.Parse(valueStr));
                    }
                    else if (StartsWithSpace(check, "int"))
                    {
                        aw.Write(int.Parse(valueStr));
                    }
                    else if (StartsWithSpace(check, "UInt64"))
                    {
                        aw.Write(ulong.Parse(valueStr));
                    }
                    else if (StartsWithSpace(check, "SInt64"))
                    {
                        aw.Write(long.Parse(valueStr));
                    }
                    else if (StartsWithSpace(check, "float"))
                    {
                        aw.Write(float.Parse(valueStr));
                    }
                    else if (StartsWithSpace(check, "double"))
                    {
                        aw.Write(double.Parse(valueStr));
                    }
                    else if (StartsWithSpace(check, "string"))
                    {
                        int firstQuote = valueStr.IndexOf('"');
                        int lastQuote = valueStr.LastIndexOf('"');
                        string valueStrFix = valueStr.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
                        valueStrFix = UnescapeDumpString(valueStrFix);
                        aw.WriteCountStringInt32(valueStrFix);
                    }

                    if (align)
                    {
                        aw.Align();
                    }
                }
                else
                {
                    alignStack.Push(align);
                }
            }
        }

        static private void DebugStr(string s)
        {
            //Console.WriteLine(s);
        }

        static private void DisplayStr(string s)
        {
            Console.WriteLine(s);
        }

        static private void HandleBundle(string ab, string png)
        {
            AssetsManager am = new AssetsManager();

            BundleFileInstance bundleInst = GetBundleInst(am, ab);
            if (bundleInst == null)
            {
                return;
            }

            string assetfile_name = GetRightAssetFileNameFromBundle(bundleInst, ab);
            if (string.IsNullOrEmpty(assetfile_name))
            {
                return;
            }

            AssetsFileInstance assetInst = GetAssetInst(am, bundleInst, assetfile_name, ab);
            if (assetInst == null)
            {
                return;
            }

            // Load classdata.tpk
            if (!File.Exists("classdata.tpk"))
            {
                DisplayStr("classdata.tpk not found!");
                return;
            }

            am.LoadClassPackage("classdata.tpk");
            if (!assetInst.file.typeTree.hasTypeTree)
            {
                am.LoadClassDatabaseFromPackage(assetInst.file.typeTree.unityVersion);
            }
            DebugStr("Loaded classdata.tpk");

            AssetTypeValueField atvf = new AssetTypeValueField(); // "baseField"
            AssetFileInfoEx afie = new AssetFileInfoEx();
            int selected = -1;
            string png_noext = png;
            int cont = 0;
            int format = 0;

            // Iterate the files in assetInst
            foreach (var inf in assetInst.table.GetAssetsOfType((int)AssetClassID.Texture2D))
            {
                afie = inf;
                atvf = am.GetTypeInstance(assetInst, afie).GetBaseField();

                var name = atvf.Get("m_Name").GetValue().AsString();
                format = atvf.Get("m_TextureFormat").GetValue().AsInt();
                DebugStr(name);

                png_noext = Path.GetFileNameWithoutExtension(png);
                png_noext = png_noext.ToLowerInvariant();
                if (name == png_noext)
                {
                    selected = cont;
                    break;
                }
                cont++;
            }

            // Selected "png" to replace not found
            if (selected == -1)
            {
                DisplayStr("Couldn't find equivalent image for " + ab + " (Asset: " + assetfile_name + ", Texture: " + png_noext + ")");
                return;
            }

            // Import textures (id is basically atvf but changed)
            bool ret = ImportTexturesCustom(atvf, png, out AssetTypeValueField id, format);
            if (id == null || !ret)
            {
                DisplayStr("Could not set image for " + ab + " (Asset: " + assetfile_name + ", Texture: " + png_noext + ")");
                return;
            }

            SaveAssetBundle(id, afie, assetInst, bundleInst, ab, assetfile_name, png_noext);

            // Unload everything
            am.UnloadAllAssetsFiles(true);
            am.UnloadAllBundleFiles();

            string ab_real_name = ab;
            PackLZ4Bundle(ab_real_name);

            DisplayStr("Done!");
        }

        static private void HandleAsset(string asset, string input_file)
        {
            AssetsManager am = new AssetsManager();
            AssetsFileInstance assetInst = am.LoadAssetsFile(asset, true);

            am.LoadClassPackage("classdata.tpk");
            if (!assetInst.file.typeTree.hasTypeTree)
            {
                am.LoadClassDatabaseFromPackage(assetInst.file.typeTree.unityVersion);
            }
            DebugStr("Loaded classdata.tpk");

            AssetTypeValueField atvf = new AssetTypeValueField(); // "baseField"
            AssetFileInfoEx afie = new AssetFileInfoEx();
            int selected = -1;
            int cont = 0;
            int format = 0;

            string file_noext = input_file;
            string assetfile_name = assetInst.name;

            AssetTypeValueField id = new AssetTypeValueField();

            if (input_file.Contains(".png"))
            {
                // Iterate the files in assetInst
                foreach (var inf in assetInst.table.GetAssetsOfType((int)AssetClassID.Texture2D))
                {
                    afie = inf;
                    atvf = am.GetTypeInstance(assetInst, afie).GetBaseField();

                    var name = atvf.Get("m_Name").GetValue().AsString();
                    format = atvf.Get("m_TextureFormat").GetValue().AsInt();
                    DebugStr(name);

                    file_noext = Path.GetFileNameWithoutExtension(input_file);
                    file_noext = file_noext.ToLowerInvariant();
                    if (name.ToLowerInvariant() == file_noext)
                    {
                        selected = cont;
                        break;
                    }
                    cont++;
                }

                // Selected "png" to replace not found
                if (selected == -1)
                {
                    DisplayStr("Couldn't find equivalent image for " + asset + " (Asset: " + assetfile_name + ", InputFile: " + file_noext + ")");
                    return;
                }

                // Import textures (id is basically atvf but changed)
                bool ret = ImportTexturesCustom(atvf, input_file, out AssetTypeValueField id_out, format);
                if (id_out == null || !ret)
                {
                    DisplayStr("Could not set image for " + asset + " (Asset: " + assetfile_name + ", InputFile: " + file_noext + ")");
                    return;
                }

                id = id_out;
            } else
            {
                // Assume .txt

                if(!input_file.Contains(".txt"))
                {
                    Console.WriteLine("Unsupported extension: " + Path.GetExtension(input_file));
                    return;
                }

                // Iterate the files in assetInst
                foreach (var inf in assetInst.table.GetAssetsOfType((int)AssetClassID.MonoBehaviour))
                {
                    afie = inf;
                    atvf = am.GetTypeInstance(assetInst, afie).GetBaseField();

                    var name = atvf.Get("m_Name").GetValue().AsString();
                    DebugStr(name);

                    file_noext = Path.GetFileNameWithoutExtension(input_file);
                    file_noext = file_noext.ToLowerInvariant();
                    if (name.ToLowerInvariant() == file_noext)
                    {
                        selected = cont;
                        break;
                    }
                    cont++;
                }

                // Selected "txt" to replace not found
                if (selected == -1)
                {
                    DisplayStr("Couldn't find equivalent MonoBehaviour for " + asset + " (Asset: " + assetfile_name + ", InputFile: " + file_noext + ")");
                    return;
                } else
                {
                    DisplayStr("Found equivalent: " + selected + ": " + atvf.Get("m_Name").GetValue().AsString() + ", path ID: " + afie.index);
                }

                bool ret = ImportMonoBehaviourCustom(input_file, am, afie, assetInst, asset);
                if(!ret)
                {
                    DisplayStr("Couldn't replace MonoBehaviour!");
                }
                return;
            }

            // buffer
            var newGoBytes = id.WriteToByteArray();
            var repl = new AssetsReplacerFromMemory(0, afie.index, (int)afie.curFileType, 0xffff, newGoBytes);

            if (repl == null || repl.ToString().Length == 0)
            {
                DisplayStr("The asset replacer was null for " + asset + " (Asset: " + assetfile_name + ", InputFile: " + file_noext + ")");
                return;
            }

            string fake_name = asset + "_temp";
            using (var stream = File.OpenWrite(fake_name))
            using (var writer = new AssetsFileWriter(stream))
            {
                assetInst.file.Write(writer, 0, new List<AssetsReplacer>() { repl }, 0);
            }

            string real_name = asset;
            // Unload everything
            am.UnloadAllAssetsFiles(true);
            File.Move(fake_name, real_name, true);
        }

        static private void DoStuff(string asset_or_ab, string png)
        {
            DebugStr("Opening file: " + asset_or_ab);
            DetectedFileType file_type = AssetBundleDetector.DetectFileType(asset_or_ab);
            DebugStr("Detected file type: " + file_type.ToString());
            switch(file_type)
            {
                case DetectedFileType.BundleFile:
                    HandleBundle(asset_or_ab, png);
                    break;
                case DetectedFileType.AssetsFile:
                    HandleAsset(asset_or_ab, png);
                    break;
                case DetectedFileType.Unknown:
                    DisplayStr("Invalid file type for " + asset_or_ab + ": " + file_type.ToString());
                    break;
            }
        }

        private static void SaveAssetBundle(
            AssetTypeValueField id, AssetFileInfoEx afie,
            AssetsFileInstance assetInst, BundleFileInstance bundleInst,
            string ab, string assetfile_name, string png_noext)
        {
            // buffer
            var newGoBytes = id.WriteToByteArray();
            var repl = new AssetsReplacerFromMemory(0, afie.index, (int)afie.curFileType, 0xffff, newGoBytes);

            if (repl == null || repl.ToString().Length == 0)
            {
                DisplayStr("The asset replacer was null for " + ab + " (Asset: " + assetfile_name + ", Texture: " + png_noext + ")");
                return;
            }

            /*
            // DO NOT write assetInst twice (there's currently a bug)
            using (var stream = File.OpenWrite("resources-modified.assets"))
            using (var writer = new AssetsFileWriter(stream))
            {
                assetInst.file.Write(writer, 0, new List<AssetsReplacer>() { repl }, 0);
            }
            */

            DebugStr("Write changes to memory");

            //write changes to memory
            byte[] newAssetData;
            using (var stream = new MemoryStream())
            using (var writer = new AssetsFileWriter(stream))
            {
                assetInst.file.Write(writer, 0, new List<AssetsReplacer>() { repl }, 0);
                newAssetData = stream.ToArray();
            }

            if (newAssetData == null)
            {
                DisplayStr("The new asset data was null for " + ab + " (Asset: " + assetfile_name + ", Texture: " + png_noext + ")");
                return;
            }

            //rename this asset name from boring to cool when saving
            var bunRepl = new BundleReplacerFromMemory(assetfile_name, null, true, newAssetData, -1);

            if (bunRepl == null)
            {
                DisplayStr("The bundle replacer was null for " + ab + " (Asset: " + assetfile_name + ", Texture: " + png_noext + ")");
                return;
            }

            string ab_fake_name = ab + "_temp";
            // Save "fake" file since it's going to be compressed later
            var bunWriter = new AssetsFileWriter(File.OpenWrite(ab_fake_name));
            bundleInst.file.Write(bunWriter, new List<BundleReplacer>() { bunRepl });
            bunWriter.Close();
        }

        private static AssetsFileInstance GetAssetInst(AssetsManager am, BundleFileInstance bundleInst, string assetfile_name, string ab)
        {
            // Load from index instead of name for now
            AssetsFileInstance assetInst = am.LoadAssetsFileFromBundle(bundleInst, 0, true);
            if (assetInst == null)
            {
                DisplayStr("Could not load asset file for " + assetfile_name + " in " + ab);
            }
            else
            {
                DebugStr("Loaded assetInst for " + assetfile_name);
            }
            return assetInst;
        }

        private static string GetRightAssetFileNameFromBundle(BundleFileInstance bundleInst, string ab)
        {
            string assetfile_name = "";
            int cont = 0;
            foreach (var i in bundleInst.file.bundleInf6.dirInf)
            {
                DebugStr("Found asset file: " + i.name);
                // Not .resS
                if (!i.name.Contains(".resS"))
                {
                    assetfile_name = i.name;
                }
                cont++;
            }

            if (cont > 2)
            {
                DisplayStr("More than 2 assets file found in " + ab + " (UNIMPLEMENTED)!");
                return string.Empty;
            }
            return assetfile_name;
        }

        private static BundleFileInstance GetBundleInst(AssetsManager am, string ab)
        {
            BundleFileInstance bundleInst = am.LoadBundleFile(ab, true);
            if (bundleInst == null)
            {
                DisplayStr("Could not load bundle file for " + ab);
            }
            return bundleInst;
        }

        private static void PackLZ4Bundle(string real_name)
        {
            // Pack using LZ4

            string fake_name = real_name + "_temp";

            if (!File.Exists(fake_name))
            {
                return;
            }

            AssetsManager am = new AssetsManager();
            // Open temp file
            var bun = am.LoadBundleFile(fake_name);
            using (var stream = File.OpenWrite(real_name))
            using (var writer = new AssetsFileWriter(stream))
            {
                // Save packed "real" file
                bun.file.Pack(bun.file.reader, writer, AssetBundleCompressionType.LZ4);
            }
            am.UnloadAllBundleFiles();

            // Delete temp file

            File.Delete(fake_name);
        }

        private static void DecompressToMemory(BundleFileInstance bundleInst)
        {
            AssetBundleFile bundle = bundleInst.file;

            MemoryStream bundleStream = new MemoryStream();
            bundle.Unpack(bundle.reader, new AssetsFileWriter(bundleStream));

            bundleStream.Position = 0;

            AssetBundleFile newBundle = new AssetBundleFile();
            newBundle.Read(new AssetsFileReader(bundleStream), false);

            bundle.reader.Close();
            bundleInst.file = newBundle;
        }

        private static bool ImportTexturesCustom(AssetTypeValueField atvf, string png, out AssetTypeValueField id, int format)
        {
            const bool dxt_mitm = false;

            TextureFormat fmt = dxt_mitm ? TextureFormat.DXT5 : (TextureFormat)format;

            int og_width = atvf.Get("m_Width").GetValue().AsInt();
            int og_height = atvf.Get("m_Height").GetValue().AsInt();

            // Try to import a .png (of the selected textureformat) from selectedFilePath
            // After doing that, save two new variables as width and height of the image
            byte[] encImageBytes = TextureImportExport.ImportPng(png, fmt, true, og_width, og_height, out int width, out int height);

            if (encImageBytes == null)
            {
                id = null;
                return false;
            }

            // Load from byte array
            AssetTypeValueField m_StreamData = atvf.Get("m_StreamData");
            m_StreamData.Get("offset").GetValue().Set(0);
            m_StreamData.Get("size").GetValue().Set(0);
            m_StreamData.Get("path").GetValue().Set("");

            if (!atvf.Get("m_MipCount").IsDummy())
                atvf.Get("m_MipCount").GetValue().Set(1);

            // Set texture format to desired format
            atvf.Get("m_TextureFormat").GetValue().Set((int)fmt);

            // Set to the byte array length
            atvf.Get("m_CompleteImageSize").GetValue().Set(encImageBytes.Length);

            // Set image width
            atvf.Get("m_Width").GetValue().Set(width);

            // Set image height
            atvf.Get("m_Height").GetValue().Set(height);

            // Read the field "image data"
            AssetTypeValueField image_data = atvf.Get("image data");

            // Set type to byte array
            image_data.GetValue().type = EnumValueTypes.ByteArray;
            image_data.templateField.valueType = EnumValueTypes.ByteArray;

            // Create new array with the red data
            AssetTypeByteArray byteArray = new AssetTypeByteArray()
            {
                size = (uint)encImageBytes.Length,
                data = encImageBytes
            };

            // Actually set the byte array
            image_data.GetValue().Set(byteArray);
            id = atvf;
            return true;
        }
    }
}
