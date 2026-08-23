using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.IO;

namespace UAFGJ
{
	partial class Program
	{
		private static bool ImportMonoBehaviourCustom(
			string inputFile,
			AssetsManager am,
			AssetFileInfo afie,
			AssetsFileInstance assetInst,
			string assetName,
			out AssetsTools.NET.AssetTypeValueField modifiedBaseField,
			out byte[] replacementData,
			out byte[] originalSerializedData)
		{
			modifiedBaseField = null;
			replacementData = Array.Empty<byte>();
			originalSerializedData = Array.Empty<byte>();

			if (assetInst == null)
				throw new InvalidOperationException("assetInst is null.");
			if (afie == null)
				throw new InvalidOperationException("AssetFileInfo is null.");
			if (!File.Exists(inputFile))
				throw new FileNotFoundException("TXT input not found.", inputFile);

			DebugStr($"[TXT] Importing MonoBehaviour '{assetName}' from dump '{inputFile}'");
			DebugStr("[TXT] Cloning the source base field before any mutation...");

			var baseField = am.GetBaseField(assetInst, afie);
			if (baseField == null || baseField.IsDummy)
				throw new InvalidDataException("AssetsTools.NET returned a null/dummy base field.");

			originalSerializedData = baseField.WriteToByteArray();
			DebugStr($"[CHECK] Original target base field serialized size={originalSerializedData.Length} SHA256={Sha256Hex(originalSerializedData)}");

			// Apply only the scalar values represented by the dump. We intentionally keep
			// the original AssetsTools.NET.AssetTypeValueField tree and let AssetsTools.NET serialize it.
			ApplyTextDumpToBaseField(inputFile, baseField);

			replacementData = baseField.WriteToByteArray();
			DebugStr($"[CHECK] Modified target base field serialized size={replacementData.Length} SHA256={Sha256Hex(replacementData)}");

			modifiedBaseField = baseField;
			return true;
		}
	}
}
