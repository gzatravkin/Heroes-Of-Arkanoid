using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SO_Fire_RingSpell : SO_VisibleSkill {
	public GameObject FireBall;
	[HideInInspector]
	public SkillNumberParameter maxHits, size;
	protected override void SpellCast ()
	{
		var t = Instantiate(FireBall,BattleController.GetCurrentPlayerObject ().transform.position+Vector3.up,Quaternion.identity);
		t.GetComponent<BlockHitter> ().MaxHits = maxHits;
		t.transform.localScale = t.transform.localScale * size;
	}
}
