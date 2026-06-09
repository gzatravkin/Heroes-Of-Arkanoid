using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AbstractBlockEffect : MonoBehaviour {
	protected int defaultHits;
	protected BlockScript block;
	public virtual void Initialization (int defaultHits, BlockScript block)
	{
		this.defaultHits = defaultHits;
		this.block = block;
	}
	public virtual void Hitted(int Damage, DamageType damageType)
	{
	}
	public virtual void Hitting(ref int damage, DamageType damageType)
	{
	}
	public virtual void Die()
	{
	}
}
