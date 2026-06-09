using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NecromantController : MonoBehaviour {	
	public int LayerNeeded;
	public GameObject ghostObj;
	public GameObject[] RebornObjects;
	void Start()
	{
			BattleEventsManager.Events.BlockDestroyed.AddListener(((arg0) => RecuperateBlock(arg0)));
	}
	public void RecuperateBlock(BlockScript block)	
	{
		var reborn = block.GetComponent<RevivibleByNecromant> ();
		if (reborn!=null&&block.gameObject.layer == LayerNeeded&&block.GetComponent<DeathMark>()==null) {
			block.gameObject.AddComponent<DeathMark> ();//отмечен смертью, не использовать его другими некромантами
			var obj = Instantiate (ghostObj, block.transform.position, block.transform.rotation);
			obj.GetComponent<RecuperableObject> ().obj = RebornObjects[reborn.revibileNumber];
		}
	}
}
