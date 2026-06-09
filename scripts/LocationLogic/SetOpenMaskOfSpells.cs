using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetOpenMaskOfSpells : MonoBehaviour {
	public bool[] mask = new bool[4];
	void Start()
	{
		var spellPanel = ServiceLocator.spellPanel;
		if (spellPanel!=null)
		for (int i = 0 ; i <mask.Length;i++)
		{
			if (mask[i])
				spellPanel.ForceOpenSpell(i);
			else
				spellPanel.ForceCloseSpell (i);
		}
	}
}
