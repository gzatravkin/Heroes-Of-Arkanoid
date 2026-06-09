using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeavensVaseScript : MonoBehaviour {
	public float LevelUpTime=15f;
	void Start()
	{
		GetComponent<BlockScript> ().OnDestoyed.AddListener (() => Die ());
	}
	protected void Die ()
	{		
		AbstractStatue.LevelUpToAll (LevelUpTime);
	}

}
