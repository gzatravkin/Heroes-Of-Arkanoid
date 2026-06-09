using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
public class SO_AbstractSkill : ScriptableObject{
	public Sprite[] ICOS_UPGRADE_LEVELS=new Sprite[1];
	public Sprite ICO_CUADRADO;
	public TextTranslation Name;
	public TextTranslation Descripcion;
	public TextTranslation fullDescripcion;
	public Skill_UpgradeInfo upgradeInfo;
	public bool AutoOpen=true;
	[Header("Parameter {0}"),SerializeField]
	private int CurrentLevel;
	public List<SkillNumberParameter> NumerableObjects { 
		get {if (numerableObjects == null)
			RefreshPropetrys (); 
			return numerableObjects; }
	}
	public List<FieldInfo> NumerableFields { 		
		get {if (numerableFields == null)
				RefreshPropetrys (); 
			return numerableFields; }
	}
	private List<FieldInfo> numerableFields;
	private List<SkillNumberParameter> numerableObjects;
	public virtual Sprite Get_ICO_UPGRADE(int Level)
	{
		Level = Mathf.Clamp (Level,0, ICOS_UPGRADE_LEVELS.Length - 1);
		return ICOS_UPGRADE_LEVELS[Level];
	}
	public bool IsOpenned(Saves.CharactaerData character)
	{
		var spellData = character.spellData;
		if (AutoOpen||spellData.GetRawData()[GetSpellIndex(character.claseIndex)].Openned)
			return true;
		return false;
	}
	public virtual int GetCurrentLevel()
	{		
		return CurrentLevel;
		//return level of skill based of su codename
	}

	public virtual string GetDescripcion()
	{
		return Descripcion;
	}
	public virtual List<string> GetDefaultParms ()
	{
		var parms = new List<string> ();
		parms.Add (CurrentLevel.ToString ());
		return parms;
	}
	public virtual string GetFullDescripcion()
	{
		var parms = GetDefaultParms ();
		for (int i = 0; i < numerableObjects.Count; i++) {
			parms.Add (numerableObjects [i]);
		}

		return string.Format(fullDescripcion,parms.ToArray());
	}
	public void GameIniciacion()
	{
		SkillInitialization ();
	}
	protected virtual void SkillInitialization()
	{
		
	}
	public void SetLevel(int level)
	{
		CurrentLevel = Mathf.Min(GetMaxLevel(),level);
		foreach (SkillNumberParameter parameter in NumerableObjects) {
			parameter.SetLvl (level);
		}
	}
	public int GetSpellIndex(SO_Hero hero)
	{
		for (int i = 0; i < hero.Skills.Length; i++) {
			if (hero.Skills [i] == this)
				return i;
		}
		for (int i = 0; i < hero.Spells.Length; i++) {
			if (hero.Spells [i] == this)
				return i+hero.Skills.Length;
		}
		return -1;
	}
	public int GetSpellIndex(int claseIndex)
	{
		var hero = SO_Configuraciones.obj.Heroes [claseIndex];
		return GetSpellIndex (hero);
	}
	public int GetPointsToUpgrade(int level)
	{
		return upgradeInfo.startPrice + upgradeInfo.priceForLevel * level;
	}
	public int GetMaxLevel()
	{
		return upgradeInfo.MaxLevel;
	}
	public void RefreshPropetrys()
	{
		if (numerableFields == null)
			numerableFields = new List<FieldInfo> (0);
		numerableFields.Clear ();
		if (numerableObjects == null)
			numerableObjects = new List<SkillNumberParameter> (0);
		numerableFields.Clear ();
		numerableObjects.Clear ();
		var fields = this.GetType ().GetFields ();
		foreach (FieldInfo fI in fields) {
			if (fI.FieldType == typeof(SkillNumberParameter)) {
				numerableFields.Add (fI);
				numerableObjects.Add ((SkillNumberParameter)fI.GetValue (this));
			}
		}
	}
	public int PointsToLevel(int levelFrom, int levelTo)
	{
		int sum = 0;
		for (int i = levelFrom + 1; i <= levelTo; i++)
			sum += GetPointsToUpgrade (i);
		return sum;
	}
	public void OnValidate()
	{
		RefreshPropetrys ();
		if (upgradeInfo.Equals(default(Skill_UpgradeInfo))) {
			upgradeInfo.MaxLevel = 10;
			upgradeInfo.priceForLevel = 2;
			upgradeInfo.startPrice = 5;
		}
	}
	public void OnEnable()
	{
		RefreshPropetrys ();
	}
}
