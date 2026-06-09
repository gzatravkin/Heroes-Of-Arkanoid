using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PartOfBlock : MonoBehaviour {	
	public int MaxBlocks = 50;
	public static int CurrentBlocksNumber = 0;
	public void Start()
	{
		CurrentBlocksNumber++;
		if (CurrentBlocksNumber>MaxBlocks) {
			Destroy (this.gameObject);
		}
	}
	static PartOfBlock()
	{
		CurrentBlocksNumber = 0;
	}
	public void OnDestroy()
	{
		CurrentBlocksNumber--;
	}
}
