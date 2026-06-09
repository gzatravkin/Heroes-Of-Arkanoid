using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[AddComponentMenu("LevelEditor/Multy floor")]
public class MultiplyFloorLevelController : MonoBehaviour {
	public List<GameObject> Floors;
	// Use this for initialization
	void Start () {
		BattleEventsManager.Events.BlockDestroyed.AddListener ((x) => {Check();});
		for (int i = 1; i < Floors.Count; i++)
			Floors [i].SetActive (false);
	}
	void Awake()
	{
		var blockLoaders = GetComponentsInChildren<BlockLoader> (true);
		foreach (BlockLoader b in blockLoaders)
			b.Awake ();
	}
	public void OnValidate()
	{
		//for (int i = 0; i < Floors.Count; i++)
			//Floors [i].SetY (i * 8);
	}
	void Update()
	{
		Check ();
	}
	public void Check()
	{
		if (Floors [0].transform.childCount == 0) {
			Destroy (Floors [0]);
			Floors.RemoveAt (0);		
			if (Floors.Count > 0) {
				Floors [0].SetActive (true);
				//Floors [0].SetY (0);
			}
		}
		
	}
}
