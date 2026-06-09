using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class SO_VisibleSkill : SO_AbstractSkill {		
	[Header("Parameter {1}")]
	public float Activate_Recourse;
	public void Cast()
	{		
		SpellCast ();
		BattleEventsManager.Events.SpellCasted.Invoke ();
	}

	public override List<string> GetDefaultParms ()
	{
		var parms = base.GetDefaultParms ();
		parms.Add (Activate_Recourse.ToString());
		return parms;
	}
	protected virtual void SpellCast()
	{
	}
}
