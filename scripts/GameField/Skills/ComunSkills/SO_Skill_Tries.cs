using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SO_Skill_Tries : SO_InvisibleSkill {
	public SkillNumberParameter AddicionalBalls;
	protected override void SkillInitialization ()
	{
		base.SkillInitialization ();
		LifeManager.SetDefaultBalls ();
		LifeManager.AddBalls(AddicionalBalls);
	}

}
