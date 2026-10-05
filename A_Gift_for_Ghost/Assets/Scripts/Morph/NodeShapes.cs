using UnityEngine;

namespace Ghost.Morph
{
    // 节点在几何植株 / 写实形态下的形状。抽象形态全部是立方体；
    // 越接近写实，叶、果实、花苞、虫子换成各自的低多边形形状（运行时生成，平面着色，边长 1 的单位体）。
    public static class NodeShapes
    {
        public enum Shape
        {
            // 始终是立方体：土、根、茎
            Cube = -1,
            // 拉长、压扁后是菱形叶片；也用于花苞
            Octahedron = 0,
            // 果实、虫子
            Icosphere = 1,
        }

        public const int ShapeCount = 2;

        static Mesh octahedron;
        static Mesh icosphere;

        public static Shape ShapeOf(Organ organ)
        {
            switch (organ)
            {
                case Organ.Leaf:
                case Organ.Bud:
                    return Shape.Octahedron;
                case Organ.Fruit:
                case Organ.Bug:
                    return Shape.Icosphere;
                default:
                    return Shape.Cube;
            }
        }

        // 各形态用多少"部位形状"：0 = 全是立方体，1 = 全是部位形状
        public static float ShapeWeight(MorphForm form)
        {
            return form == MorphForm.Geometric || form == MorphForm.Real ? 1f : 0f;
        }

        public static Mesh GetMesh(Shape shape)
        {
            switch (shape)
            {
                case Shape.Octahedron:
                    if (octahedron == null) octahedron = BuildOctahedron();
                    return octahedron;
                case Shape.Icosphere:
                    if (icosphere == null) icosphere = BuildIcosphere();
                    return icosphere;
                default:
                    return null;
            }
        }

        static Mesh BuildOctahedron()
        {
            var v = new[]
            {
                new Vector3(0.5f, 0f, 0f), new Vector3(-0.5f, 0f, 0f),
                new Vector3(0f, 0.5f, 0f), new Vector3(0f, -0.5f, 0f),
                new Vector3(0f, 0f, 0.5f), new Vector3(0f, 0f, -0.5f),
            };
            var faces = new[]
            {
                0, 2, 4, 0, 4, 3, 0, 3, 5, 0, 5, 2,
                1, 4, 2, 1, 3, 4, 1, 5, 3, 1, 2, 5,
            };
            return BuildFlatMesh("NodeOctahedron", v, faces);
        }

        // 二十面体细分一次（80 个面），半径 0.5
        static Mesh BuildIcosphere()
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var basis = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            var ico = new[]
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            var vertices = new System.Collections.Generic.List<Vector3>();
            foreach (var p in basis) vertices.Add(p.normalized * 0.5f);
            var faces = new System.Collections.Generic.List<int>();
            for (int i = 0; i < ico.Length; i += 3)
            {
                int a = ico[i], b = ico[i + 1], c = ico[i + 2];
                int ab = Midpoint(vertices, a, b), bc = Midpoint(vertices, b, c), ca = Midpoint(vertices, c, a);
                faces.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }
            return BuildFlatMesh("NodeIcosphere", vertices.ToArray(), faces.ToArray());
        }

        static int Midpoint(System.Collections.Generic.List<Vector3> vertices, int a, int b)
        {
            vertices.Add(((vertices[a] + vertices[b]) * 0.5f).normalized * 0.5f);
            return vertices.Count - 1;
        }

        // 每个面独立顶点 + 面法线，得到棱角分明的低多边形效果；绕序统一成朝外
        static Mesh BuildFlatMesh(string name, Vector3[] points, int[] faces)
        {
            int count = faces.Length;
            var vertices = new Vector3[count];
            var normals = new Vector3[count];
            var indices = new int[count];
            for (int i = 0; i < count; i += 3)
            {
                Vector3 a = points[faces[i]], b = points[faces[i + 1]], c = points[faces[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, a + b + c) < 0f)
                {
                    (b, c) = (c, b);
                    n = -n;
                }
                n.Normalize();
                vertices[i] = a;
                vertices[i + 1] = b;
                vertices[i + 2] = c;
                normals[i] = normals[i + 1] = normals[i + 2] = n;
                indices[i] = i;
                indices[i + 1] = i + 1;
                indices[i + 2] = i + 2;
            }

            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = indices;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
