using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FonManager : MonoBehaviour {
	public static Sprite fonImage;
	private static FonManager ObjRef;
	public static FonManager objRef{
		get{			
			if (ObjRef == null)
				ObjRef = GameObject.FindObjectOfType<FonManager> ();
			return ObjRef;
		}
	}
	public Image target;
	public static void SetFon(Sprite FonImage)
	{
		fonImage = FonImage;
		objRef.RefreshFon ();
	}
	[RuntimeInitializeOnLoadMethod]
	public static void Initialization()
	{		
		if (GameObject.FindObjectOfType<FonManager> () == null) {
			GameObject fonManager = (GameObject)Instantiate (SO_Configuraciones.obj.fonManager.gameObject,Vector3.zero,Quaternion.identity);
			fonManager.SetParentByName ("MultyScenesManagers");
			DontDestroyOnLoad (GameObject.Find ("MultyScenesManagers"));
			ObjRef = fonManager.GetComponent<FonManager> ();
		}
	}
	void RefreshFon()
	{
		if (target.sprite != fonImage)
			target.sprite = fonImage;
	}
	// Use this for initialization
	void Start () {		
		if (ObjRef!=null&&ObjRef!=this)
			Destroy (this.gameObject);
	}

}
