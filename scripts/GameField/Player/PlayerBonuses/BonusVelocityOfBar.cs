using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BonusVelocityOfBar : AbstractBonus {
	public float Speed=5f;
	public override void Recalculation ()
	{
		base.Recalculation ();
		var playerBar = (Player_BarController)player;
		if (playerBar != null)
			playerBar.barMove.MovimientSpeed += Speed;
	}
	public BonusVelocityOfBar(float speed=1f,float time=3f)
	{
		this.Speed = speed;
		this.TimeExist = time;
	}

}
