﻿using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Collections.Generic;
using System.IO;

namespace UABEAvalonia
{
    public partial class AssetsFileInfoWindow
    {
        private void FillScriptInfo()
        {
            if (cbxFiles.SelectedItem == null)
                return;

            AssetsFileInstance selectedFile = activeFile;
            if (selectedFile == null)
                return;

            List<string> items = new List<string>();
            List<AssetPPtr> scriptTypes = selectedFile.file.Metadata.ScriptTypes;
            for (int i = 0; i < scriptTypes.Count; i++)
            {
                AssetPPtr pptr = scriptTypes[i];
                AssetTypeValueField? scriptBf = workspace.GetBaseField(selectedFile, pptr.FileId, pptr.PathId);
                if (scriptBf == null)
                {
					if (pptr.FileId == 0)
					{
						items.Add($"{i} - {selectedFile.name}/{pptr.PathId}");
					}
					else
					{
						string fileName = selectedFile.file.Metadata.Externals[pptr.FileId - 1].PathName;
						items.Add($"{i} - {Path.GetFileName(fileName)}/{pptr.PathId}");
					}
                    continue;
				}

                string nameSpace = scriptBf["m_Namespace"].AsString;
                string className = scriptBf["m_ClassName"].AsString;

                string fullName;
                if (nameSpace != "")
                    fullName = $"{nameSpace}.{className}";
                else
                    fullName = className;

                items.Add($"{i} - {fullName}");
            }

            boxScriptInfoList.ItemsSource = items;
        }
    }
}
