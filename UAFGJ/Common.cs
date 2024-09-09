using AssetsTools.NET.Extra;
using AssetsTools.NET;
using System.IO;

namespace UAFGJ
{
	partial class Program
	{
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
			foreach (var i in bundleInst.file.BlockAndDirInfo.DirectoryInfos)
			{
				DebugStr("Found asset file: " + i.Name);
				if (i.Name.Contains(".resS"))
				{
					continue;
				}
				if(i.Name.EndsWith(".resource"))
				{
					continue;
				}
				DebugStr("Found good? asset file: " + i.Name);
				assetfile_name = i.Name;
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

		private static void DecompressToMemory(BundleFileInstance bundleInst)
		{
			AssetBundleFile bundle = bundleInst.file;

			MemoryStream bundleStream = new MemoryStream();
			bundle.Unpack(new AssetsFileWriter(bundleStream));

			bundleStream.Position = 0;

			AssetBundleFile newBundle = new AssetBundleFile();
			newBundle.Read(new AssetsFileReader(bundleStream));

			bundle.Reader.Close();
			bundleInst.file = newBundle;
		}
	}
}