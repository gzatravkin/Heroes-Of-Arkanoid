using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SO_Paladin_Passive : SO_InvisibleSkill {
	[HideInInspector]
	public SkillNumberParameter size=new SkillNumberParameter(0.7f,0.9f,1.2f);
	[HideInInspector]
	public SkillNumberParameter maxHits=new SkillNumberParameter(1,1,2,true);
	[HideInInspector]
	public SkillNumberParameter reflectSpeed=new SkillNumberParameter(3,4,5);
	public GameObject ShildObject;
	private GameObject shildActual;
	protected override void SkillInitialization ()
	{
		base.SkillInitialization ();
		if (shildActual == null) {
			var bar = Saves.SaveSystem.GetCurrentHero ().GetActualBar ();
			shildActual = Instantiate (ShildObject) as GameObject;
			shildActual.SetParentWithScaleOne (bar.transform);
			shildActual.transform.localScale = new Vector3 (size, 1, 1);
			var shildComponent = shildActual.GetComponent<PaladinShild> ();
			shildComponent.MaxHits = maxHits;
			shildComponent.reflectionSpeed = reflectSpeed;
		}
	}
}
