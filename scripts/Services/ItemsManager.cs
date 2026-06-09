using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemsManager{
	public static void GetRandomItem()
	{
		var items = SO_Configuraciones.obj.Items;
		var NonMaxItems = items.FindAll(x=>x.Level!=x.maxLevel);
		if (NonMaxItems.Count > 0) {
			int n = Random.Range (0, NonMaxItems.Count);
			Saves.SaveSystem.GetPlayerData ().items.UpLevel (NonMaxItems[n].GetItemIndex());
			var item = NonMaxItems[n];
			item.RefreshLevel ();
			PalletsCreator.CreatePallet_ItemOpen (item, item.Level);
		}
	}
}
