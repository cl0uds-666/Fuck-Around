using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class TrackSpawner : MonoBehaviour
{
    [Header("References")]
    public Transform train;
    public GameObject trackPrefab;

    [Header("Track Settings")]
    public int visibleTrackPieces = 50;
    public float spacing = 4f;
    public Vector3 direction = Vector3.right;

    [Header("Runtime Streaming")]
    public int piecesBehindTrain = 10;
    public int piecesAheadOfTrain = 60;

    [Header("Editor Preview")]
    public bool showInEditor = true;
    public int editorPreviewPieces = 100;

    private readonly Dictionary<int, GameObject> spawnedPieces = new Dictionary<int, GameObject>();

    private void Update()
    {
        if (trackPrefab == null)
        {
            return;
        }

        if (!Application.isPlaying)
        {
            if (showInEditor)
            {
                SpawnEditorPreview();
            }

            return;
        }

        if (train == null)
        {
            return;
        }

        StreamTrack();
    }

    private void SpawnEditorPreview()
    {
        ClearTrack();

        for (int i = 0; i < editorPreviewPieces; i++)
        {
            SpawnPiece(i);
        }
    }

    private void StreamTrack()
    {
        int trainPieceIndex = Mathf.FloorToInt(train.position.x / spacing);

        int startIndex = trainPieceIndex - piecesBehindTrain;
        int endIndex = trainPieceIndex + piecesAheadOfTrain;

        for (int i = startIndex; i <= endIndex; i++)
        {
            if (!spawnedPieces.ContainsKey(i))
            {
                SpawnPiece(i);
            }
        }

        List<int> piecesToRemove = new List<int>();

        foreach (KeyValuePair<int, GameObject> piece in spawnedPieces)
        {
            if (piece.Key < startIndex || piece.Key > endIndex)
            {
                piecesToRemove.Add(piece.Key);
            }
        }

        foreach (int index in piecesToRemove)
        {
            if (spawnedPieces[index] != null)
            {
                Destroy(spawnedPieces[index]);
            }

            spawnedPieces.Remove(index);
        }
    }

    private void SpawnPiece(int index)
    {
        Vector3 position = transform.position + direction.normalized * spacing * index;

        GameObject piece = Instantiate(trackPrefab, position, Quaternion.identity, transform);

        spawnedPieces.Add(index, piece);
    }

    private void ClearTrack()
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

        spawnedPieces.Clear();
    }
}