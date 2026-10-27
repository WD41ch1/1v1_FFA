using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Entity_Weapons : ScriptableObject
{	
	public List<Sheet> sheets = new List<Sheet> ();

	[System.SerializableAttribute]
	public class Sheet
	{
		public string name = string.Empty;
		public List<Param> list = new List<Param>();
	}

	[System.SerializableAttribute]
	public class Param
	{
		
		public int itemId;
		public float weaponDamage;
		public int FireMode;
		public float fireRate;
		public float reloadTime;
		public int ammoType;
		public int magazineSize;
		public float ammoSpeed;
		public float fireAccuracy;
		public float recoilValue;
		public float adsSpeed;
		public float adsMultiplier;
		public float distanceAttenuation;
		public float headMultiplier;
		public float bodyMultiplier;
		public float legMultiplier;
	}
}

