using AssetsTools.NET.Extra;
using AssetsTools.NET;
using System;
using System.IO;

namespace UAFGJ
{
	partial class Program
	{
		static private bool FindTXTFile(
	string input_file,
	ref AssetsFileInstance assetInst, ref AssetFileInfo afie, ref AssetTypeValueField atvf, ref AssetsManager am,
	string asset, string assetfile_name
	)
		{
			if (Path.GetExtension(input_file) != ".txt")
			{
				Console.WriteLine("Unsupported extension: " + Path.GetExtension(input_file));
				return false;
			}

			int cont = 0;
			int selected = -1;

			string file_noext = Path.GetFileNameWithoutExtension(input_file);
			file_noext = file_noext.ToLowerInvariant();

			// Iterate the files in assetInst
			foreach (var inf in assetInst.file.GetAssetsOfType((int)AssetClassID.MonoBehaviour))
			{
				afie = inf;
				atvf = am.GetBaseField(assetInst, afie);

				if (atvf == null)
				{
					DisplayStr("ATVF is currently null at position " + cont + "!");
				}

				var name = atvf["m_Name"].AsString;
				DebugStr(name);

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
				return false;
			}
			else
			{
				DisplayStr("Found equivalent: " + selected + ": " + atvf["m_Name"].AsString + ", path ID: " + afie.PathId);
			}

			bool ret = ImportMonoBehaviourCustom(input_file, am, afie, assetInst, asset);
			if (!ret)
			{
				DisplayStr("Couldn't replace MonoBehaviour!");
			}

			return true;
		}

		static private void FindPNGFile(
			string input_file,
			ref AssetFileInfo afie, ref AssetsFileInstance assetInst, ref AssetTypeValueField atvf, ref AssetsManager am,
			string asset, string assetfile_name)
		{
			int _format = 0;
			int _selected = -1;
			int cont = 0;

			string file_noext = Path.GetFileNameWithoutExtension(input_file);
			file_noext = file_noext.ToLowerInvariant();
			// Iterate the Texture2D files in assetInst
			foreach (var inf in assetInst.file.GetAssetsOfType((int)AssetClassID.Texture2D))
			{
				afie = inf;
				atvf = am.GetBaseField(assetInst, afie);

				var name = atvf["m_Name"].AsString;
				_format = atvf["m_TextureFormat"].AsInt;
				DebugStr(name);

				// Is it the right file?
				if (name.ToLowerInvariant() == file_noext)
				{
					_selected = cont;
					break;
				}
				cont++;
			}

			if (_selected == -1 || cont > _selected)
			{
				// Selected "png" to replace not found
				DisplayStr("Couldn't find equivalent image for " + asset + " (Asset: " + assetfile_name + ", InputFile: " + file_noext + ")");
			}
			else
			{
				DisplayStr("Found equivalent image for " + asset + "( Asset: " + assetfile_name + ", InputFile: " + file_noext + "): " + _selected + " / pathID " + afie.PathId);
			}

			bool ret = ImportTexturesCustom(ref atvf, input_file, _format);
			if (atvf == null || !ret)
			{
				DisplayStr("Could not set image for " + asset + " (Asset: " + assetfile_name + ", InputFile: " + file_noext + ")");
			}
			else
			{
				DisplayStr("Successfully set image for " + asset + " (Asset: " + assetfile_name + ", InputFile: " + file_noext + ")");
			}
		}
	}
}