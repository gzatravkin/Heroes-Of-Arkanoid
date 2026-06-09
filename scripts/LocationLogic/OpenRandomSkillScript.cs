using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OpenRandomSkillScript : MonoBehaviour {
	void Start()
	{		
		var skills = Saves.SaveSystem.GetCurrentHero ().GetAllSkills ();
		int skillNumber = Random.Range (0, skills.Count);
		int sum = 1;
		if (skills [skillNumber].GetCurrentLevel () == -1)
			sum = 2;
		skills [skillNumber].SetLevel (skills [skillNumber].GetCurrentLevel () + sum);
		ServiceLocator.spellPanel.GameInitialization (ServiceLocator.spellController);
		skills [skillNumber].GameIniciacion ();
	}
}
