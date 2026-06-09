using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockEffects_OpenLavaSpawner : AbstractBlockEffect {
	public int HpToOpen=1;
	public FabricOfObjects lavaFabric;
	public override void Hitted (int Damage, DamageType damageType)
	{
		base.Hitted (Damage, damageType);
		if (base.block.HP == HpToOpen)
			lavaFabric.enabled = true;
	}
}
