using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using Avalonia;
using TexturePlugin;
using UABEAvalonia;

namespace UAFGJ
{
	partial class Program
	{
		StreamReader sr;
		AssetsFileWriter aw;

		static void Main(string[] args)
		{
			if(args == null)
			{
				DisplayStr("Null args!");
				return;
			}

			if (args.Length < 2)
			{
				DisplayStr("Not enough arguments!");
				return;
			}

			string asset_or_ab = args[0].Replace('\\', '/');
			string input_file = args[1].Replace('\\', '/');
			string pathid = args.Length >= 3 ? args[2] : "";
			pathid = pathid.Replace("\\", "/");

			if (!File.Exists(asset_or_ab))
			{
				DisplayStr(".asset/.ab/.txt file not found: " + asset_or_ab + "!");
				return;
			}
			else
			{
				DisplayStr("File exists: " + asset_or_ab);
			}

			if (!File.Exists(input_file))
			{
				DisplayStr(".png/.txt file not found!");
				return;
			} else
			{
				DisplayStr("File exists: " + input_file);
			}

			DoStuff(asset_or_ab, input_file, pathid);
		}

		static private void DoStuff(string asset_or_ab, string png, string specific_pathid)
		{
			DebugStr("Opening file: " + asset_or_ab);
			DetectedFileType file_type = FileTypeDetector.DetectFileType(asset_or_ab);
			DebugStr("Detected file type: " + file_type.ToString());
			switch (file_type)
			{
				case DetectedFileType.BundleFile:
					HandleBundle(asset_or_ab, png, specific_pathid);
					break;
				case DetectedFileType.AssetsFile:
					// .TXT support too
					HandleAsset(asset_or_ab, png, specific_pathid);
					break;
				case DetectedFileType.Unknown:
					DisplayStr("Invalid file type for " + asset_or_ab + ": " + file_type.ToString());
					break;
			}
		}
	}
}
