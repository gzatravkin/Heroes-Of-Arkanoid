using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class AbstractBonus {
	public bool Finished = false;
	public float TimeExist = 0f;
	public float BattleTimeToDesactive=0f;
	protected AbstractPlayerController player;
	public void SetTimeExist(float time)
	{
		TimeExist = time;
		BattleTimeToDesactive = TimeManager.battleTime + TimeExist;
	}
	public virtual void Activate(AbstractPlayerController player)
	{
		BattleTimeToDesactive = TimeManager.battleTime + TimeExist;
		this.player = player;
	}
	public virtual void Recalculation()
	{		
	}
	public virtual void Desactivate()
	{
		Finished = true;
	}
}
