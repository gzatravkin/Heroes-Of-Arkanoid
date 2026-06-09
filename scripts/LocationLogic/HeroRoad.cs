using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName="ScriptableObject/HeroRoad")]
public class HeroRoad : ScriptableObject {
	public List<RoadElement> levels = new List<RoadElement>();
	public bool AutoLine=true;
	[SerializeField]
	private List<GameObject> ObjectsToParss = new List<GameObject>();
	void ParsObjects()
	{
		for (int i = 0; i < ObjectsToParss.Count; i++) {
			var levelObj = ObjectsToParss [i].GetComponent<Level> ();
			if (levelObj != null)
				levels.Add (new RoadElement (levelObj));
		}
		ObjectsToParss.Clear ();
	}
	float findMaxX()
	{
		float MaxX = levels[0].ElementPos.x;
		levels.TrueForAll (x => {
			if (x.ElementPos.x>MaxX)
				MaxX=x.ElementPos.x;
			return true;
		});
		return MaxX;
	}
	void OnValidate()
	{

		ParsObjects ();
		levels.RemoveAll (x => x.levelObj == null);
		for (int i = 0; i<levels.Count; i++) {
			if (levels [i].UniqueCode == 0||!IsUnique(levels[i].UniqueCode)) {
				levels [i].UniqueCode = GetFirstLibreKey (levels);
			}
			levels [i].RequieredLevels.RemoveAll (x => GetRoadElementByKey (x.RoadKey) == null);
			if (i > 0) {
				if (AutoLine && levels [i].RequieredLevels.Count == 0) {
					levels [i].RequieredLevels.Add (new RoadElement.RoadElementReference (levels [i - 1].UniqueCode, false));
					levels [i].ElementPos = new Vector2 (findMaxX() + 1f, 0);
				}
			}
		}
	}
	void OnEnable()
	{
		for (int i = levels.Count-1; i >=0; i--) {
			levels [i].road = this;
			}
	}
	private bool IsUnique(int code)
	{
		var countOfIndexes = levels.FindAll (x => x.UniqueCode == code);
		return countOfIndexes.Count == 1;
	}
	public RoadElement GetRoadElementByKey(int KeyCode)
	{
		return levels.Find (x => x.UniqueCode == KeyCode);
	}
	public List<RoadElement> GetRelativeLevels(int lastKey)
	{
		return levels.FindAll (x => x.RequieredLevels.FindIndex (b=>b.RoadKey==lastKey)!=-1);
	}
	public List<RoadElement> GetOpennedAndUnwinnedLevels(Saves.CharactaerData character)
	{
		return levels.FindAll (x => x.IsOpen(character)&&!x.IsWinned(character));
	}
	private int GetFirstLibreKey(List<RoadElement> list)
	{
		for (int i = 1; i < list.Count+1; i++) {			
			if (list.FindIndex (x => x.UniqueCode == i) == -1)
				return i;
		}
		return -1;
	}
}
