using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockEffect_ChangeSpriteByHP : AbstractBlockEffect {
	[System.Serializable]
	public class SpriteData
	{
		public Sprite sprite;
		public float TimeToSwap;
		public int HP;
	}
	public List<SpriteData> spriteData;
	public override void Hitted (int damage, DamageType damageType)
	{
		base.Hitted (damage, damageType);
		if (spriteData != null) {
			var SpriteToSet = spriteData.Find (x =>x!=null&& x.HP == (block.HP));
			if (SpriteToSet == null)
				return;
			if (SpriteToSet.TimeToSwap != 0f)
				StartCoroutine (StaticEffects_SwapOfSprite.SwapSprite (GetComponent<SpriteRenderer> (), SpriteToSet.sprite, SpriteToSet.TimeToSwap));
			else
				GetComponent<SpriteRenderer> ().sprite = SpriteToSet.sprite;
		}
	}

}
