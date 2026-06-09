using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemPanel : MonoBehaviour {
	public GameObject slotObject;
	public Sprite LockedItem;
	public GameObject ItemsPanel;
	private List<ButtonsMenu> menuGroup = new List<ButtonsMenu>();
	void InitializationMenu(int Slot, ButtonsMenu buttonsMenu)
	{
		int slotNumber = Slot;
		if (buttonsMenu != null) {
			var listOfItemButtons = new List<PalletButtonData> ();
			var items = Saves.SaveSystem.GetPlayerData ().items.itemLevels;
			for (int i = 0; i < items.Count; i++) {
				if (items [i] <= 0)
					continue;
				int number = i;
				var t = new PalletButtonData ("", SO_Configuraciones.obj.Items[i].Ico[items[i]], () => Select (slotNumber,number));
				t.openTarget = SO_Configuraciones.obj.Items[i];
				t.OpenCondition = (x => ((SO_AbstractItem)x).IsSelected()==false);
				listOfItemButtons.Add (t);
			}
			var nullSelect = new PalletButtonData ("", LockedItem, () => Select (Slot,-1));
			listOfItemButtons.Add (nullSelect);
			int ItemIndexToSelect = Saves.SaveSystem.GetPlayerData ().items.GetItemSelected (Slot);
			int selectIndex = listOfItemButtons.Count - 1;
			if (ItemIndexToSelect>=0)
			{
				var itemToSelect = SO_Configuraciones.obj.Items [ItemIndexToSelect];
				selectIndex = listOfItemButtons.FindIndex (x => x.openTarget == itemToSelect);
			}

			buttonsMenu.Initialization (listOfItemButtons, selectIndex);
		}
	}
	// Use this for initialization
	void Start () {
		menuGroup = new List<ButtonsMenu> ();
		for (int i = 0; i < Saves.ItemsData.MaxItemsToChoise; i++) {
			var t = Instantiate (slotObject, transform.position, transform.rotation) as GameObject;
			var menu = t.GetComponent<ButtonsMenu> ();
			t.GetComponent<ItemDescripcionShower> ().slotToShow = i;
			menu.listObject = ItemsPanel;
			menuGroup.Add (menu);
			t.SetParentWithScaleOne (transform);
			menu.ShowLocked = false;
			menu.ShowSelected = true;
			InitializationMenu (i, menu);
			menu.SetGroup (menuGroup);
		}
	}

	void Select(int Slot, int index)
	{				
		Saves.SaveSystem.GetPlayerData ().items.SetItemSelected (Slot, index);
	}
}
