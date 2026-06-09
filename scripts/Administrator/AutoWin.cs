using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoWin : MonoBehaviour {

	public void Win()
	{
		ServiceLocator.winController.Win ();
	}
}
