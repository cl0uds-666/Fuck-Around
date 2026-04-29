using System.Collections.Generic;
using UnityEngine;

public class ForestSpawner : MonoBehaviour
{
    [Header("References")]
    public Transform train;
    public GameObject[] treePrefabs;

    [Header("Chunk Settings")]
    public float chunkLength = 25f;
    public int chunksBehind = 8;
    public int chunksAhead = 35;

    [Header("Forest Bands")]
    public float nearZMin = 18f;
    public float nearZMax = 35f;
    public float farZMin = 35f;
    public float farZMax = 90f;

    [Header("Density")]
    public int nearTreesPerSide = 3;
    public int farTreesPerSide = 10;

    [Header("Scale")]
    public float nearMinScale = 0.8f;
    public float nearMaxScale = 1.4f;
    public float farMinScale = 1.8f;
    public float farMaxScale = 3.5f;

    [Header("Placement")]
    public float treeY = 0f;

    private readonly Dictionary<int, GameObject> spawnedChunks = new Dictionary<int, GameObject>();

    private void Update()
    {
        if (train == null || treePrefabs == null || treePrefabs.Length == 0)
        {
            return;
        }

        int currentChunk = Mathf.FloorToInt(train.position.x / chunkLength);

        int startChunk = currentChunk - chunksBehind;
        int endChunk = currentChunk + chunksAhead;

        for (int i = startChunk; i <= endChunk; i++)
        {
            if (!spawnedChunks.ContainsKey(i))
            {
                SpawnChunk(i);
            }
        }

        List<int> chunksToRemove = new List<int>();

        foreach (KeyValuePair<int, GameObject> chunk in spawnedChunks)
        {
            if (chunk.Key < startChunk || chunk.Key > endChunk)
            {
                chunksToRemove.Add(chunk.Key);
            }
        }

        foreach (int index in chunksToRemove)
        {
            if (spawnedChunks[index] != null)
            {
                Destroy(spawnedChunks[index]);
            }

            spawnedChunks.Remove(index);
        }
    }

    private void SpawnChunk(int chunkIndex)
    {
        GameObject chunkParent = new GameObject("Forest Chunk " + chunkIndex);
        chunkParent.transform.SetParent(transform);

        float chunkStartX = chunkIndex * chunkLength;

        SpawnBand(chunkStartX, chunkParent.transform, nearTreesPerSide, nearZMin, nearZMax, nearMinScale, nearMaxScale);
        SpawnBand(chunkStartX, chunkParent.transform, farTreesPerSide, farZMin, farZMax, farMinScale, farMaxScale);

        spawnedChunks.Add(chunkIndex, chunkParent);
    }

    private void SpawnBand(
        float chunkStartX,
        Transform parent,
        int treeCountPerSide,
        float zMin,
        float zMax,
        float minScale,
        float maxScale
    )
    {
        SpawnSide(chunkStartX, parent, 1, treeCountPerSide, zMin, zMax, minScale, maxScale);
        SpawnSide(chunkStartX, parent, -1, treeCountPerSide, zMin, zMax, minScale, maxScale);
    }

    private void SpawnSide(
        float chunkStartX,
        Transform parent,
        int side,
        int treeCount,
        float zMin,
        float zMax,
        float minScale,
        float maxScale
    )
    {
        for (int i = 0; i < treeCount; i++)
        {
            GameObject prefab = treePrefabs[Random.Range(0, treePrefabs.Length)];

            float x = chunkStartX + Random.Range(0f, chunkLength);
            float z = Random.Range(zMin, zMax) * side;

            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            GameObject tree = Instantiate(
                prefab,
                new Vector3(x, treeY, z),
                rotation,
                parent
            );

            float scale = Random.Range(minScale, maxScale);
            tree.transform.localScale *= scale;
        }
    }
}