using AssetsTools.NET.Extra;
using AssetsTools.NET;
using System.Collections.Generic;
using System;
using System.IO;
using Avalonia.Styling;
using AssetsTools.NET.Texture;

namespace UAFGJ
{
	partial class Program
	{
		static private void HandleAsset(string asset, string input_file)
		{
			AssetsManager am = new AssetsManager();
			AssetsFileInstance assetInst = am.LoadAssetsFile(asset, true);

			ClassPackageFile meta_class = am.LoadClassPackage("classdata.tpk");
			ClassDatabaseFile meta_db = null;
			if (!assetInst.file.Metadata.TypeTreeEnabled)
			{
				meta_db = am.LoadClassDatabaseFromPackage(assetInst.file.Metadata.UnityVersion);
			}
			DebugStr("Loaded classdata.tpk");

			AssetTypeValueField atvf = new AssetTypeValueField(); // "baseField"
			AssetFileInfo afie = new AssetFileInfo();

			string assetfile_name = assetInst.name;

			if(Path.GetExtension(input_file) != ".png")
			{
				// Assume .txt

				if (!FindTXTFile(input_file, ref assetInst, ref afie, ref atvf, ref am, asset, assetfile_name))
				{
					DisplayStr("Failed to replace TXT!");
				}
				return;
			}

			// PNG

			FindPNGFile(input_file, ref afie, ref assetInst, ref atvf, ref am, asset, assetfile_name);

			if (afie == null)
			{
				DisplayStr("AFIE is null!");
				return;
			}

			if (atvf == null)
			{
				DisplayStr("ID is null!");
				return;
			}

			/* Save the asset file */

			// buffer
			var newGoBytes = atvf.WriteToByteArray();

			if (newGoBytes == null || newGoBytes.Length == 0)
			{
				DisplayStr("Null/invalid buffer!");
				return;
			}

			var repl = new AssetsReplacerFromMemory(afie.PathId, (int)afie.TypeId, 0xFFFF, newGoBytes);

			if (repl == null || repl.ToString().Length == 0)
			{
				DisplayStr("The asset replacer was null for " + asset + " (Asset: " + assetfile_name + ", InputFile: " + input_file + ")");
				return;
			}

			// Don't use the same name, because UABEA doesn't support overwriting files(?)
			string fake_name = asset + "_temp";
			using (var stream = File.OpenWrite(fake_name))
			{
				using (var writer = new AssetsFileWriter(stream))
				{
					assetInst.file.Write(writer, 0, new List<AssetsReplacer>() { repl });
				}
			}

			string real_name = asset;

			// Unload everything
			if (!am.UnloadAllAssetsFiles(true))
			{
				DisplayStr("Could not unload all assets!");
			}
			File.Move(fake_name, real_name, true);

			DisplayStr("Successfully replaced asset!");
		}
	}
}