using AssetsTools.NET.Texture;
using AssetsTools.NET;
using System.IO;
using TexturePlugin;
using System.Collections.Generic;
using UABEAvalonia;
using AssetsTools.NET.Extra;

namespace UAFGJ
{
	partial class Program
	{
		private static bool ImportTexturesCustom(ref AssetTypeValueField atvf, string png, int format)
		{
			const bool dxt_mitm = false;
			const bool do_test_dump = false;

			TextureFormat fmt = dxt_mitm ? TextureFormat.DXT5 : (TextureFormat)format;

			int og_width = atvf["m_Width"].AsInt;
			int og_height = atvf["m_Height"].AsInt;

			// Don't resize fonts
			bool should_resize = !png.Contains("FOT") && !png.Contains("HOT") && !png.Contains("Atlas");

			// Try to import a .png (of the selected textureformat) from selectedFilePath
			// After doing that, save two new variables as width and height of the image
			byte[] encImageBytes = TextureImportExport.ImportNX(png, fmt, should_resize, og_width, og_height, out int width, out int height);

			if (encImageBytes == null || encImageBytes.Length <= 0)
			{
				DisplayStr("New image is null!");
				return false;
			}

			if (do_test_dump)
			{
				using (StreamWriter sw = new StreamWriter(new FileStream("test_dump.png", FileMode.OpenOrCreate)))
				{
					sw.BaseStream.Write(encImageBytes, 0, encImageBytes.Length);
					sw.Dispose();
				};
			}

			// Load from byte array
			AssetTypeValueField m_StreamData = atvf["m_StreamData"];
			m_StreamData["offset"].AsInt = 0;
			m_StreamData["size"].AsInt = 0;
			m_StreamData["path"].AsString = "";

			if (!atvf["m_MipCount"].IsDummy)
			{
				atvf["m_MipCount"].AsInt = 1;
			}

			// Set texture format to desired format
			atvf["m_TextureFormat"].AsInt = ((int)fmt);

			// Set to the byte array length
			atvf["m_CompleteImageSize"].AsInt = encImageBytes.Length;

			// Set image width
			atvf["m_Width"].AsInt = width;

			// Set image height
			atvf["m_Height"].AsInt = height;

			atvf["image data"].Value.ValueType = AssetValueType.ByteArray; ;
			atvf["image data"].TemplateField.ValueType = AssetValueType.ByteArray;
			atvf["image data"].Value.AsByteArray = encImageBytes;

			DisplayStr("Successfully set image data");

			return true;
		}
	}
}