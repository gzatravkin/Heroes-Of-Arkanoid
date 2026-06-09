using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SO_Fire_Passive : SO_InvisibleSkill {
	public GameObject Fire_OnBallEffect;
	public SkillNumberParameter maxNormalizedDistance = new SkillNumberParameter (0.1f, 0.13f, 0.15f);
	[HideInInspector]
	public SkillNumberParameter ExplotionSize = new SkillNumberParameter (0.1f, 0.13f, 0.15f);
	[HideInInspector]
	public SkillNumberParameter maxHits = new SkillNumberParameter(1,4,6);
	[HideInInspector]
	public SkillNumberParameter Damage = new SkillNumberParameter(1,1,1);
	[HideInInspector]
	public SkillNumberParameter Velocity = new SkillNumberParameter(0.5f,1.7f,5);
	protected override void SkillInitialization ()
	{
		base.SkillInitialization ();
		BattleEventsManager.Events.Ball_BarCollision.AddListener ((((BallController arg0, float arg1) => CheckBallPos (arg0,arg1))));
	}
	public void CheckBallPos(BallController ball, float normalizedPos)
	{			
		if (Mathf.Abs (normalizedPos)<maxNormalizedDistance) {
			UpgradeBall (ball);
		}
	}
	public void UpgradeBall(BallController ball)
	{
		var t = (GameObject)Instantiate (Fire_OnBallEffect);
		t.SetParentWithScaleOne (ball.transform);		
		t.GetComponent<Ball_FirePassiveEffect> ().Initialization (ball, ExplotionSize, maxHits, Damage,Velocity);
	}
}
