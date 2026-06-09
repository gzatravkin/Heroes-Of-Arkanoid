using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
public class Parameter_Grid {
	[System.Serializable]
	public class LevelData
	{
		public int Level;
		public float Value;
		public LevelData()
		{
		}
		public LevelData(int Level,float Value)
		{
			this.Level=Level;
			this.Value=Value;
		}
	}

	public List<LevelData> Data;
	public bool SmoothGrid;
	public float GetGridValue(int Level)
	{
		if (Data == null)
			Data = new List<LevelData> ();
		Data.Sort ((x, y) => x.Level - y.Level);
		if (Data.Count == 0)
			Data.Add (new LevelData ());
		int indexFrom = Data.Count-1;
		int indexTo = indexFrom;
		for (int i = 1; i < Data.Count; i++) {
			if (Data[i].Level > Level) {				
				indexFrom = i - 1;
				indexTo = i;	
				break;
			}
		}
		if (!SmoothGrid)
			return Data[indexFrom].Value;	
		if (indexFrom == indexTo)
			return Data [indexTo].Value;
		float origen = Data[indexFrom].Level;
		float fin = Data [indexTo].Level;
		float pos = Level;
		float lerpValue = (pos-origen)/(fin-origen);
		float returnValue = Mathf.Lerp (Data[indexFrom].Value, Data[indexTo].Value, lerpValue);
		return returnValue;
	}
}
