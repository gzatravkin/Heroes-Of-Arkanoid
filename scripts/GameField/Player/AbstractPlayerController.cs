using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class AbstractPlayerController : MonoBehaviour {
	public List<AbstractBonus> bonuses = new List<AbstractBonus>();
	public virtual void Initialization()
	{
		BattleEventsManager.Events.UpdateEvent.AddListener ( (() => Update ()));
	}
	private void Update()
	{
		bool flag = false;
		for (int i = 0; i < bonuses.Count; i++) {
			if (bonuses [i].TimeExist > 0 && bonuses [i].BattleTimeToDesactive < TimeManager.battleTime||bonuses[i].Finished) {
				flag = true;
				bonuses [i].Desactivate ();
				bonuses.RemoveAt (i);
			}
		}
		if (flag)
			RecalculateBonuses ();
	}
	public virtual AbstractBonus AddBonus(AbstractBonus bonus)
	{
		bonuses.Add (bonus);
		bonus.Activate (this);
		RecalculateBonuses ();
		return bonus;
	}
	public void GetDamage(float Damage)
	{
		LifeManager.GetDamage (Damage);
	}
	public virtual void SetDefaultCaracteristicas()
	{
	}
	public virtual void RecalculateBonuses()
	{
		SetDefaultCaracteristicas ();
		for (int i = 0; i < bonuses.Count; i++) {
			bonuses [i].Recalculation ();
		}
	}
	public abstract bool IsInGame ();
}
