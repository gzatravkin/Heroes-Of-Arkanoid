using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SO_AbstractSkill),true)]
	public class SkillEditor : Editor {	

	public override void OnInspectorGUI ()
	{		
		serializedObject.Update ();
		var skill = ((SO_AbstractSkill)target);
		skill.SetLevel (skill.GetCurrentLevel());
		DrawDefaultInspector ();
		int DefaultParametrsCount = skill.GetDefaultParms ().Count;
		if (skill.NumerableFields.Count > 0) {
			EditorGUILayout.Separator ();
			var centeredStyle = GUI.skin.GetStyle ("Label");
			centeredStyle.alignment = TextAnchor.MiddleCenter;
			EditorGUILayout.LabelField ("Spell parametres", centeredStyle);
		}
		for (int i = 0; i < skill.NumerableFields.Count; i++) {						
			ShowNumberParameter ((SkillNumberParameter)skill.NumerableObjects [i],serializedObject.FindProperty(skill.NumerableFields[i].Name),DefaultParametrsCount+i);
		}
		EditorGUI.indentLevel++;
		EditorGUILayout.LabelField ("Spell descripcion in level " + skill.GetCurrentLevel());
		EditorGUI.indentLevel--;
		for (int i = 0; i < skill.NumerableFields.Count; i++) {	
			EditorGUILayout.LabelField (skill.NumerableFields[i].Name+"   "+skill.NumerableObjects[i]);
		}
		EditorGUILayout.LabelField ("Standart descripcion");
		EditorGUILayout.LabelField (skill.GetDescripcion());
		EditorGUILayout.LabelField ("Full descripcion");
		EditorGUILayout.TextArea (skill.GetFullDescripcion());
		serializedObject.ApplyModifiedProperties ();
	}
	void ShowNumberParameter(SkillNumberParameter parameterNumber, SerializedProperty property, int ParameterIndex)
	{			
		if (parameterNumber != null) {						
			EditorGUILayout.BeginHorizontal ();
			EditorGUILayout.LabelField (property.name +" {"+(ParameterIndex).ToString()+"}", EditorStyles.boldLabel);
			parameterNumber.skillParameterType = (SkillNumberParameter.SkillParameterNumberType) EditorGUILayout.EnumPopup (parameterNumber.skillParameterType);
			EditorGUILayout.EndHorizontal ();
			EditorGUI.indentLevel++;
			parameterNumber.RoundValueToDescripcion = EditorGUILayout.ToggleLeft ("Rounded", parameterNumber.RoundValueToDescripcion);
			if (parameterNumber.skillParameterType == SkillNumberParameter.SkillParameterNumberType.line) {
				EditorGUILayout.BeginVertical ();
				parameterNumber.startValue = EditorGUILayout.FloatField ("Начальное значение",parameterNumber.startValue);
				parameterNumber.forLvlValue = EditorGUILayout.FloatField ("Прибавка за уровень",parameterNumber.forLvlValue);
				EditorGUILayout.EndVertical ();
			}
			if (parameterNumber.skillParameterType == SkillNumberParameter.SkillParameterNumberType.curve) {				
				if (parameterNumber.curve.keys.Length == 0 && parameterNumber.curve.length != 10)
					parameterNumber.curve = AnimationCurve.Linear (0, 0, 10, 0);
					
				parameterNumber.curve = EditorGUILayout.CurveField(parameterNumber.curve);
			}
			if (parameterNumber.skillParameterType == SkillNumberParameter.SkillParameterNumberType.grid) {				
				var grid = property.FindPropertyRelative ("Grid");
				var Data = grid.FindPropertyRelative("Data");
				grid.isExpanded = EditorGUI.Foldout (EditorGUILayout.GetControlRect(),  grid.isExpanded, "Grid",true);
				if (grid.isExpanded) {										
					EditorGUI.indentLevel++;
					EditorGUILayout.PropertyField (grid.FindPropertyRelative("SmoothGrid"));
					EditorGUILayout.PropertyField (Data.FindPropertyRelative ("Array.size"));
					for (int i = 0; i < Data.arraySize; i++) {
						EditorGUILayout.BeginHorizontal ();
						var element = Data.GetArrayElementAtIndex (i);
						EditorGUILayout.PropertyField (element.FindPropertyRelative ("Level"));
						EditorGUILayout.PropertyField (element.FindPropertyRelative ("Value"), GUIContent.none);
						EditorGUILayout.EndHorizontal ();	
					}
					EditorGUI.indentLevel--;
				}
			}
			EditorGUI.indentLevel--;
		}
	}


	public static bool Foldout(bool foldout, GUIContent content, bool toggleOnLabelClick, GUIStyle style)
	{
		Rect position = GUILayoutUtility.GetRect(40f, 40f, 16f, 16f, style);
		// EditorGUI.kNumberW == 40f but is internal
		return EditorGUI.Foldout(position, foldout, content, toggleOnLabelClick, style);
	}
	/*
	//void ShowParameterNumberList(SerializedProperty list, List<SkillNumberParameter> realList)
	{
		EditorGUI.BeginChangeCheck ();
		SerializedProperty size = list.FindPropertyRelative("Array.size");
		list.isExpanded=EditorGUILayout.Foldout(list.isExpanded,"Spell NumberParametres",true,EditorStyles.foldout);
		if (!list.isExpanded)
			return;		
		EditorGUI.indentLevel++;
		EditorGUILayout.PropertyField(size);
		var showButtons = true;
		for (int i = 0; i < list.arraySize; i++) {
			if (i >= realList.Count)
				continue;						
			EditorGUILayout.LabelField ("Parameter {"+(2+i).ToString()+"}",EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			ShowNumberParameter(realList[i],list.GetArrayElementAtIndex(i));
			EditorGUILayout.BeginHorizontal();
			ShowButtons(list, i);
			EditorGUILayout.EndHorizontal();
			EditorGUI.indentLevel--;
			EditorGUILayout.Space ();
		}
		EditorGUI.indentLevel--;
	}
	//public static void ShowButtons (SerializedProperty list, int index) {
		if (GUILayout.Button(moveButtonContent, EditorStyles.miniButtonLeft, miniButtonWidth)) {
			list.MoveArrayElement(index, index + 1);
		}
		if (GUILayout.Button(duplicateButtonContent, EditorStyles.miniButtonMid, miniButtonWidth)) {
			list.InsertArrayElementAtIndex(index);
		}
		if (GUILayout.Button(deleteButtonContent, EditorStyles.miniButtonRight, miniButtonWidth)) {
			int oldSize = list.arraySize;
			list.DeleteArrayElementAtIndex(index);
			if (list.arraySize == oldSize) {
				list.DeleteArrayElementAtIndex(index);
			}
		}
	}
  */
}
