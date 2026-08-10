using System.IO;
using System.Text;
using UnityEngine;

public static class BinaryReaderExtensions
{
	public static ushort[] ReadTriangles(this BinaryReader reader, int count)
	{
		var triangles = new ushort[count];
		for (var i = 0; i < count; i += 3)
		{
			triangles[i] = reader.ReadUInt16();
			triangles[i + 2] = reader.ReadUInt16();
			triangles[i + 1] = reader.ReadUInt16();
		}

		return triangles;
	}

	public static sbyte[] ReadSByteArray(this BinaryReader reader, int length)
	{
		var bytes = new sbyte[length];
		for(var i = 0; i < length; i++)
			bytes[i] = reader.ReadSByte();
		return bytes;
	}

	public static string ReadString(this BinaryReader reader, int length) => Encoding.ASCII.GetString(reader.ReadBytes(length)).TrimEnd('\0');

	public static string ReadLengthPrefixedString(this BinaryReader reader) => reader.ReadString(reader.ReadInt32());

	public static Color ReadColor3(this BinaryReader reader) => new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

	public static Color GetReadColor4(this BinaryReader reader) => new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

	public static Color[] ReadColor4Array(this BinaryReader reader, int length)
	{
		var colors = new Color[length];
		for (var i = 0; i < colors.Length; i++)
			colors[i] = reader.GetReadColor4();
		return colors;
	}

	public static Color32 ReadColor323(this BinaryReader reader) => new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), 1);

	public static Color32 ReadColor32(this BinaryReader reader) => new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte());

	public static Color32[] ReadColor323Array(this BinaryReader reader, int length)
	{
		var colors = new Color32[length];
		for(var i = 0; i < length; i++)
			colors[i] = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), 1);
		return colors;
	}

	public static Matrix4x4 ReadMatrix(this BinaryReader reader) => new Matrix4x4()
	{
		m00 = reader.ReadSingle(),
		m02 = reader.ReadSingle(),
		m01 = reader.ReadSingle(),
		m20 = reader.ReadSingle(),
		m22 = reader.ReadSingle(),
		m21 = reader.ReadSingle(),
		m10 = reader.ReadSingle(),
		m12 = reader.ReadSingle(),
		m11 = reader.ReadSingle(),
		m33 = 1
	};

	public static Quaternion ReadQuaternion(this BinaryReader reader) => new Quaternion() { w = -reader.ReadSingle(), x = reader.ReadSingle(), z = reader.ReadSingle(), y = reader.ReadSingle() };

	public static Quaternion ReadRotation(this BinaryReader reader)
	{
		var m00 = reader.ReadSingle();
		var m02 = reader.ReadSingle();
		var m01 = reader.ReadSingle();

		var m20 = reader.ReadSingle();
		var m22 = reader.ReadSingle();
		var m21 = reader.ReadSingle();

		var m10 = reader.ReadSingle();
		var m12 = reader.ReadSingle();
		var m11 = reader.ReadSingle();

		return new Matrix4x4(new(m00, m10, m20, 0.0f), new(m01, m11, m21, 0.0f), new(m02, m12, m22, 0.0f), new(0.0f, 0.0f, 0.0f, 1.0f)).rotation;
	}

	public static Vector3 ReadVector3(this BinaryReader reader) => new Vector3(){ x = reader.ReadSingle(), z = reader.ReadSingle(), y = reader.ReadSingle() };

	public static Vector3 ReadEulerAngle(this BinaryReader reader) => new Vector3() { x = reader.ReadSingle() * Mathf.Rad2Deg, z = reader.ReadSingle() * Mathf.Rad2Deg, y = reader.ReadSingle() * Mathf.Rad2Deg };

	public static Vector2[] ReadUvArray(this BinaryReader reader, int length)
	{
		var array = new Vector2[length];
		for (var i = 0; i < length; i++)
			array[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
		return array;
	}

	public static Vector3[] ReadVector3Array(this BinaryReader reader, int length)
	{
		var array = new Vector3[length];
		for(var i = 0; i < length; i++)
			array[i] = reader.ReadVector3();
		return array;
	}

	public static Vector3[] ReadVertexArray(this BinaryReader reader, int length)
	{
		var vertices = new Vector3[length];
		for (var i = 0; i < length; i++)
			vertices[i] = reader.ReadVector3();
		return vertices;
	}
}