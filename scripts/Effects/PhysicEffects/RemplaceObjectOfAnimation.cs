using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RemplaceObjectOfAnimation : MonoBehaviour {
	[System.Serializable]
	public class Target
	{
		public GameObject obj;
		public GameObject prefab;
		public float sizeCoef = 1f;
	}
	public List<Target> targets = new List<Target>();
	public void OnValidate()
	{
		foreach (var t in targets)
			if (t.sizeCoef == 0)
				t.sizeCoef = 1f;
	}
	public void RemplaceObject(int number)
	{
		var go = Instantiate (targets[number].prefab, targets[number].obj.transform.transform.position, targets[number].obj.transform.rotation);
		go.transform.parent = null;
		go.transform.localScale=targets[number].obj.transform.lossyScale*targets[number].sizeCoef;
	}
	public void RemplaceObjectRandom(int RandomFrom)
	{
		RemplaceObject (Random.Range (RandomFrom, targets.Count));
	}
}
