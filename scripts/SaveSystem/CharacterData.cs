using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Saves
{
	[System.Serializable]
	public class CharactaerData  {
		public int claseIndex,Lvl,Exp;
		public int LibrePoints,TotalPoints;
		public bool canChoise;
		public List<int> missionesWinned = new List<int>(0);
		public SpellData spellData = new SpellData();
		public static int GetExpToLevel(int level)
		{
			return Mathf.RoundToInt(100f * Mathf.Pow (1.1f, (level - 1)));
		}
		public int ExpToLevelUp(bool WithActual=true)
		{
			int exp = GetExpToLevel (Lvl + 1);
			if (WithActual)
				exp += -Exp;
			return exp;
		}
		public void SaveMissionAsWinned(int number)
		{
			if (missionesWinned.FindIndex (x=>x==number) == -1)
				missionesWinned.Add (number);
		}
		public void LevelUp()
		{
			Lvl++;
			TotalPoints += Mathf.RoundToInt(SO_Configuraciones.obj.PointsForLevel.GetGridValue (Lvl));
			LibrePoints += Mathf.RoundToInt(SO_Configuraciones.obj.PointsForLevel.GetGridValue (Lvl));
		}
		public void AddExp(int exp)
		{		
			Exp += exp;	
			while (ExpToLevelUp (false) <= Exp) {
				Exp += -ExpToLevelUp (false);
				LevelUp ();
			}
		}
		public CharactaerData(int claseIndex)
		{
			this.claseIndex = claseIndex;
			this.spellData = new SpellData (claseIndex);
			InitializationOfSpells ();
			LibrePoints = 0;
		}
		public CharactaerData()
		{
			//LibrePoints = 250;
			//InitializationOfSpells ();
		}
		public void ReturnUpgardes()
		{
			LibrePoints = TotalPoints;
			var t = spellData.GetRawData ();
			foreach (var d in t) {
				d.Level = 0;
			}
		}
		void InitializationOfSpells()
		{
		this.spellData = new SpellData ();
		var allskills = SO_Configuraciones.obj.Heroes [claseIndex].GetAllSkills ();
		for (int i = 0; i<allskills.Count;i++)
				spellData.SetLevel(i,0);
		}
		public void ReloadSpellLevels()
		{
			SO_Configuraciones.obj.Heroes [claseIndex].SetSpellLevelsData(spellData.GetRawData().ToArray());
		}
	}
}