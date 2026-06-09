using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OpenSkillScript : MonoBehaviour {
	public int SkillNumber = 5;
	public int Level = 1;
	public int FreeCasts = 2;
	public void Start()
	{
		var Character = Saves.SaveSystem.GetCurrentCharacterData ();
		var SpellData = Saves.SaveSystem.GetCurrentCharacterData ().spellData;

		if (!SpellData.GetRawData () [SkillNumber].Openned && SpellData.GetLevel (SkillNumber) < Level) {
			Character.TotalPoints += SO_Configuraciones.obj.Heroes [Character.claseIndex].GetAllSkills () [SkillNumber].PointsToLevel (SpellData.GetLevel (SkillNumber), Level);
			SpellData.SetLevel (SkillNumber, Level, true);
			PalletsCreator.CreatePallet_SkillOpen (SO_Configuraciones.obj.Heroes [Character.claseIndex].GetAllSkills () [SkillNumber]);
		}
		ServiceLocator.spellController.AddFreeCasts (FreeCasts);
	}
}
