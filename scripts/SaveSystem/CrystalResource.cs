using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
public class CrystalResource {
	public int BlueCrystals;
	public int GreenCrystals;
	public int RedCrystals;
	public int YellowCrystals;
	public bool IsPartOf(CrystalResource other)
	{
		if (BlueCrystals <= other.BlueCrystals &&
		    GreenCrystals <= other.GreenCrystals &&
		    RedCrystals <= other.RedCrystals &&
		    YellowCrystals <= other.YellowCrystals)
			return true;
		else
			return false;
	}
	public CrystalResource(int Blue, int Green, int Red, int Yellow)
	{
		BlueCrystals = Blue;
		GreenCrystals= Green;
		RedCrystals = Red;
		YellowCrystals = Yellow;
	}
	public CrystalResource()
	{		
	}
	public static CrystalResource operator+(CrystalResource x,CrystalResource y)
	{
		return new CrystalResource (x.BlueCrystals+y.BlueCrystals,
			x.GreenCrystals+y.GreenCrystals
			,x.RedCrystals+y.RedCrystals
			,x.YellowCrystals+y.YellowCrystals);
	}

	public static CrystalResource operator*(int x,CrystalResource y)
	{
		return new CrystalResource (x*y.BlueCrystals,
			x*y.GreenCrystals
			,x*y.RedCrystals
			,x*y.YellowCrystals);
	}
	public static CrystalResource operator-(CrystalResource x,CrystalResource y)
	{
		return new CrystalResource (x.BlueCrystals-y.BlueCrystals,
			x.GreenCrystals-y.GreenCrystals
			,x.RedCrystals-y.RedCrystals
			,x.YellowCrystals-y.YellowCrystals);
	}
}
