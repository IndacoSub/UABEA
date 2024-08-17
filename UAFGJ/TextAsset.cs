using AssetsTools.NET.Extra;
using AssetsTools.NET;
using TextAssetPlugin;
using UABEAvalonia;
using System.IO;
using System.Collections.Generic;

namespace UAFGJ
{
	partial class Program
	{
		static private bool ImportTextAssetCustom(string input_file, AssetsManager am, AssetFileInfo afie, AssetsFileInstance assetInst, string assetname)
		{
			string fake_name = assetname + "_temp";
			AssetContainer cont = new AssetContainer(afie, assetInst);
			var baseField = am.GetBaseField(assetInst, afie);
			byte[] byteData = File.ReadAllBytes(input_file);
			baseField["m_Name"].AsString = Path.GetFileNameWithoutExtension(input_file);
			baseField["m_Script"].AsByteArray = byteData;

			byte[] savedAsset = baseField.WriteToByteArray();

			var replacer = new AssetsReplacerFromMemory(cont.PathId, cont.ClassId, cont.MonoId, savedAsset);

			using (var stream = File.OpenWrite(fake_name))
			{
				using (var writer = new AssetsFileWriter(stream))
				{
					assetInst.file.Write(writer, 0, new List<AssetsReplacer>() { replacer });
				}
			}

			am.UnloadAllAssetsFiles(true);
			File.Move(fake_name, assetname, true);

			return true;
		}
	}
}