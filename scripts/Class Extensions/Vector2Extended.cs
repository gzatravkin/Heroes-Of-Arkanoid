using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Vector2Extended {
	public static float sqadDistance (this Vector2 vector2, Vector2 other)
	{		
		return (vector2.x - other.x) * (vector2.x - other.x) + (vector2.y - other.y) * (vector2.y - other.y);
	}
	public static float sqadDistance2D (this Vector3 vector3, Vector2 other)
	{		
		return (vector3.x - other.x) * (vector3.x - other.x) + (vector3.y - other.y) * (vector3.y - other.y);
	}
}
