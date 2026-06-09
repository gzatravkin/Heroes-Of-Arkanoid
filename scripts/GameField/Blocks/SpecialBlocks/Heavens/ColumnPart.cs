using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColumnPart : MonoBehaviour {
	public static List<ColumnPart> parts = new List<ColumnPart>();
	[System.Serializable]
	public class Column
	{
		public List<ColumnPart> parts = new List<ColumnPart>();
		public bool Sorted = false;
	}
	public static List<Column> Columns = new List<Column>();
	[System.Serializable]
	public enum PartType
	{
		bot,middle,top
	}
	public PartType partType;
	public Column column;
	public Vector3 wantedPosOfTop=new Vector3(0,1);
	public float SpeedMove=5f;
	public float minDistance=1f;
	public Sprite TopDamagedSprite;
	void Awake()
	{
		PartsInitialization ();
	}
	void Start()
	{
		ColumnInitialization ();
	}
	bool IsInterssect(ColumnPart col1, ColumnPart col2)
	{
		float sqrDistance = col1.minDistance * col1.minDistance*col1.transform.lossyScale.y;
		return (((Vector2)col1.transform.position - (Vector2)col2.transform.position).sqrMagnitude < sqrDistance);
	}
	void ColumnInitialization()
	{
		var partsActuales = parts.FindAll (x => x.gameObject.activeInHierarchy);
		var partsClosest = partsActuales.FindAll (x => IsInterssect(x,this));
		int ColumnNumber = -1;
		foreach (var o in partsClosest) {			
			ColumnNumber = Columns.FindIndex (x => x.parts.Find (y => y == o) != null);
			if (ColumnNumber!=-1)
				break;
		}
		if (ColumnNumber == -1) {
			column = new Column ();
			column.parts = partsClosest;
			Columns.Add (column);
		}
		else {			
			foreach (var o in partsClosest) {
				if (Columns [ColumnNumber].parts.Find (x=>x==o) == null)
					Columns [ColumnNumber].parts.Add (o);
			}
			column = Columns [ColumnNumber];
		}
		SortColumn (column);
	}
	void PartsInitialization()
	{
		parts.Add (this);
		parts.RemoveAll (x => x == null);
	}
	void SortColumn(Column column)
	{
		var bot = column.parts.Find (x => x.partType == PartType.bot);
		if (bot == null) {
			column.parts.Sort ((x, y) => Mathf.RoundToInt (x.transform.position.y - y.transform.position.y));
		} else {
			column.parts.Sort ((x, y) => ColumnPartComparer(bot,x,y));
		}
		column.Sorted = true;
	}
	int ColumnPartComparer(ColumnPart bot, ColumnPart col1, ColumnPart col2 )
	{
		if (col1==bot)
			return -1;
		if (col2 == bot)
			return 1;
		var distToBot1 = col1.transform.position.sqadDistance2D (bot.transform.position);
		var distToBot2 = col2.transform.position.sqadDistance2D (bot.transform.position);
		if (distToBot1 > distToBot2)
			return 1;
		if (distToBot1 < distToBot2)
			return -1;
		return 0;
	}
	void Initialization()
	{
		
	}
	public Vector3 GetWorldWantedPos()
	{
		return transform.position + (transform.rotation * wantedPosOfTop);
	}
	// Update is called once per frame
	void Update () {		
		bool BlockWasDestroyed = column.parts.Contains (null);
		if (BlockWasDestroyed)
		{
		column.parts.RemoveAll (x => x == null);
			if (column.parts.Count > 1) {
				var top = column.parts [column.parts.Count - 1];
				if (top.partType != PartType.top) {
					top.GetComponent<SpriteRenderer> ().sprite = TopDamagedSprite;
				}
			}
		}
		if (this != column.parts [0])
			return;//ONLY BOT WILL CALCULE
		for (int i = 1; i < column.parts.Count; i++) {
			Transform partTransform = column.parts [i].transform;
			Vector3 ToGo = column.parts [i - 1].GetWorldWantedPos ();
			partTransform.position = Vector3.MoveTowards(partTransform.position,ToGo,SpeedMove*TimeManager.GetDeltaTime(TimeType.ScaledTime));
		}
	}
}
