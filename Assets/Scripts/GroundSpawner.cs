using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class GroundSpawner : MonoBehaviour
{
    [Header("References")]
    public Transform train;
    public GameObject greyTilePrefab;
    public GameObject greenTilePrefab;

    [Header("Scenery")]
    public GameObject[] sceneryPrefabs;
    public int sceneryPerSidePerTile = 2;
    public float scenerySpawnChance = 0.7f;
    public float sceneryZMin = 12f;
    public float sceneryZMax = 25f;
    public float sceneryY = 0f;
    public float minScale = 0.8f;
    public float maxScale = 1.3f;

    [Header("Tile Settings")]
    public float spacing = 10f; // Unity plane default is 10x10 at scale 1
    public float greenZOffset = 10f;
    public int greenRowsPerSide = 5;

    [Header("Runtime Streaming")]
    public int tilesBehindTrain = 10;
    public int tilesAheadOfTrain = 60;

    [Header("Editor Preview")]
    public bool showInEditor = true;
    public int editorPreviewTiles = 100;

    private readonly Dictionary<int, GameObject> spawnedTiles = new Dictionary<int, GameObject>();

    private void Update()
    {
        if (greyTilePrefab == null || greenTilePrefab == null)
        {
            return;
        }

        if (!Application.isPlaying)
        {
            if (showInEditor)
            {
                ClearTiles();

                for (int i = 0; i < editorPreviewTiles; i++)
                {
                    SpawnTileSet(i);
                }
            }

            return;
        }

        if (train == null)
        {
            return;
        }

        StreamTiles();
    }

    private void StreamTiles()
    {
        int trainTileIndex = Mathf.FloorToInt(train.position.x / spacing);

        int startIndex = trainTileIndex - tilesBehindTrain;
        int endIndex = trainTileIndex + tilesAheadOfTrain;

        for (int i = startIndex; i <= endIndex; i++)
        {
            if (!spawnedTiles.ContainsKey(i))
            {
                SpawnTileSet(i);
            }
        }

        List<int> tilesToRemove = new List<int>();

        foreach (KeyValuePair<int, GameObject> tile in spawnedTiles)
        {
            if (tile.Key < startIndex || tile.Key > endIndex)
            {
                tilesToRemove.Add(tile.Key);
            }
        }

        foreach (int index in tilesToRemove)
        {
            if (spawnedTiles[index] != null)
            {
                Destroy(spawnedTiles[index]);
            }

            spawnedTiles.Remove(index);
        }
    }

    private void SpawnScenery(float tileX, Transform parent)
    {
        //sceneryZMax = greenZOffset * greenRowsPerSide;

        if (sceneryPrefabs == null || sceneryPrefabs.Length == 0)
        {
            return;
        }

        SpawnScenerySide(tileX, 1, parent);
        SpawnScenerySide(tileX, -1, parent);
    }

    private void SpawnScenerySide(float tileX, int side, Transform parent)
    {
        for (int i = 0; i < sceneryPerSidePerTile; i++)
        {
            if (Random.value > scenerySpawnChance)
            {
                continue;
            }

            GameObject prefab = sceneryPrefabs[Random.Range(0, sceneryPrefabs.Length)];

            float x = tileX + Random.Range(-spacing / 2f, spacing / 2f);
            float z = Random.Range(sceneryZMin, sceneryZMax) * side;

            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            GameObject scenery = Instantiate(
                prefab,
                new Vector3(x, sceneryY, z),
                rotation,
                parent
            );

            float scale = Random.Range(minScale, maxScale);
            scenery.transform.localScale *= scale;
        }
    }

    private void SpawnTileSet(int index)
    {
        float x = index * spacing;

        GameObject parent = new GameObject("Ground Tile Set " + index);
        parent.transform.SetParent(transform);
        parent.transform.position = Vector3.zero;

        Instantiate(greyTilePrefab, new Vector3(x, 0f, 0f), Quaternion.identity, parent.transform);
        //for (int i = 1; i <= greenRowsPerSide; i++)
        //{
        //    float zOffset = greenZOffset * i;

        //    Instantiate(greenTilePrefab, new Vector3(x, 0f, zOffset), Quaternion.identity, parent.transform);
        //    Instantiate(greenTilePrefab, new Vector3(x, 0f, -zOffset), Quaternion.identity, parent.transform);
        //}

        SpawnScenery(x, parent.transform);

        spawnedTiles.Add(index, parent);
    }

    private void ClearTiles()
    {
        List<GameObject> children = new List<GameObject>();

        foreach (Transform child in transform)
        {
            children.Add(child.gameObject);
        }

        foreach (GameObject child in children)
        {
            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }

        spawnedTiles.Clear();
    }
}