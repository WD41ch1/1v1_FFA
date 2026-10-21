using UnityEngine;
using System.Collections;
using System.IO;
using UnityEditor;
using System.Xml.Serialization;
using NPOI.HSSF.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.SS.UserModel;

public class WeaponsData_importer : AssetPostprocessor {
	private static readonly string filePath = "Assets/WorkSpace/shishido/ExcelData/Item/WeaponsData.xlsx";
	private static readonly string exportPath = "Assets/WorkSpace/shishido/ExcelData/Item/WeaponsData.asset";
	private static readonly string[] sheetNames = { "Weapons", };
	
	static void OnPostprocessAllAssets (string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
	{
		foreach (string asset in importedAssets) {
			if (!filePath.Equals (asset))
				continue;
				
			Entity_Weapons data = (Entity_Weapons)AssetDatabase.LoadAssetAtPath (exportPath, typeof(Entity_Weapons));
			if (data == null) {
				data = ScriptableObject.CreateInstance<Entity_Weapons> ();
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

					Entity_Weapons.Sheet s = new Entity_Weapons.Sheet ();
					s.name = sheetName;
				
					for (int i=1; i<= sheet.LastRowNum; i++) {
						IRow row = sheet.GetRow (i);
						ICell cell = null;
						
						Entity_Weapons.Param p = new Entity_Weapons.Param ();
						
					cell = row.GetCell(0); p.itemId = (int)(cell == null ? 0 : cell.NumericCellValue);
					cell = row.GetCell(1); p.weaponDamage = (float)(cell == null ? 0 : cell.NumericCellValue);
					cell = row.GetCell(2); p.FireMode = (int)(cell == null ? 0 : cell.NumericCellValue);
					cell = row.GetCell(4); p.fireRate = (float)(cell == null ? 0 : cell.NumericCellValue);
					cell = row.GetCell(5); p.reloadTime = (float)(cell == null ? 0 : cell.NumericCellValue);
					cell = row.GetCell(6); p.ammoType = (int)(cell == null ? 0 : cell.NumericCellValue);
					cell = row.GetCell(8); p.magazineSize = (int)(cell == null ? 0 : cell.NumericCellValue);
					cell = row.GetCell(9); p.ammoSpeed = (float)(cell == null ? 0 : cell.NumericCellValue);
					cell = row.GetCell(10); p.fireAccuracy = (float)(cell == null ? 0 : cell.NumericCellValue);
					cell = row.GetCell(11); p.recoilValue = (float)(cell == null ? 0 : cell.NumericCellValue);
					cell = row.GetCell(12); p.adsSpeed = (float)(cell == null ? 0 : cell.NumericCellValue);
					cell = row.GetCell(13); p.adsMultiplier = (float)(cell == null ? 0 : cell.NumericCellValue);
					cell = row.GetCell(14); p.distanceAttenuation = (float)(cell == null ? 0 : cell.NumericCellValue);
					cell = row.GetCell(15); p.headMultiplier = (float)(cell == null ? 0 : cell.NumericCellValue);
					cell = row.GetCell(16); p.bodyMultiplier = (float)(cell == null ? 0 : cell.NumericCellValue);
					cell = row.GetCell(17); p.legMultiplier = (float)(cell == null ? 0 : cell.NumericCellValue);
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
