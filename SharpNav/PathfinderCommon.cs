﻿#region License
/**
 * Copyright (c) 2013-2014 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
 * Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE
 */
#endregion

using System;
using System.Collections.Generic;

using SharpNav.Geometry;

#if MONOGAME || XNA
using Microsoft.Xna.Framework;
#elif OPENTK
using OpenTK;
#elif SHARPDX
using SharpDX;
#endif

namespace SharpNav
{
	/// <summary>
	/// Store constants, structs, methods in this single class so that other classes can access this information.
	/// </summary>
	public class PathfinderCommon
	{
		public const int VERTS_PER_POLYGON = 6; //max number of vertices

		//tile serialization constants used to detect compatibility of navigation tile data
		//public const int NAVMESH_MAGIC = 'D' << 24 | 'N' << 16 | 'A' << 8 | 'V'; //magic number 
		//public const int NAVMESH_VERSION = 7; //version number

		public const int EXT_LINK = 0x8000; //entity links to external entity
		public const int NULL_LINK = unchecked((int)0xffffffff); //doesn't link to anything

		public const int MAX_AREAS = 64; //max number of user defined area ids

		public const int STRAIGHTPATH_START = 0x01; //vertex is in start position of path
		public const int STRAIGHTPATH_END = 0x02; //vertex is in end position of path
		public const int STRAIGHTPATH_OFFMESH_CONNECTION = 0x04; //vertex is at start of an off-mesh connection

		public const int STRAIGHTPATH_AREA_CROSSINGS = 0x01; //add a vertex at each polygon edge crossing where area changes
		public const int STRAIGHTPATH_ALL_CROSSINGS = 0x02; //add a vertex at each polygon edge crossing

		public const int OFFMESH_CON_BIDIR = 1; //bidirectional

		public static bool OverlapQuantBounds(Vector3 amin, Vector3 amax, Vector3 bmin, Vector3 bmax)
		{
			bool overlap = true;
			overlap = (amin.X > bmax.X || amax.X < bmin.X) ? false : overlap;
			overlap = (amin.Y > bmax.Y || amax.Y < bmin.Y) ? false : overlap;
			overlap = (amin.Z > bmax.Z || amax.Z < bmin.Z) ? false : overlap;
			return overlap;
		}

		public static bool DistancePointPolyEdgesSquare(Vector3 pt, Vector3[] verts, int nverts, float[] ed, float[] et)
		{
			bool c = false;

			for (int i = 0, j = nverts - 1; i < nverts; j = i++)
			{
				Vector3 vi = verts[i];
				Vector3 vj = verts[j];
				if (((vi.Z > pt.Z) != (vj.Z > pt.Z)) &&
					(pt.X < (vj.X - vi.X) * (pt.Z - vi.Z) / (vj.Z - vi.Z) + vi.X))
				{
					c = !c;
				}

				ed[j] = MathHelper.Distance.PointToSegment2DSquared(ref pt, ref vj, ref vi, out et[j]);
			}

			return c;
		}

		public static bool ClosestHeightPointTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c, ref float h)
		{
			Vector3 v0 = c - a;
			Vector3 v1 = b - a;
			Vector3 v2 = p - a;

			float dot00, dot01, dot02, dot11, dot12;

			Vector3Extensions.Dot2D(ref v0, ref v0, out dot00);
			Vector3Extensions.Dot2D(ref v0, ref v1, out dot01);
			Vector3Extensions.Dot2D(ref v0, ref v2, out dot02);
			Vector3Extensions.Dot2D(ref v1, ref v1, out dot11);
			Vector3Extensions.Dot2D(ref v1, ref v2, out dot12);

			//compute barycentric coordinates
			float invDenom = 1.0f / (dot00 * dot11 - dot01 * dot01);
			float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
			float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

			float EPS = 1E-4f;

			//if point lies inside triangle, return interpolated y-coordinate
			if (u >= -EPS && v >= -EPS && (u + v) <= 1 + EPS)
			{
				h = a.Y + v0.Y * u + v1.Y * v;
				return true;
			}

			return false;
		}

		public static void RandomPointInConvexPoly(Vector3[] pts, int npts, float[] areas, float s, float t, ref Vector3 pt)
		{
			//calculate triangle areas
			float areaSum = 0.0f;
			float area;
			for (int i = 2; i < npts; i++)
			{
				Triangle3.Area2D(ref pts[0], ref pts[i - 1], ref pts[i], out area);
				areaSum += Math.Max(0.001f, area);
				areas[i] = area;
			}

			//find sub triangle weighted by area
			float thr = s * areaSum;
			float acc = 0.0f;
			float u = 0.0f;
			int tri = 0;
			for (int i = 2; i < npts; i++)
			{
				float dacc = areas[i];
				if (thr >= acc && thr < (acc + dacc))
				{
					u = (thr - acc) / dacc;
					tri = i;
					break;
				}

				acc += dacc;
			}

			float v = (float)Math.Sqrt(t);

			float a = 1 - v;
			float b = (1 - u) * v;
			float c = u * v;
			Vector3 pa = pts[0];
			Vector3 pb = pts[tri - 1];
			Vector3 pc = pts[tri];

			pt.X = a * pa.X + b * pb.X + c * pc.X;
			pt.Y = a * pa.Y + b * pb.Y + c * pc.Y;
			pt.Z = a * pa.Z + b * pb.Z + c * pc.Z;
		}

		public class MeshHeader
		{
			//public int magic; //tile magic number (used to identify data format)
			//public int version;
			public int x;
			public int y;
			public int layer;
			public uint userId;
			public int polyCount;
			public int vertCount;
			public int maxLinkCount;
			public int detailMeshCount;

			public int detailVertCount;

			public int detailTriCount;
			public int bvNodeCount;
			public int offMeshConCount;
			public int offMeshBase; //index of first polygon which is off-mesh connection
			public float walkableHeight;
			public float walkableRadius;
			public float walkableClimb;
			public BBox3 bounds;

			public float bvQuantFactor; //bounding volume quantization facto
		}

		public class Poly
		{
			public int firstLink; //index to first link in linked list
			public int[] verts; //indices of polygon's vertices
			public int[] neis; //packed data representing neighbor polygons references and flags for each edge
			public int flags; //user defined polygon flags
			public int vertCount;
			public int areaAndtype = 0; //bit packed area id and polygon type

			public void SetArea(int a)
			{
				areaAndtype = (areaAndtype & 0xc0) | (a & 0x3f);
			}

			public void SetType(PolygonType t)
			{
				areaAndtype = (areaAndtype & 0x3f) | ((int)t << 6);
			}

			public int GetArea()
			{
				return areaAndtype & 0x3f;
			}

			public PolygonType GetPolyType()
			{
				return (PolygonType)(areaAndtype >> 6);
			}
		}

		public struct PolyDetail
		{
			public uint vertBase; //offset of vertices in some array
			public uint triBase; //offset of triangles in some array
			public int vertCount;
			public int triCount;
		}

		public struct OffMeshConnection
		{
			public Vector3[] pos; //the endpoints of the connection
			public float radius;
			public int poly;
			public int flags; //assigned flag from Poly
			public int side; //endpoint side
			public uint userId; //id of offmesh connection
		}

		public struct BVNode
		{
			public BBox3 bounds;
			public int index;
		}

		public struct NavMeshParams
		{
			public Vector3 origin;
			public float tileWidth;
			public float tileHeight;
			public int maxTiles;
			public int maxPolys;
		}

		public class Link
		{
			public int reference; //neighbor reference (the one it's linked to)
			public int next; //index of next link
			public int edge; //index of polygon edge
			public int side;
			public int bmin;
			public int bmax;
		}

		public class MeshTile
		{
			public int salt; //counter describing modifications to the tile

			public int linksFreeList; //index to the next free link
			public PathfinderCommon.MeshHeader header;
			public PathfinderCommon.Poly[] polys;
			public Vector3[] verts;
			public Link[] links;
			public PathfinderCommon.PolyDetail[] detailMeshes;

			public Vector3[] detailVerts;
			public PolyMeshDetail.TriangleData[] detailTris;

			public PathfinderCommon.BVNode[] bvTree; //bounding volume nodes

			public PathfinderCommon.OffMeshConnection[] offMeshCons;

			public NavMeshBuilder data;
			public MeshTile next;
		}
	}
}
