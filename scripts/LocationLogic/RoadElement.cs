//Space to clean choise, cntr to destroy all conections
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RoadElement
{	
	public Level levelObj;

	[System.Serializable]
	public class RoadElementReference
	{
		public int RoadKey;
		public bool Rigid = false;

		public RoadElementReference (int RoadKey, bool Rigid)
		{
			this.RoadKey = RoadKey;
			this.Rigid = Rigid;
		}

		public RoadElementReference ()
		{
		}
	}

	public List<RoadElementReference> RequieredLevels = new List<RoadElementReference> ();
	public void RemoveAllReferences()
	{
		var output = GetOutput ();
		foreach (var o in output)
			o.RemoveReference (UniqueCode);
		RequieredLevels.Clear ();
	}
	public List<RoadElement> GetEntry ()
	{
		return road.levels.FindAll (x => ContainsReference (x));
	}

	public List<RoadElement> GetOutput ()
	{
		return road.levels.FindAll (x => x.ContainsReference (this));
	}
	public RoadElement ()
	{
	}
	public RoadElement (Level levelObj)
	{
		this.levelObj = levelObj;
	}
	public void RemoveReference (int UniqueCode)
	{
		RequieredLevels.RemoveAll (x => x.RoadKey == UniqueCode);
	}

	public void RemoveReference (RoadElement element)
	{
		RemoveReference (element.UniqueCode);
	}

	public void AddReference (int codeLevelToWin, bool rigid)
	{
		RequieredLevels.Add (new RoadElementReference (codeLevelToWin, rigid));
	}

	public bool ContainsReference (RoadElement other)
	{
		return (RequieredLevels.Find (x => x.RoadKey == other.UniqueCode) != null);
	}

	public RoadElementReference GetObjectReference (RoadElement other)
	{
		return RequieredLevels.Find (x => x.RoadKey == other.UniqueCode);
	}

	public Vector2 ElementPos;
	[HideInInspector]
	public HeroRoad road;
	public int UniqueCode = 0;

	public bool IsOpen (Saves.CharactaerData character)
	{
		if (IsWinned (character) || RequieredLevels == null || RequieredLevels.Count == 0)
			return true;
		else {
			bool opened = true;
			var entry = GetEntry ();
			if (entry.TrueForAll (x => !x.IsWinned (character)))
				opened = false;
			if (opened)
				foreach (var roadElement in entry) {
					var relacion = GetObjectReference (roadElement);
					if (relacion.Rigid && !roadElement.IsWinned (character)) {
						opened = false;
						break;
					}
				}
			return opened;
		}
	}

	public bool IsWinned (Saves.CharactaerData character)
	{		
		return (character.missionesWinned.FindIndex (x => x == UniqueCode) != -1);
	}
}