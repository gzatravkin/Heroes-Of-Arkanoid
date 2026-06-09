using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Saves
{
	[System.Serializable]
	public class ItemsData
	{
		public static readonly int MaxItemsToChoise = 3;
		public List<int> itemLevels = new List<int>();
		public List<int> ItemsChoised = new List<int>(0);
		public int GetLevel (int itemIndex)
		{	
			if (itemIndex < 0)
				return 0;
			if (itemIndex >= itemLevels.Count)
				return 0;
			else
				return itemLevels [itemIndex];
		}
		public void SetLevel (int itemIndex, int Level)
		{				
			for (int i = 0; itemLevels.Count<=itemIndex; i++)
				itemLevels.Add (0);
			itemLevels [itemIndex] = Level;
			GlobalEvents.SkillsChanged.Invoke();
		}
		public void UpLevel(int spellNumber)
		{
			SetLevel (spellNumber, GetLevel (spellNumber) + 1);
		}

		public int GetItemSelected (int pos)
		{	
			if (pos >= ItemsChoised.Count)
				return -1;
			else
				return ItemsChoised[pos];
		}
		public void SetItemSelected (int pos, int index)
		{				
			if (pos >= MaxItemsToChoise)
				return;
			if (ItemsChoised.Contains (pos) && index != -1)
				return;
			for (int i = 0; ItemsChoised.Count<=pos; i++)
				ItemsChoised.Add (-1);
			ItemsChoised[pos] = index;
			GlobalEvents.ItemsChanged.Invoke (pos);
		}
	}
}