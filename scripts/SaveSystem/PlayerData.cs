using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Saves
{
	[System.Serializable]
	public class PlayerData  {
		public int CurrentHero = 0;
		public static readonly int PointsToItem = 100;
		public List<CharactaerData> Characters;
		public int TreasurePoints = 0;
		public ItemsData items = new ItemsData();
		public ResourceData resources;
		public void SetCurrentHero(int number)
		{
			if (CurrentHero != number) {
				CurrentHero = number;
				GlobalEvents.ClassChanged.Invoke ();
			}
		}
		public void AddTreasurePoints(int points)
		{
			TreasurePoints += points;
			if (TreasurePoints >= PointsToItem) {
				TreasurePoints = 0;
				ItemsManager.GetRandomItem ();
			}
		}
		public void AddCharacter(int clase)
		{
			if (Characters == null)
				Characters = new List<CharactaerData> ();
			Characters.Add (new CharactaerData(clase));
		}
	}
}