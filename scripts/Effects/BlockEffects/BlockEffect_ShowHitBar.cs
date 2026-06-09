using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockEffect_ShowHitBar : AbstractBlockEffect {
	public CustomHitBar hitBar;
	public override void Initialization (int defaultHits, BlockScript block)
	{
		base.Initialization (defaultHits,block);
		hitBar.SetHP (defaultHits);
		hitBar.SetMaxHP (defaultHits);
	}
	public override void Hitted (int damage, DamageType damageType)
	{
		base.Hitted (damage,damageType);
		hitBar.SetHP (block.HP);
	}

}
