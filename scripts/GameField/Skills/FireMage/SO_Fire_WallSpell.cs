using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SO_Fire_WallSpell : SO_VisibleSkill {	
	[HideInInspector]
	public SkillNumberParameter MaxBlocks	,TimeToBurn,TimeToExtend,Damage, Area,BlocksInAreaToMaxDestroy;
	public GameObject FireWall_BallEffect;
	public GameObject FireAnimation;
	protected override void SpellCast ()
	{
		var Bar = (Player_BarController)BattleController.GetPlayer ();
		var randomBall = Bar.GetRandomBall();
		var fireWallEffect = Instantiate (FireWall_BallEffect);
		fireWallEffect.SetParentWithScaleOne (randomBall.transform);
		GameObject FireWallController = new GameObject ("FireWallController");
		FireWallController.SetParentByName ("Managers");
		var fireWallController = FireWallController.AddComponent<FireWallController> ();
		fireWallController.Initialization (
			FireAnimation,
			fireWallEffect, 
			randomBall, 
			Damage, 
			Area,
			MaxBlocks,
			BlocksInAreaToMaxDestroy,
			TimeToExtend,
			TimeToBurn 
		);	
	}
}
