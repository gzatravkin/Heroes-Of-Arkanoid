using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CreateOnBallCollision : AbstractBallCollied {
	public GameObject prefab;
	public bool MakeChild=true;
	protected override void Hit ()
	{
		base.Hit ();
		var obj = Instantiate (prefab, transform.position, transform.rotation);
		obj.SetParentWithScaleOne (transform);
		if (!MakeChild)
			obj.transform.parent = transform.parent;
	}
}
