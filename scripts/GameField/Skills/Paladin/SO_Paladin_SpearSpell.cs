using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SO_Paladin_SpearSpell : SO_VisibleSkill {

	public GameObject Spear;
	[HideInInspector]
	public SkillNumberParameter maxHits = new SkillNumberParameter(4,6,9), size=new SkillNumberParameter(0.8f,1f,1.2f);
	protected override void SpellCast ()
	{
		var t = Instantiate(Spear,BattleController.GetCurrentPlayerObject ().transform.position+Vector3.up,Quaternion.identity) as GameObject;
		t.GetComponent<BlockHitter> ().MaxHits = maxHits;
		t.transform.localScale = t.transform.localScale * size;
	}
}
