using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CastSpell_Button : MonoBehaviour {
	private SpellPanel_List spellPanel;
	public Button clickButton;
	public Image Ico;
	private SO_VisibleSkill spell;
	public int currentBtnNumber;
	public void GameInitialization(SpellPanel_List spellPanel, int ButtonNumber)
	{		
		this.spellPanel = spellPanel;
		currentBtnNumber = ButtonNumber;
	}
	void Update()
	{
		if (Input.GetKeyDown ("" + (currentBtnNumber+1)))
			spellPanel.CastSpell (spell);
	}
	public void SetSpell(SO_VisibleSkill spell)
	{
		Ico.sprite = spell.ICO_CUADRADO;
		this.spell = spell;
		clickButton.onClick.AddListener (() => spellPanel.CastSpell (spell));
	}
	public void RemoveSpell()
	{
		Ico.sprite = SO_Configuraciones.obj.LockedSpell;
		this.spell = null;
		clickButton.onClick.RemoveAllListeners ();
	}
}
