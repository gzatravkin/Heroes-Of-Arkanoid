using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
public class SkillLevelNeededParameter :Abstract_SkillParameter  {
	public int LevelNeeded;
	public TextTranslation Descripcion_WhenHave;
	public TextTranslation Descripcion_WhenHaveNot;
	public bool IsActive()
	{
		return false;
	}

}
