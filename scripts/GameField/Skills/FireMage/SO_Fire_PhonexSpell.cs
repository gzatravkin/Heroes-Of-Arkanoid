using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SO_Fire_PhonexSpell : SO_VisibleSkill {
	public GameObject PhonexEffect;
	[HideInInspector]
	public SkillNumberParameter LifeTime, Size, Damage,MaxHits;
	protected override void SpellCast ()
	{
		var ball = ((Player_BarController)BattleController.GetCurrentPlayerObject ()).GetRandomBall();
		var phonexEffect = Instantiate (PhonexEffect);
		phonexEffect.transform.parent = ball.transform;
		phonexEffect.transform.localPosition = Vector3.zero;
		phonexEffect.transform.localScale = Vector3.one*(float)Size;
		phonexEffect.transform.localRotation = Quaternion.identity;
		var blockHitter = phonexEffect.GetComponent<BlockHitter> ();
		var destroyDelayed = phonexEffect.GetComponent<Effects_DestroyInTime> ();
		blockHitter.Damage = Damage;
		blockHitter.MaxHits = MaxHits;
		destroyDelayed.TimeToDestroy = LifeTime;
	}

}
