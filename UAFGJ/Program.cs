using System;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using UABEAvalonia;

namespace UAFGJ
{
	partial class Program
	{
		static void Main(string[] args)
		{
			try
			{
				if (args == null)
				{
					DisplayStr("Null args!");
					return;
				}

				if (args.Length < 2)
				{
					DisplayStr("Usage: UAFGJ.exe <bundle/assets> <input.txt/png> [pathId]");
					return;
				}

				string assetOrBundle = args[0].Replace('\\', '/');
				string inputFile = args[1].Replace('\\', '/');
				string pathId = args.Length >= 3 ? args[2].Trim() : string.Empty;

				if (!File.Exists(assetOrBundle))
				{
					DisplayStr("Input bundle/assets file not found: " + assetOrBundle);
					return;
				}

				if (!File.Exists(inputFile))
				{
					DisplayStr("Input replacement file not found: " + inputFile);
					return;
				}

				DisplayStr("File exists: " + assetOrBundle);
				DisplayStr("Replacement exists: " + inputFile);
				if (!string.IsNullOrWhiteSpace(pathId))
					DisplayStr("Requested path ID: " + pathId);

				DoStuff(assetOrBundle, inputFile, pathId);
			}
			catch (Exception ex)
			{
				DisplayStr("[FATAL] " + ex.GetType().Name + ": " + ex.Message);
				DebugStr(ex.ToString());
				Environment.ExitCode = 1;
			}
		}

		static private void DoStuff(string assetOrBundle, string inputFile, string specificPathId)
		{
			DebugStr("Opening file: " + assetOrBundle);
			DetectedFileType fileType = FileTypeDetector.DetectFileType(assetOrBundle);
			DebugStr("Detected file type: " + fileType);

			switch (fileType)
			{
				case DetectedFileType.BundleFile:
					HandleBundle(assetOrBundle, inputFile, specificPathId);
					break;

				case DetectedFileType.AssetsFile:
					HandleAsset(assetOrBundle, inputFile, specificPathId);
					break;

				default:
					DisplayStr("Invalid file type for " + assetOrBundle + ": " + fileType);
					break;
			}
		}
	}
}
