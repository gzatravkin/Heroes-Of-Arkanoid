using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SpellPanel_List:MonoBehaviour {

	public static SpellPanel_List objRef;
	public GameObject ListParent;
	public GameObject ButtonSample;
	//[HideInInspector,SerializeField]
//	private float WidthPart=0.1375f;
	public List<CastSpell_Button> spellButtons;
	public RectTransform WidthObject;
	protected SO_SpellController spellController;
	protected List<bool> skillLevels;
	protected SO_VisibleSkill[] Spells;
	void Awake()
	{
		objRef = this;
	}
	public void SetSkillMask(List<bool> levels)
	{
		skillLevels = levels;
	}
	protected bool IsOpenSpell(int index)
	{		
		return Spells [index].GetCurrentLevel ()>0;		
	}
	public void CastSpell(SO_VisibleSkill spell)
	{		
		if (spell!=null)			
			spellController.CastSpell (spell);
	}
	public void GameInitialization(SO_SpellController spellController)
	{
		this.spellController = spellController;
		Spells = spellController.GetSpells ();
		ServiceLocator.spellPanel = this;
		Initialization ();
	}
	public static float GetWidthPart()
	{
		return SO_Configuraciones.obj.WidthPartOfBattle;
	}
	protected void Initialization ()
	{
		ListParent.KillAllChilds ();
		spellButtons.Clear ();
		for (int i = 0; i < Spells.Length; i++) {			
			var but = (GameObject)Instantiate (ButtonSample, Vector3.zero, ListParent.transform.rotation);
			but.SetParentWithScaleOne (ListParent.transform);
			var castSpellButton = but.GetComponent<CastSpell_Button> ();
			spellButtons.Add (castSpellButton);
			castSpellButton.GameInitialization (this,i);
			if (IsOpenSpell(i))
				castSpellButton.SetSpell (Spells [i]);
		}
	}
	public void ForceOpenSpell (int spellIndex)
	{
		var Spells = spellController.GetSpells ();
		spellButtons [spellIndex].SetSpell (Spells [spellIndex]);
	}
	public void ForceCloseSpell (int spellIndex)
	{		
		spellButtons [spellIndex].RemoveSpell ();
	}
	#if UNITY_EDITOR
	void OnValidate()
	{		
		if (UnityEditor.PrefabUtility.GetPrefabParent (gameObject) == null && UnityEditor.PrefabUtility.GetPrefabObject (gameObject) != null)
			return;
//		var battleFieldCameraController = GameObject.FindObjectOfType<BattleFieldCameraController> ();
		//if (battleFieldCameraController!=null)
			//WidthPart = battleFieldCameraController .GetWidthPart((WidthObject.rect.xMax - WidthObject.rect.xMin)*transform.localScale.x);	
	}
	#endif
}
