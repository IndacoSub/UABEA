using AssetsTools.NET.Extra;
using AssetsTools.NET;
using System.Collections.Generic;
using System.IO;
using UABEAvalonia;
using System;

namespace UAFGJ
{
	partial class Program
	{

		static private bool ImportMonoBehaviourCustom(string input_file, AssetsManager am, AssetFileInfo afie, AssetsFileInstance assetInst, string assetname)
		{
			AssetContainer ac = new AssetContainer(afie, assetInst);
			string fake_name = assetname + "_temp";

			using (FileStream fs = File.OpenRead(input_file))
			{
				using (StreamReader sr = new StreamReader(fs))
				{
					AssetImportExport importer = new AssetImportExport();
					byte[]? bytes = importer.ImportTextAsset(sr, out string ex);

					if (bytes == null)
					{
						DisplayStr("Parse error: Something went wrong when reading the dump file.");
						return false;
					}

					AssetsReplacer replacer = AssetImportExport.CreateAssetReplacer(ac, bytes);
					using (var stream = File.OpenWrite(fake_name))
					{
						using (var writer = new AssetsFileWriter(stream))
						{
							assetInst.file.Write(writer, 0, new List<AssetsReplacer>() { replacer });
						}
					}
				}
			}

			am.UnloadAllAssetsFiles(true);
			File.Move(fake_name, assetname, true);

			return true;
		}

		public byte[]? ImportTextAsset(StreamReader sr, out string? exceptionMessage)
		{
			this.sr = sr;
			using (MemoryStream ms = new MemoryStream())
			{
				aw = new AssetsFileWriter(ms);
				aw.BigEndian = false;
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
	}
}