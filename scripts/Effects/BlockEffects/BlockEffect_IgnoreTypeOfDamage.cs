using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockEffect_IgnoreTypeOfDamage : AbstractBlockEffect {
	public DamageType damageToIgnore;
	public override void Hitting (ref int damage, DamageType damageType)
	{
		base.Hitting (ref damage, damageType);
		if (damageType == damageToIgnore)
			damage = 0;
	}
}
