using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName="ScriptableObject/SpellControllers/Souls")]
public class SO_SpellController_Soulce : SO_SpellController {
	public float OrigenValue=0;
	public float MPPerSoul=10;
	public override void GameInitialization (SO_Hero Hero, RecourcePanel RecourceShower)
	{
		base.GameInitialization (Hero, RecourceShower);
		Recource = OrigenValue;
		BattleEventsManager.Events.BlockDestroyed.AddListener ((arg0) => BlockDestroyReaction());
	}
	private void BlockDestroyReaction()
	{
		Recource = Mathf.Min (MaxRecource, Recource + MPPerSoul);
	}
}
