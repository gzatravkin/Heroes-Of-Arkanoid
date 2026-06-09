using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HeroIcoShower : MonoBehaviour {
	public Image image;
	public void Start()
	{		
		RefresIco ();
		GlobalEvents.ClassChanged.AddListener (() => RefresIco ());
	}
	public void RefresIco()
	{		
		SetIco (Saves.SaveSystem.GetCurrentCharacterData ().claseIndex);
	}
	private void SetIco(int ClaseIndex)
	{
		if (image==null)
			image = GetComponent<Image> ();
		image.sprite= SO_Configuraciones.obj.Heroes[ClaseIndex].SPRITE_INTERFACE_ICO;
	}

}
