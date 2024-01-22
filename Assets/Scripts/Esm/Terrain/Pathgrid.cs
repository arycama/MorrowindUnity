using System;
using System.Collections.Generic;
using UnityEngine;

namespace Esm
{
	public class Pathgrid : EsmRecordCollection<Tuple<int, int>, Pathgrid>
	{
		public List<int> pathgridConnections = new List<int>();
		public PathgridPoint[] points;
		private PathgridData data;

		public string Id { get; private set; }
		public string Cellname { get; private set; }

		public override void Initialize(System.IO.BinaryReader reader, RecordHeader header)
		{
			while (reader.BaseStream.Position < header.DataEndPos)
			{
				var type = (SubRecordType)reader.ReadInt32();
				var size = reader.ReadInt32();

				switch (type)
				{
					case SubRecordType.Id:
						Cellname = reader.ReadString(size);
						break;
					case SubRecordType.Data:
						data = new PathgridData(reader);
						break;
					case SubRecordType.PathgridPoint:
						points = new PathgridPoint[data.PointCount];
						for (var i = 0; i < points.Length; i++)
						{
							points[i] = new PathgridPoint(reader);
						}
						break;
					case SubRecordType.PathgridConnection:
						for (var i = 0; i < size / 4; i++)
						{
							pathgridConnections.Add(reader.ReadInt32());
						}
						break;
				}
			}

			GenerateConnections();

			//pathGrids.Add(id, this);
			if (data.x != 0 && data.y != 0)
			{
				records.Add(new Tuple<int, int>(data.x, data.y), this);
			}
		}

		private void GenerateConnections()
		{
			var connectionIndex = 0;

			// Get connections for each point
			for (var i = 0; i < points.Length; i++)
			{
				// Get the next connection until we have all the connections for this point
				for (var j = 0; j < points[i].connectionNum; j++)
				{
					var connectedPointIndex = pathgridConnections[connectionIndex];
					var connectedPoint = points[connectedPointIndex];
					points[i].AddConnection(connectedPoint);
					connectionIndex++;
				}
			}
		}

		public static Pathgrid LoadPathGrid(Vector3 position)
		{
			var coordinates = new Tuple<int, int>(Mathf.FloorToInt(position.x / 8192), Mathf.FloorToInt(position.z / 8192));

			Pathgrid pathgrid;
			if (!records.TryGetValue(coordinates, out pathgrid))
			{
				return null;
				//throw new KeyNotFoundException(coordinates.ToString());
			}

			return pathgrid;
		}

		/// <summary>
		/// Given a current position, finds the closest point in this path grid
		/// </summary>
		/// <param name="position"> The position (In world space) </param>
		/// <returns> The pathgrid point closest to the position </returns>
		public PathgridPoint GetNearestPoint(Vector3 position)
		{
			float distance = float.MaxValue;
			PathgridPoint targetPoint = null;

			// Convert position to world space
			var offset = new Vector3(8192 * data.x, 0, 8192 * data.y);

			// Loop through EVERY path grid point and see which one is closest
			// Uses square magnitude for speed
			foreach (var point in points)
			{
				var worldPosition = point.Position + offset;
				var sqrDistance = (worldPosition - position).sqrMagnitude;

				if (sqrDistance < distance)
				{
					targetPoint = point;
					distance = sqrDistance;
				}
			}

			return targetPoint;
		}

		[Serializable]
		private class PathgridData
		{
			public int x, y;
			private readonly short granularity;

			public short PointCount { get; private set; }

			public PathgridData(System.IO.BinaryReader reader)
			{
				x = reader.ReadInt32();
				y = reader.ReadInt32();
				granularity = reader.ReadInt16();
				PointCount = reader.ReadInt16();
			}
		}
	}
}