using System.IO;
using System.Text;
using UnityEngine;

public class BufferReader : BinaryReader
{
	public BufferReader(Stream input) : base(input) { }

	public BufferReader(Stream input, Encoding encoding) : base(input, encoding) { }

	public BufferReader(Stream input, Encoding encoding, bool leaveOpen) : base(input, encoding, leaveOpen) { }

	public bool ReadBool32() => ReadInt32() != 0;

	public int[] ReadTriangles(int count)
	{
		var triangles = new int[count];
		for (var i = 0; i < count; i += 3)
		{
			triangles[i] = ReadInt16();
			triangles[i + 2] = ReadInt16();
			triangles[i + 1] = ReadInt16();
		}

		return triangles;
	}

	public sbyte[] ReadSByteArray(int length)
	{
		var bytes = new sbyte[length];
		for(var i = 0; i < length; i++)
			bytes[i] = ReadSByte();
		return bytes;
	}

	public string ReadString(int length) => Encoding.ASCII.GetString(ReadBytes(length)).TrimEnd('\0');

	public string ReadLengthPrefixedString() => ReadString(ReadInt32());

	public Color ReadColor3() => new Color(ReadSingle(), ReadSingle(), ReadSingle());

	public Color GetReadColor4() => new Color(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());

	public Color[] ReadColor4Array(int length)
	{
		var colors = new Color[length];
		for (var i = 0; i < colors.Length; i++)
			colors[i] = GetReadColor4();
		return colors;
	}

	public Color32 ReadColor323() => new Color32(ReadByte(), ReadByte(), ReadByte(), 1);

	public Color32 ReadColor32() => new Color32(ReadByte(), ReadByte(), ReadByte(), ReadByte());

	public Color32[] ReadColor323Array(int length)
	{
		var colors = new Color32[length];
		for(var i = 0; i < length; i++)
			colors[i] = new Color32(ReadByte(), ReadByte(), ReadByte(), 1);
		return colors;
	}

	public Matrix4x4 ReadMatrix() => new Matrix4x4()
	{
		m00 = ReadSingle(),
		m02 = ReadSingle(),
		m01 = ReadSingle(),
		m20 = ReadSingle(),
		m22 = ReadSingle(),
		m21 = ReadSingle(),
		m10 = ReadSingle(),
		m12 = ReadSingle(),
		m11 = ReadSingle(),
		m33 = 1
	};

	public Quaternion ReadQuaternion() => new Quaternion() { w = -ReadSingle(), x = ReadSingle(), z = ReadSingle(), y = ReadSingle() };

	public Quaternion ReadRotation()
	{
		var m00 = ReadSingle();
		var m02 = ReadSingle();
		var m01 = ReadSingle();

		var m20 = ReadSingle();
		var m22 = ReadSingle();
		var m21 = ReadSingle();

		var m10 = ReadSingle();
		var m12 = ReadSingle();
		var m11 = ReadSingle();

		var tr = m00 + m11 + m22;

		if (tr > 0)
		{
			var s = Mathf.Sqrt(tr + 1); // S=4*qw 
			var recip = 0.5f / s;
			return new Quaternion()
			{
				w = 0.5f * s,
				x = (m21 - m12) * recip,
				y = (m02 - m20) * recip,
				z = (m10 - m01) * recip
			};
		}
		else if ((m00 > m11) && (m00 > m22))
		{
			var s = Mathf.Sqrt(1 + m00 - m11 - m22); // S=4*qx 
			var recip = 0.5f / s;
			return new Quaternion()
			{
				w = (m21 - m12) * recip,
				x = 0.5f * s,
				y = (m01 + m10) * recip,
				z = (m02 + m20) * recip
			};
		}
		else if (m11 > m22)
		{
			var s = Mathf.Sqrt(1 + m11 - m00 - m22); // S=4*qy
			var recip = 0.5f / s;
			return new Quaternion()
			{
				w = (m02 - m20) * recip,
				x = (m01 + m10) * recip,
				y = 0.5f * s,
				z = (m12 + m21) * recip
			};
		}
		else
		{
			var s = Mathf.Sqrt(1 + m22 - m00 - m11); // S=4*qz
			var recip = 0.5f / s;
			return new Quaternion()
			{
				w = (m10 - m01) * recip,
				x = (m02 + m20) * recip,
				y = (m12 + m21) * recip,
				z = 0.5f * s
			};
		}
	}

	public Vector3 ReadVector3() => new Vector3(){ x = ReadSingle(), z = ReadSingle(), y = ReadSingle() };

	public Vector3 ReadEulerAngle() => new Vector3() { x = ReadSingle() * Mathf.Rad2Deg, z = ReadSingle() * Mathf.Rad2Deg, y = ReadSingle() * Mathf.Rad2Deg };

	public Vector2[] ReadUvArray(int length)
	{
		var array = new Vector2[length];
		for (var i = 0; i < length; i++)
			array[i] = new Vector2(ReadSingle(), ReadSingle());
		return array;
	}

	public Vector3[] ReadVector3Array(int length)
	{
		var array = new Vector3[length];
		for(var i = 0; i < length; i++)
			array[i] = ReadVector3();
		return array;
	}

	public Vector3[] ReadVertexArray(int length)
	{
		var vertices = new Vector3[length];
		for (var i = 0; i < length; i++)
			vertices[i] = ReadVector3();
		return vertices;
	}
}