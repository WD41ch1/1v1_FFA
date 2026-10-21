using UnityEngine;
using System.Collections;
using System.IO;
using UnityEditor;
using System.Xml.Serialization;
using NPOI.HSSF.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.SS.UserModel;

public class ItemMasterData_importer : AssetPostprocessor {
	private static readonly string filePath = "Assets/WorkSpace/shishido/ExcelData/Item/ItemMasterData.xlsx";
	private static readonly string exportPath = "Assets/WorkSpace/shishido/ExcelData/Item/ItemMasterData.asset";
	private static readonly string[] sheetNames = { "Items", };
	
	static void OnPostprocessAllAssets (string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
	{
		foreach (string asset in importedAssets) {
			if (!filePath.Equals (asset))
				continue;
				
			Entity_Items data = (Entity_Items)AssetDatabase.LoadAssetAtPath (exportPath, typeof(Entity_Items));
			if (data == null) {
				data = ScriptableObject.CreateInstance<Entity_Items> ();
				AssetDatabase.CreateAsset ((ScriptableObject)data, exportPath);
				data.hideFlags = HideFlags.NotEditable;
			}
			
			data.sheets.Clear ();
			using (FileStream stream = File.Open (filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
				IWorkbook book = null;
				if (Path.GetExtension (filePath) == ".xls") {
					book = new HSSFWorkbook(stream);
				} else {
					book = new XSSFWorkbook(stream);
				}
				
				foreach(string sheetName in sheetNames) {
					ISheet sheet = book.GetSheet(sheetName);
					if( sheet == null ) {
						Debug.LogError("[QuestData] sheet not found:" + sheetName);
						continue;
					}

					Entity_Items.Sheet s = new Entity_Items.Sheet ();
					s.name = sheetName;
				
					for (int i=1; i<= sheet.LastRowNum; i++) {
						IRow row = sheet.GetRow (i);
						ICell cell = null;
						
						Entity_Items.Param p = new Entity_Items.Param ();
						
					cell = row.GetCell(0); p.itemId = (int)(cell == null ? 0 : cell.NumericCellValue);
					cell = row.GetCell(1); p.dataType = (int)(cell == null ? 0 : cell.NumericCellValue);
					cell = row.GetCell(3); p.itemName = (cell == null ? "" : cell.StringCellValue);
					cell = row.GetCell(4); p.itemType = (int)(cell == null ? 0 : cell.NumericCellValue);
					cell = row.GetCell(6); p.maxStack = (int)(cell == null ? 0 : cell.NumericCellValue);
						s.list.Add (p);
					}
					data.sheets.Add(s);
				}
			}

			ScriptableObject obj = AssetDatabase.LoadAssetAtPath (exportPath, typeof(ScriptableObject)) as ScriptableObject;
			EditorUtility.SetDirty (obj);
		}
	}
}
