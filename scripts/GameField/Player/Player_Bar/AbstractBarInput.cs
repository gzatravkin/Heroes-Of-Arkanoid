using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class AbstractBarInput : MonoBehaviour {
	public BarMove moveComponent;
	public BarLogic logicComponent;
	protected BattleFieldCameraController battleFieldCamera;
	protected virtual void Start()
	{		
		CompInitialization ();
		battleFieldCamera = Camera.main.GetComponent<BattleFieldCameraController> ();
	}
	// Update is called once per frame

	private void CompInitialization()
	{
		if (moveComponent == null)
			moveComponent = GetComponent<BarMove> ();
		if (logicComponent == null)
			logicComponent = GetComponent<BarLogic> ();
	}
	protected virtual void OnValidate()
	{
		CompInitialization ();
	}
}
