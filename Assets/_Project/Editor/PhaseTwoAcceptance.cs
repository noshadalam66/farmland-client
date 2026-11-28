using System;
using System.IO;
using FarmLand.Empire.World;
using UnityEngine;

namespace FarmLand.Empire.Editor
{
    public static class PhaseTwoAcceptance
    {
        public static void VerifyTerrain()
        {
            string directory = Environment.GetEnvironmentVariable("FARMLAND_TERRAIN_PACKAGE");
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException("Set FARMLAND_TERRAIN_PACKAGE to the exported 9 km² San Francisco package.");

            var package = TerrainPackage.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")));
            if (package.ChunksPerAxis != 3 || !package.HasWaterMask)
                throw new InvalidDataException("Expected nine terrain tiles with water masks.");

            const int samples = 33;
            var tiles = new Vector3[3, 3][];
            bool hasLand = false;
            bool hasWater = false;
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
            {
                var heights = File.ReadAllBytes(Path.Combine(directory, TerrainPackage.ChunkFilename(x, y)));
                var water = File.ReadAllBytes(Path.Combine(directory, TerrainPackage.WaterFilename(x, y)));
                Mesh mesh = package.CreateChunk(x, y, heights, water);
                try
                {
                    if (mesh.vertexCount != samples * samples || mesh.subMeshCount != 2)
                        throw new InvalidDataException($"Tile {x},{y} has unexpected geometry.");
                    tiles[x, y] = mesh.vertices;
                    hasLand |= mesh.GetIndexCount(0) > 0;
                    hasWater |= mesh.GetIndexCount(1) > 0;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }

                for (int i = 0; i < samples; i++)
                {
                    if (x > 0 && !tiles[x, y][i * samples].Equals(tiles[x - 1, y][i * samples + samples - 1]))
                        throw new InvalidDataException($"Tile {x},{y} has a mismatched west edge.");
                    if (y > 0 && !tiles[x, y][i].Equals(tiles[x, y - 1][(samples - 1) * samples + i]))
                        throw new InvalidDataException($"Tile {x},{y} has a mismatched north edge.");
                }
            }

            if (!hasLand || !hasWater)
                throw new InvalidDataException("Expected both land and water geometry.");
            Debug.Log("Phase 2 terrain mesh acceptance passed.");
        }
    }
}
