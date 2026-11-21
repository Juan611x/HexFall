using System.Collections.Generic;
using HexFall.Surfaces;
using NUnit.Framework;
using UnityEngine;

namespace HexFall.UI.Tests
{
    public class MeshSurfaceResolverTests
    {
        readonly List<Object> created = new List<Object>();
        SurfaceRegistry registry;
        SurfaceDefinition stone;
        SurfaceDefinition brick;
        SurfaceDefinition fallback;
        Texture2D stoneTex;
        Texture2D brickTex;
        MeshSurfaceResolver resolver;

        [SetUp]
        public void SetUp()
        {
            registry = Track(ScriptableObject.CreateInstance<SurfaceRegistry>());
            stone = Track(ScriptableObject.CreateInstance<SurfaceDefinition>());
            brick = Track(ScriptableObject.CreateInstance<SurfaceDefinition>());
            fallback = Track(ScriptableObject.CreateInstance<SurfaceDefinition>());
            stoneTex = Track(new Texture2D(1, 1));
            brickTex = Track(new Texture2D(1, 1));
            registry.SetEntries(new[]
            {
                new SurfaceTextureEntry { texture = stoneTex, surface = stone },
                new SurfaceTextureEntry { texture = brickTex, surface = brick },
            }, fallback);
            resolver = new MeshSurfaceResolver(registry);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in created) Object.DestroyImmediate(obj);
            created.Clear();
        }

        T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        Material MakeMaterial(Texture texture)
        {
            Shader shader = null;
            foreach (var shaderName in new[] { "Universal Render Pipeline/Lit", "HexFall/PSX Lit", "Sprites/Default" })
            {
                shader = Shader.Find(shaderName);
                if (shader != null) break;
            }
            var material = Track(new Material(shader));
            material.mainTexture = texture;
            return material;
        }

        GameObject MakeCube(Material material)
        {
            var cube = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
            return cube;
        }

        // One triangle per submesh keeps the triangle indices obvious.
        GameObject MakeTwoSubMeshObject(Material first, Material second)
        {
            var mesh = Track(new Mesh());
            mesh.SetVertices(new[]
            {
                new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 0, 0),
                new Vector3(2, 0, 0), new Vector3(2, 1, 0), new Vector3(3, 0, 0),
            });
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.SetTriangles(new[] { 3, 4, 5 }, 1);

            var go = Track(new GameObject("TwoSubMeshes"));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { first, second };
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        [Test]
        public void Resolve_BoxCollider_UsesSharedMaterial()
        {
            var cube = MakeCube(MakeMaterial(brickTex));

            Assert.AreSame(brick, resolver.Resolve(cube.GetComponent<Collider>(), -1));
        }

        [Test]
        public void Resolve_MultiSubMeshMeshCollider_PicksSubMeshMaterial()
        {
            var go = MakeTwoSubMeshObject(MakeMaterial(stoneTex), MakeMaterial(brickTex));
            var collider = go.GetComponent<Collider>();

            Assert.AreSame(stone, resolver.Resolve(collider, 0));
            Assert.AreSame(brick, resolver.Resolve(collider, 1));
        }

        [Test]
        public void Resolve_MeshColliderWithoutTriangle_UsesFirstMaterial()
        {
            var go = MakeTwoSubMeshObject(MakeMaterial(stoneTex), MakeMaterial(brickTex));

            Assert.AreSame(stone, resolver.Resolve(go.GetComponent<Collider>(), -1));
        }

        [Test]
        public void Resolve_SubMeshBeyondMaterials_ReturnsDefault()
        {
            var go = MakeTwoSubMeshObject(MakeMaterial(stoneTex), MakeMaterial(brickTex));
            go.GetComponent<MeshRenderer>().sharedMaterials = new[] { MakeMaterial(stoneTex) };

            Assert.AreSame(fallback, resolver.Resolve(go.GetComponent<Collider>(), 1));
        }

        [Test]
        public void Resolve_ColliderWithoutRenderer_ReturnsDefault()
        {
            var go = Track(new GameObject("NoRenderer"));
            var collider = go.AddComponent<BoxCollider>();

            Assert.AreSame(fallback, resolver.Resolve(collider, -1));
        }

        [Test]
        public void Resolve_RendererOnParent_IsUsed()
        {
            var parent = MakeCube(MakeMaterial(stoneTex));
            Object.DestroyImmediate(parent.GetComponent<Collider>());
            var child = Track(new GameObject("Child"));
            child.transform.SetParent(parent.transform);
            var collider = child.AddComponent<BoxCollider>();

            Assert.AreSame(stone, resolver.Resolve(collider, -1));
        }

        [Test]
        public void Resolve_UnmappedTexture_ReturnsDefault()
        {
            var cube = MakeCube(MakeMaterial(Track(new Texture2D(1, 1))));

            Assert.AreSame(fallback, resolver.Resolve(cube.GetComponent<Collider>(), -1));
        }

        [Test]
        public void Resolve_MaterialWithoutTexture_ReturnsDefault()
        {
            var cube = MakeCube(MakeMaterial(null));

            Assert.AreSame(fallback, resolver.Resolve(cube.GetComponent<Collider>(), -1));
        }

        [Test]
        public void Resolve_NullCollider_ReturnsDefault()
        {
            Assert.AreSame(fallback, resolver.Resolve(null, 0));
        }
    }
}
