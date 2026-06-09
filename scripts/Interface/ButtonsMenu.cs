using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class ButtonsMenu : MonoBehaviour {
	public GameObject listObject;
	public List<PalletButtonData> buttonData = new List<PalletButtonData>();
	public int selected=0;
	public bool ShowSelected=false;
	public bool ShowLocked=true;
	public UnityEvent_Int OnSelect = new UnityEvent_Int();
	private List<ButtonsMenu> buttonMenuGroup;
	public void SetGroup(List<ButtonsMenu> buttonMenuGroup)
	{
		this.buttonMenuGroup = buttonMenuGroup;
	}
	public void Initialization(List<PalletButtonData> data, int selectedNumber)
	{
		buttonData = data;
		Select (selectedNumber);
		RefreshIco ();
		var btn = GetComponent<Button> ();
		if (btn == null)
			btn = gameObject.AddComponent<Button> ();
		btn.onClick.AddListener (()=>Click ());
	}
	void Click()
	{
		if (listObject.transform.childCount > 0)
			HideList ();
		else
			ShowList ();
	}
	void RefreshIco()
	{		
		Debug.Log ("Resfresh ico " + selected.ToString());
		GetComponent<Image> ().sprite = buttonData [selected].Ico;		
	}
	public void ShowList ()
	{		
		if (buttonMenuGroup != null) {
			foreach (var menu in buttonMenuGroup)
				if (menu != this)
					menu.HideList ();
		}
		listObject.KillAllChilds ();
		listObject.SetActive (true);
		for (int i = 0; i < buttonData.Count; i++) {
			if (i == selected&&!ShowSelected)
				continue;
			if (!buttonData[i].IsOpenned()&&!ShowLocked)
				continue;
			var btn = new GameObject("Button");
			btn.AddComponent<Image> ().sprite = buttonData[i].Ico;
			var classButton= btn.AddComponent<Button> ();
			classButton.interactable = buttonData [i].IsOpenned();
			btn.SetParentWithScaleOne (listObject.transform);
			int number = i;
			classButton.onClick.AddListener (() => Select (number));
		}
	}
	void HideList()
	{
		listObject.SetActive (false);
		listObject.KillAllChilds ();
	}
	void Select(int number)
	{
		selected = number;
		RefreshIco ();
		buttonData [number].action.Invoke ();
		OnSelect.Invoke (number);
		HideList ();
	}
}
