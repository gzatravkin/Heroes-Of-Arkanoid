using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Saves
{
	[System.Serializable]
	public class SpellData
	{
		[System.Serializable]
		public class Skill
		{
			public int Level;
			public bool Openned;
			public Skill()
			{
			}
			public Skill(int level)
			{
				this.Level=level;
			}
			public Skill(int level, bool Openned)
			{
				this.Level=level;
				this.Openned=Openned;
			}
		}
		private const int DefaultLevelValue = -1;
		public List<Skill> skills;
		public SpellData(int clase)
		{				
			skills = new List<Skill> ();
			var allSkills = SO_Configuraciones.obj.Heroes [clase].GetAllSkills ();
			for (int i = 0 ;i<allSkills.Count;i++)
			{
				skills.Add (new Skill(0,allSkills[i].AutoOpen));
			}			
		}
		public SpellData()
		{
			if (skills == null)
				skills = new List<Skill> ();
		}
		public int GetLevel (int spellNumber)
		{	
			if (spellNumber >= skills.Count)
				return -1;
			else
				return skills [spellNumber].Level;
		}
		public void SetLevel (int spellNumber, int Level, bool IsOpen=false)
		{				
			for (int i = 0; skills.Count <= spellNumber; i++)
				skills.Add (new Skill(DefaultLevelValue,false));
			skills [spellNumber] = new Skill(Level,IsOpen);
			GlobalEvents.SkillsChanged.Invoke();
		}
		public void UpLevel(int spellNumber)
		{
			SetLevel (spellNumber, GetLevel (spellNumber) + 1, GetRawData()[spellNumber].Openned);
		}
		public List<Skill> GetRawData()
		{
			return skills;
		}
	}
}