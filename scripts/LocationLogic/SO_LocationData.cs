using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName="ScriptableObject/LocationData")]
public class SO_LocationData : ScriptableObject {
	public TextTranslation LocationName;
	public Sprite SPRITE_FON;
	public Sprite SPRITE_MISSION_SELECT_ICO;
	public int GetLocationIndex()
	{
		return SO_Configuraciones.obj.Locationes.FindIndex (x => x == this);
	}
}
