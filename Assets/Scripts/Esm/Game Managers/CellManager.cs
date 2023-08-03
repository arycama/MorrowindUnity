using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages loading and unloading of scenes
/// </summary>
public class CellManager : Singleton<CellManager>
{
	//public static List<GameObject> StaticBatching = new List<GameObject>();

	[SerializeField, Tooltip("Additional cells to load in each direction around the player")]
	private int cellsToLoad = 1;

	[SerializeField]
	private CellRecord currentCell;

	private Scene InteriorCell;
	private Vector2Int coordinates;
	private Dictionary<CellRecord, Scene> loadedCells = new Dictionary<CellRecord, Scene>();

	// Called with the currently active scene when cells are finished loading
	public static event Action<CellRecord> OnFinishedLoadingCells;

	public static Transform Target { get; set; }

	// Returns a display-friendly name of an exterior cell, given a position
	public static string GetCellName(Vector3 position)
	{
		var coordinates = new Vector2Int(Mathf.FloorToInt(position.x / 8192), Mathf.FloorToInt(position.z / 8192));
		var cell = CellRecord.GetExteriorCell(coordinates);

		return string.IsNullOrEmpty(cell.Name) ? cell.Region.Name : cell.Name;
	}

	public static void LoadCell(string loadCell)
	{
		// Unload existing scenes
		foreach (var cell in Instance.loadedCells.Values)
			SceneManager.UnloadSceneAsync(cell);

		if (Instance.InteriorCell.IsValid())
			SceneManager.UnloadSceneAsync(Instance.InteriorCell);
		
		Instance.loadedCells.Clear();

		// Change this into an interface or something?
		if (string.IsNullOrEmpty(loadCell))
		{
			Instance.Start();
			Instance.enabled = true;
		}
		else
		{
			Instance.enabled = false;
			var cell = CellRecord.GetInteriorCell(loadCell);
			Instance.InteriorCell = LoadCell(cell);
			Instance.currentCell = cell;
			OnFinishedLoadingCells?.Invoke(cell);
		}
	}

	private static Scene LoadCell(CellRecord cell)
	{
		// Save currently active scene so it can be restored later
		var activeScene = SceneManager.GetActiveScene();

		var sceneName = GetUniqueCellName(cell);
		var newScene = SceneManager.CreateScene(sceneName);
		SceneManager.SetActiveScene(newScene);

		cell.CellData.LoadTerrain();
		//StaticBatching.Clear();

		// Load the cell objects
		foreach (var reference in cell.ReferenceData)
		{
			var creatableData = reference.ObjectId;
			var clone = creatableData.CreateGameObject(reference);

			// Levelled Creature records might not spawn a creature, so return
			if (clone != null)
			{
				reference.TransformData.SetTransformData(clone.transform);
				clone.transform.localScale = new Vector3(reference.Scale, reference.Scale, reference.Scale);
			}
		}

		//StaticBatchingUtility.Combine(StaticBatching.ToArray(), new GameObject("Static Batch Root"));

		// Set the original scene as the active scene. This stops weather from changing when new cells are loaded
		SceneManager.SetActiveScene(activeScene);

		return newScene;
	}

	private static string GetUniqueCellName(CellRecord cell)
	{
		if (cell.CellData.IsInterior)
			return cell.Name;

		if (string.IsNullOrEmpty(cell.Name))
			return $"{cell.Region.Name} {cell.CellData.Coordinates}";
		else
			return $"{cell.Name} {cell.CellData.Coordinates}";
	}

	private void Start()
	{
		// Code for loading multiple cells at once
		coordinates = new Vector2Int(Mathf.FloorToInt(Target.position.x / 8192), Mathf.FloorToInt(Target.position.z / 8192));

		loadedCells = LoadCells();
		OnFinishedLoadingCells?.Invoke(currentCell);
	}

	private void Update()
	{
		// Check if current cell has changed 
		var coordinates = new Vector2Int(Mathf.FloorToInt(Target.position.x / 8192), Mathf.FloorToInt(Target.position.z / 8192));

		if (this.coordinates == coordinates)
			return;

		this.coordinates = coordinates;

		// Load the new cells
		var newCells = LoadCells();

		// Unload any cells that are not in the new list of scenes
		foreach (var cell in loadedCells)
		{
			Scene scene;
			if (!newCells.TryGetValue(cell.Key, out scene))
				SceneManager.UnloadSceneAsync(cell.Value);
		}

		loadedCells = newCells;
	}

	private Dictionary<CellRecord, Scene> LoadCells()
	{
		var newCells = new Dictionary<CellRecord, Scene>();

		for (var x = -cellsToLoad; x <= cellsToLoad; x++)
		{
			for (var y = -cellsToLoad; y <= cellsToLoad; y++)
			{
				var coordinates = new Vector2Int(this.coordinates.x + x, this.coordinates.y + y);
				var cellRecord = CellRecord.GetExteriorCell(coordinates);

				// Check to see if this cell is already loaded, if so, don't re-load it, just add it to the new list of loaded cells
				Scene scene;
				if (!loadedCells.TryGetValue(cellRecord, out scene))
					scene = LoadCell(cellRecord);

				newCells.Add(cellRecord, scene);

				// If in the middle, set as current cell (Note, may be different to Unity's current cell
				if (x == 0 && y == 0)
					currentCell = cellRecord;
			}
		}

		return newCells;
	}
}