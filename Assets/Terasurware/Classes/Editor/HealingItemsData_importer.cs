using UnityEngine;
using System.Collections;
using System.IO;
using UnityEditor;
using System.Xml.Serialization;
using NPOI.HSSF.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.SS.UserModel;

public class HealingItemsData_importer : AssetPostprocessor {
	private static readonly string filePath = "Assets/WorkSpace/shishido/ExcelData/Item/HealingItemsData.xlsx";
	private static readonly string exportPath = "Assets/WorkSpace/shishido/ExcelData/Item/HealingItemsData.asset";
	private static readonly string[] sheetNames = { "HealingItems", };
	
	static void OnPostprocessAllAssets (string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
	{
		foreach (string asset in importedAssets) {
			if (!filePath.Equals (asset))
				continue;
				
			Entity_HealingItems data = (Entity_HealingItems)AssetDatabase.LoadAssetAtPath (exportPath, typeof(Entity_HealingItems));
			if (data == null) {
				data = ScriptableObject.CreateInstance<Entity_HealingItems> ();
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

					Entity_HealingItems.Sheet s = new Entity_HealingItems.Sheet ();
					s.name = sheetName;
				
					for (int i=1; i<= sheet.LastRowNum; i++) {
						IRow row = sheet.GetRow (i);
						ICell cell = null;
						
						Entity_HealingItems.Param p = new Entity_HealingItems.Param ();
						
					cell = row.GetCell(0); p.itemId = (int)(cell == null ? 0 : cell.NumericCellValue);
					cell = row.GetCell(1); p.healingValue = (float)(cell == null ? 0 : cell.NumericCellValue);
					cell = row.GetCell(2); p.maxHealingAmount = (float)(cell == null ? 0 : cell.NumericCellValue);
					cell = row.GetCell(3); p.healingTiem = (float)(cell == null ? 0 : cell.NumericCellValue);
					cell = row.GetCell(4); p.healingCoolTiem = (float)(cell == null ? 0 : cell.NumericCellValue);
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
