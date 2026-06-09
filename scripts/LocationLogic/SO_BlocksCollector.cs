using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName="ScriptableObject/BlockCollector"),System.Serializable]
public class SO_BlocksCollector : ScriptableObject {
	public SO_LocationData associatedLocation;
	public Sprite Ico;
	public GameObject[] Blocks;
}
