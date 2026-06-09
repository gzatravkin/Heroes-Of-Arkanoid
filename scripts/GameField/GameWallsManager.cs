using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GameWallsManager {
	public static Rect GameRect;
	public static GameObject WallsObject;
	public static readonly string LeftWallTag = "LeftWall";
	public static readonly string RightWallTag = "RightWall";
	public static readonly string TopWallTag = "TopWall";
	public static void Initialization()
	{
		var cam = Camera.main;
		var ySize = cam.orthographicSize;
		var xSize= ySize*cam.aspect;
		if (WallsObject == null)
			WallsObject = new GameObject ("Walls");
		else
			WallsObject.KillAllChilds ();
		var points = new Vector2[4];
		float xMin = -xSize+BattleFieldCameraController.FinalWidthOfInterface;
		points[0] = new Vector2 (xMin, -ySize);
		points [1] = new Vector2 (xMin, ySize);
		points [2] = new Vector2 (xSize, ySize);
		points [3] = new Vector2 (xSize, -ySize);
		CreateWall (points [0], points [1], "LeftWall");
		CreateWall (points [1], points [2], "TopWall");
		CreateWall (points [2], points [3], "RightWall");
		GameRect.xMin = xMin+cam.transform.position.x;
		GameRect.xMax = xSize+cam.transform.position.x;
		GameRect.yMin = -ySize+cam.transform.position.y;
		GameRect.yMax = ySize+cam.transform.position.y;
	}
	public static void CreateWall(Vector2 origen, Vector2 fin, string tag)
	{
		var cam = Camera.main;
		var Wall = new GameObject (tag);
		Wall.tag = tag;
		var rigi = Wall.AddComponent<Rigidbody2D> ();
		rigi.isKinematic = true;
		Wall.SetParentByName ("Walls");
		Wall.transform.position = cam.transform.position;
		var Edge = Wall.AddComponent<EdgeCollider2D> ();
		Edge.points = new Vector2[]{origen,fin};
	}
	public static Rect GetGameFieldRect()
	{
		return GameRect;
	}
}
