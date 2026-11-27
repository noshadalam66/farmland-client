using System.Collections.Generic;
using UnityEngine;

namespace FarmLand.Empire.Prototype
{
    internal sealed class TerrainMeshLifetime : MonoBehaviour
    {
        private readonly List<Mesh> meshes = new List<Mesh>();

        internal void Add(Mesh mesh) => meshes.Add(mesh);

        private void OnDestroy()
        {
            foreach (Mesh mesh in meshes) Destroy(mesh);
        }
    }
}
