using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ClassPanel : MonoBehaviour {
	public GameObject list;
	public ButtonsMenu buttonsMenu;
	void Start()
	{
		if (buttonsMenu != null) {
			var listOfClassButtons = new List<PalletButtonData> ();
			var characters = Saves.SaveSystem.GetPlayerData ().Characters;
			for (int i = 0; i < characters.Count; i++) {
				int number = i;
				var t = new PalletButtonData ("", SO_Configuraciones.obj.Heroes [characters [i].claseIndex].SPRITE_INTERFACE_ICO, () => Select (number));
				t.openTarget = SO_Configuraciones.obj.Heroes [characters [i].claseIndex];
				t.OpenCondition = (x => ((SO_Hero)x).IsOpenned ());
				listOfClassButtons.Add (t);
			}
			buttonsMenu.Initialization (listOfClassButtons, Saves.SaveSystem.GetPlayerData ().CurrentHero);
		}
	}
	void Select(int pers)
	{
		Saves.SaveSystem.GetPlayerData ().SetCurrentHero(pers);
	}
}
