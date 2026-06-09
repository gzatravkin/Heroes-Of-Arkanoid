using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
public class SkillNumberParameter : Abstract_SkillParameter{
	[System.Serializable]
	public enum SkillParameterNumberType
	{
		line, curve, grid
	}

	public bool RoundValueToDescripcion;
	public SkillParameterNumberType skillParameterType;
	public Parameter_Grid Grid;
	public float startValue=0f;
	public float forLvlValue=1f;

	public AnimationCurve curve = AnimationCurve.Linear(0,0,10,1);
	public SkillNumberParameter()
	{
		Grid = new Parameter_Grid ();
		Grid.SmoothGrid = true;
	}

	public float GetFloat()
	{
		if (skillParameterType == SkillParameterNumberType.grid) {
			return Grid.GetGridValue (LvlActual);
		}
		if (skillParameterType == SkillParameterNumberType.curve)
			return curve.Evaluate (LvlActual);
		if (skillParameterType == SkillParameterNumberType.line)
			return startValue + (LvlActual-1) * forLvlValue;
		return 0f;
	}
	public int GetInt()
	{
		return Mathf.RoundToInt (GetFloat());
	}
	public override string ToString ()
	{
		return GetFloat ().ToString ();
	}
	public static implicit operator int(SkillNumberParameter v)  {  return v.GetInt();  }

	public static implicit operator float(SkillNumberParameter v) {  return v.GetFloat();  }
	public static implicit operator string(SkillNumberParameter v) 
	{  
		if (v.RoundValueToDescripcion)
			return v.GetInt ().ToString();
		else
			return v.GetFloat().ToString();  
	}
	public SkillNumberParameter(float lvl0,float lvl5, float lvl10, bool rounded=false)
	{
		var t = this;
		t.skillParameterType = SkillParameterNumberType.grid;
		t.Grid = new Parameter_Grid ();
		t.Grid.Data = new List<Parameter_Grid.LevelData> ();
		t.Grid.Data.Add (new Parameter_Grid.LevelData(0,lvl0));
		t.Grid.Data.Add (new Parameter_Grid.LevelData(5,lvl5));
		t.Grid.Data.Add (new Parameter_Grid.LevelData(10,lvl10));
		t.RoundValueToDescripcion = true;
		t.Grid.SmoothGrid = true;
        t.RoundValueToDescripcion = rounded;
	}

}
