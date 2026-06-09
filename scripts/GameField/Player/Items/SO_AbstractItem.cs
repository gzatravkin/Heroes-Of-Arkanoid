using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
public abstract class SO_AbstractItem : ScriptableObject {
	public int maxLevel=3;
	public Sprite[] Ico;
	public TextTranslation Name;
	public TextTranslation Descripcion;
	public int Level = 0;
	public int GetItemIndex()
	{
		return SO_Configuraciones.obj.Items.FindIndex (x => x == this);
	}
	public void RefreshLevel()
	{
		Level = Saves.SaveSystem.GetPlayerData().items.GetLevel(GetItemIndex());
	}
	public void GameInitialization()
	{
		RefreshLevel ();
		if (Level>0&&IsSelected())
			ItemInitialization ();
	}
	protected virtual void ItemInitialization()
	{
	}
	public bool IsSelected()
	{
		return Saves.SaveSystem.GetPlayerData ().items.ItemsChoised.Contains (GetItemIndex ());
	}
}
