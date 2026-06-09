using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MyRigidBody2D:MonoBehaviour
{
	private Transform target;
	public Vector2 Velocity;
	public float AngularVelocity;
	public float AngularDrag = 1f;
	public float LineDrag = 1f;
	public void Update()
	{
		target.position += (Vector3)(Velocity*Time.deltaTime);
		Velocity += Time.deltaTime * new Vector2 (0, -10f);
		Velocity = Vector2.MoveTowards (Velocity, Vector2.zero, LineDrag * Time.deltaTime);
		AngularVelocity = Mathf.MoveTowards (AngularVelocity, 0, AngularDrag * Time.deltaTime);
		target.rotation = Quaternion.Euler(new Vector3 (0, 0, target.rotation.eulerAngles.z+AngularVelocity*Time.deltaTime));
	}
	public void Initialization(Transform target)	
	{
		this.target = target;
	}
}
