using AssetsTools.NET.Extra;
using AssetsTools.NET;
using System.IO;
using System.Collections.Generic;

namespace UAFGJ
{
	partial class Program
	{
		static private void HandleBundle(string ab, string input_file, string specific_pathid)
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

			ClassPackageFile meta_class = am.LoadClassPackage("classdata.tpk");
			ClassDatabaseFile meta_db = null;
			if (!assetInst.file.Metadata.TypeTreeEnabled)
			{
				meta_db = am.LoadClassDatabaseFromPackage(assetInst.file.Metadata.UnityVersion);
			}
			DebugStr("Loaded classdata.tpk");

			AssetTypeValueField atvf = new AssetTypeValueField(); // "baseField"
			AssetFileInfo afie = new AssetFileInfo();
			int selected = -1;
			string inputfile_noext = input_file;
			int cont = 0;

			if (Path.GetExtension(input_file) != ".png")
			{
				// Assume .txt

				if (!FindTXTFile(input_file, ref assetInst, ref afie, ref atvf, ref am, ab, assetfile_name, specific_pathid))
				{
					DisplayStr("Failed to replace TXT!");
					return;
				}
			}
			else
			{

				int format = 0;

				// Iterate the files in assetInst
				foreach (var inf in assetInst.file.GetAssetsOfType((int)AssetClassID.Texture2D))
				{
					afie = inf;
					atvf = am.GetBaseField(assetInst, afie);
					var name = atvf["m_Name"].AsString;
					format = atvf["m_TextureFormat"].AsInt;
					DebugStr(name);

					inputfile_noext = Path.GetFileNameWithoutExtension(input_file);
					inputfile_noext = inputfile_noext.Trim().ToLowerInvariant();
					// Is it the right file?
					if (name.Trim().ToLowerInvariant() == inputfile_noext)
					{
						DebugStr("Found potential candidate: " + name + ", pid: " + inf.PathId);
						if (specific_pathid == "" || long.Parse(specific_pathid) == inf.PathId)
						{
							selected = cont;
							break;
						}
					}
					cont++;
				}

				// Selected "png" to replace not found
				if (selected == -1)
				{
					DisplayStr("Couldn't find equivalent image for " + ab + " (Asset: " + assetfile_name + ", Texture: " + inputfile_noext + ")");
					return;
				}

				// Import textures (id is basically atvf but changed)
				bool ret = ImportTexturesCustom(ref atvf, input_file, format);
				if (atvf == null || !ret)
				{
					DisplayStr("Could not set image for " + ab + " (Asset: " + assetfile_name + ", Texture: " + inputfile_noext + ")");
					return;
				}
			}

			SaveAssetBundle(atvf, afie, assetInst, bundleInst, ab, assetfile_name, inputfile_noext);

			// Unload everything
			if (!am.UnloadAllAssetsFiles(true))
			{
				DisplayStr("Could not unload all asset files!");
			}
			if (!am.UnloadAllBundleFiles())
			{
				DisplayStr("Could not unload all bundle files!");
			}

			string ab_real_name = ab;
			PackLZ4Bundle(ab_real_name);

			DisplayStr("Done!");
		}

		private static void SaveAssetBundle(
			AssetTypeValueField id, AssetFileInfo afie,
			AssetsFileInstance assetInst, BundleFileInstance bundleInst,
			string ab, string assetfile_name, string png_noext)
		{
			// buffer
			var newGoBytes = id.WriteToByteArray();
			if (newGoBytes == null)
			{
				DisplayStr("The buffer is null!");
				return;
			}

			var repl = new AssetsReplacerFromMemory(afie.PathId, (int)afie.TypeId, 0xffff, newGoBytes);

			if (repl == null || repl.ToString().Length == 0)
			{
				DisplayStr("The asset replacer was null for " + ab + " (Asset: " + assetfile_name + ", Texture: " + png_noext + ")");
				return;
			}

			DebugStr("Writing changes to memory");

			//write changes to memory
			byte[] newAssetData;
			using (var stream = new MemoryStream())
			{
				using (var writer = new AssetsFileWriter(stream))
				{
					assetInst.file.Write(writer, 0, new List<AssetsReplacer>() { repl });
					newAssetData = stream.ToArray();
				}
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
			if (bun == null)
			{
				DisplayStr("Loaded bundle file is null!");
				return;
			}
			using (var stream = File.OpenWrite(real_name))
			{
				using (var writer = new AssetsFileWriter(stream))
				{
					// Save packed "real" file
					bun.file.Pack(bun.file.Reader, writer, AssetBundleCompressionType.LZ4);
				}
			}
			if (!am.UnloadAllBundleFiles())
			{
				DisplayStr("Could not unload all bundle files!");
			}

			// Delete temp file

			File.Delete(fake_name);
		}
	}
}