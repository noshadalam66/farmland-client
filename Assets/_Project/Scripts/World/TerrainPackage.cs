using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FarmLand.Empire.World
{
    public sealed class TerrainPackage
    {
        private const int Samples = 33;
        private const float Spacing = 31.25f;
        private readonly int chunksPerAxis;

        private TerrainPackage(int chunksPerAxis, bool hasWaterMask)
        {
            this.chunksPerAxis = chunksPerAxis;
            HasWaterMask = hasWaterMask;
        }

        public int ChunksPerAxis => chunksPerAxis;
        public bool HasWaterMask { get; }

        public static TerrainPackage Parse(string json)
        {
            JObject manifest = JObject.Parse(json);
            int area = manifest.Value<int?>("areaKm2") ?? 0;
            int axis = area switch { 1 => 1, 4 => 2, 9 => 3, _ => 0 };
            if (axis == 0 || manifest.Value<int?>("version") != 1 ||
                manifest.Value<int?>("samplesPerChunkAxis") != Samples ||
                manifest.Value<double?>("sampleSpacingMeters") != Spacing ||
                manifest.Value<string>("encoding") != "float32-le" ||
                manifest["chunks"] is not JArray chunks || chunks.Count != area)
                throw new InvalidDataException("Unsupported terrain manifest.");

            JToken water = manifest["waterMask"];
            bool hasWaterMask = water != null;
            if (hasWaterMask && (water is not JObject mask ||
                                 mask.Value<string>("encoding") != "uint8" ||
                                 mask.Value<int?>("waterValue") != 1))
                throw new InvalidDataException("Unsupported terrain water mask.");

            var seen = new HashSet<(int, int)>();
            foreach (JToken entry in chunks)
            {
                if (entry is not JObject chunk)
                    throw new InvalidDataException("Invalid terrain chunk entry.");

                int x = chunk.Value<int?>("x") ?? -1;
                int y = chunk.Value<int?>("y") ?? -1;
                if (x < 0 || y < 0 || x >= axis || y >= axis || !seen.Add((x, y)) ||
                    chunk.Value<string>("file") != ChunkFilename(x, y) ||
                    (hasWaterMask && chunk.Value<string>("waterFile") != WaterFilename(x, y)))
                    throw new InvalidDataException("Invalid terrain chunk coordinates or filename.");
            }
            return new TerrainPackage(axis, hasWaterMask);
        }

        public static string ChunkFilename(int x, int y) => $"height-{x}-{y}.f32";

        public static string WaterFilename(int x, int y) => $"water-{x}-{y}.u8";

        public Mesh CreateChunk(int x, int y, byte[] contents, byte[] water = null)
        {
            if (x < 0 || y < 0 || x >= ChunksPerAxis || y >= ChunksPerAxis)
                throw new ArgumentOutOfRangeException(nameof(x), "Chunk is outside the world.");
            if (contents == null || contents.Length != Samples * Samples * sizeof(float))
                throw new InvalidDataException("The terrain chunk has an invalid byte count.");

            if (water != null && (!HasWaterMask || water.Length != Samples * Samples))
                throw new InvalidDataException("The terrain water mask has an invalid byte count.");

            var vertices = new Vector3[Samples * Samples];
            var uv = new Vector2[vertices.Length];
            int cells = Samples - 1;
            int worldCells = ChunksPerAxis * cells;
            float halfWidth = worldCells * Spacing / 2;
            for (int row = 0; row < Samples; row++)
            {
                int worldRow = y * cells + row;
                for (int column = 0; column < Samples; column++)
                {
                    int worldColumn = x * cells + column;
                    int index = row * Samples + column;
                    int bits = BinaryPrimitives.ReadInt32LittleEndian(contents.AsSpan(index * sizeof(float), sizeof(float)));
                    float height = BitConverter.Int32BitsToSingle(bits);
                    if (water != null && water[index] > 1)
                        throw new InvalidDataException("The terrain water mask contains an invalid value.");
                    if (float.IsNaN(height) || float.IsInfinity(height))
                        throw new InvalidDataException("The terrain chunk contains an invalid height.");

                    vertices[index] = new Vector3(worldColumn * Spacing - halfWidth, height, halfWidth - worldRow * Spacing);
                    uv[index] = new Vector2((float)worldColumn / worldCells, 1f - (float)worldRow / worldCells);
                }
            }

            var landTriangles = new List<int>(cells * cells * 6);
            var waterTriangles = water == null ? null : new List<int>();
            for (int row = 0; row < cells; row++)
            {
                for (int column = 0; column < cells; column++)
                {
                    int northwest = row * Samples + column;
                    int northeast = northwest + 1;
                    int southwest = northwest + Samples;
                    int southeast = southwest + 1;
                    AddTriangle(northwest, northeast, southwest, water, landTriangles, waterTriangles);
                    AddTriangle(northeast, southeast, southwest, water, landTriangles, waterTriangles);
                }
            }

            var mesh = new Mesh
            {
                name = ChunkFilename(x, y),
                vertices = vertices,
                uv = uv,
            };
            mesh.subMeshCount = water == null ? 1 : 2;
            mesh.SetTriangles(landTriangles, 0, false);
            if (waterTriangles != null) mesh.SetTriangles(waterTriangles, 1, false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddTriangle(int a, int b, int c, byte[] water,
            List<int> landTriangles, List<int> waterTriangles)
        {
            List<int> triangles = water != null && water[a] + water[b] + water[c] >= 2
                ? waterTriangles
                : landTriangles;
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }
    }
}
