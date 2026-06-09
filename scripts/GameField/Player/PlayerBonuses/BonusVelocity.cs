using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BonusVelocity : AbstractBonus {
	public float Speed=5f;
	public override void Recalculation ()
	{
		base.Recalculation ();
		var playerBar = (Player_BarController)player;
		if (playerBar != null)
			playerBar.CurrentBallSpeed += Speed;
	}
	public BonusVelocity(float speed=1f,float time=3f)
	{
		this.Speed = speed;
		this.TimeExist = time;
	}
}
