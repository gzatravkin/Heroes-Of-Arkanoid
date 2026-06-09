using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockEffect_GetMoreDamage : AbstractBlockEffect{
	public DamageType damageToUp=DamageType.Ball;
	public float Coef=3;
	public override void Hitting (ref int damage, DamageType damageType)
	{
		base.Hitting (ref damage, damageType);
		if (damageType == damageToUp)
			damage = Mathf.RoundToInt(damage * Coef);
	}
}
