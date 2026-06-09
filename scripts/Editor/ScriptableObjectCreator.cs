using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Linq;
public class ScriptableObjectCreator : MonoBehaviour {
	private static void SaveAsset(Object asset,string Dirrecion)
	{	
		string Name = asset.name;
		AssetDatabase.CreateAsset(asset, Dirrecion+asset.GetType().Name.ToString()+".asset");
		AssetDatabase.SaveAssets();
		EditorUtility.FocusProjectWindow();
		Selection.activeObject = asset;
	}

	[MenuItem("Assets/Create/ScriptableObject/Spells/Fire")]
	public static void CreateSpells_Fire()
	{
		var lA = new List<Object> ();

		lA.Add(ScriptableObject.CreateInstance<SO_Fire_PhonexSpell>());
		lA.Add(ScriptableObject.CreateInstance<SO_Fire_RingSpell>());
		lA.Add(ScriptableObject.CreateInstance<SO_Fire_TurretSpell>());
		lA.Add(ScriptableObject.CreateInstance<SO_Fire_WallSpell>());
		lA.Add(ScriptableObject.CreateInstance<SO_Fire_Passive>());
		foreach (Object o in lA)
			SaveAsset(o,"Assets/ScriptableObjects/Skills/FireMage/");
	}
	[MenuItem("Assets/Create/ScriptableObject/Spells/Paladin")]
	public static void CreateSpells_Paladin()
	{
		var lA = new List<Object> ();

		lA.Add(ScriptableObject.CreateInstance<SO_Paladin_DuplicationSpell>());
		lA.Add(ScriptableObject.CreateInstance<SO_Paladin_LastDaySpell>());
		lA.Add(ScriptableObject.CreateInstance<SO_Paladin_Passive>());
		lA.Add(ScriptableObject.CreateInstance<SO_Paladin_PenterationSpell>());
		lA.Add(ScriptableObject.CreateInstance<SO_Paladin_SpearSpell>());
		foreach (Object o in lA)
			SaveAsset(o,"Assets/ScriptableObjects/Skills/Paladin/");
	}
	[MenuItem("Assets/Create/ScriptableObject/Spells/Engineer")]
	public static void CreateSpells_Engineer()
	{
		var lA = new List<Object> ();

		lA.Add(ScriptableObject.CreateInstance<SO_Engineer_LightingSpell>());
		lA.Add(ScriptableObject.CreateInstance<SO_Engineer_MagnetSpell>());
		lA.Add(ScriptableObject.CreateInstance<SO_Engineer_Passive>());
		lA.Add(ScriptableObject.CreateInstance<SO_Engineer_RadiationSpell>());
		lA.Add(ScriptableObject.CreateInstance<SO_Engineer_RocketSpell>());
		foreach (Object o in lA)
			SaveAsset(o,"Assets/ScriptableObjects/Skills/Engineer/");
	}
	[MenuItem("Assets/Create/ScriptableObject/SkillsComun")]
	public static void CreateSkillsComunes()
	{
		var lA = new List<Object> ();
		lA.Add(ScriptableObject.CreateInstance<SO_Skill_Life>());
		lA.Add(ScriptableObject.CreateInstance<SO_Skill_Size>());
		lA.Add(ScriptableObject.CreateInstance<SO_Skill_Tries>());
		foreach (Object o in lA)
			SaveAsset(o,"Assets/ScriptableObjects/Skills/Comunes/");
	}
	[MenuItem("Assets/Create/ScriptableObject/Configuraciones")]
	public static void CreateConfiguraciones()
	{
		SaveAsset(ScriptableObject.CreateInstance<SO_Configuraciones>(),"Assets/ScriptableObjects/");
	}

	[MenuItem("Assets/Create/ScriptableObject/SpellSystem/ManaSpellSystem")]
	public static void CreateSpellSystemMana()
	{
		SaveAsset(ScriptableObject.CreateInstance<SO_SpellController_Mana>(),"Assets/ScriptableObjects/");
	}
}
