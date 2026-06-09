using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PalletController : MonoBehaviour {
	public GameObject ButtonSample;
	public GameObject ChoisePanel;
	public Text  nameTranslation, descripcionTranslation;
	public Image imageTranslation;
	public void Initialization(string name,string descripcion, Sprite image, PalletButtonData[] data)
	{
		nameTranslation.text = name;
		descripcionTranslation.text = descripcion;
		if (imageTranslation != null)
			imageTranslation.sprite = image;
		if (ChoisePanel != null) {
			ChoisePanel.KillAllChilds ();
			foreach (var o in data) {
				var obj = Instantiate (ButtonSample, transform.position, transform.rotation) as GameObject;
				obj.SetParentWithScaleOne (ChoisePanel.transform);
				obj.GetComponentInChildren<Text> ().text = o.Name;
				if (o.action != null)
					obj.GetComponent<Button> ().onClick.AddListener (o.action);
				if (o.Ico != null)
					obj.GetComponent<Image> ().sprite = o.Ico;
				obj.GetComponent<Button> ().onClick.AddListener (() => Destroy (this.gameObject));
				obj.GetComponent<Button> ().interactable = o.IsOpenned ();
			}
		}
	}
}
