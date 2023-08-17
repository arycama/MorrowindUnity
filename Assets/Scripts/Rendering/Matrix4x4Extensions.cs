using UnityEngine;

public static class Matrix4x4Extensions
{
    public static float Near(this Matrix4x4 matrix) => matrix[2, 3] / (matrix[2, 2] - 1f);
    public static float Far(this Matrix4x4 matrix) => matrix[2, 3] / (matrix[2, 2] + 1f);
    public static float Fov(this Matrix4x4 matrix) => matrix[1, 1];
    public static float Aspect(this Matrix4x4 matrix) => matrix.m11 / matrix.m00;
    public static float OrthoWidth(this Matrix4x4 matrix) => 2f / matrix.m00;
    public static float OrthoHeight(this Matrix4x4 matrix) => 2f / matrix.m11;
    public static float OrthoNear(this Matrix4x4 matrix) => (1f + matrix.m23) / matrix.m22;
    public static float OrthoFar(this Matrix4x4 matrix) => (matrix.m23 - 1f) / matrix.m22;

    public static Vector3 Right(this Matrix4x4 matrix) => matrix.GetColumn(0);

    public static Vector3 Up(this Matrix4x4 matrix) => matrix.GetColumn(1);

    public static Vector3 Forward(this Matrix4x4 matrix) => matrix.GetColumn(2);

    public static Vector3 Position(this Matrix4x4 matrix) => matrix.GetColumn(3);

    public static Matrix4x4 ConvertToAtlasMatrix(this Matrix4x4 m)
    {
        if (SystemInfo.usesReversedZBuffer)
            m.SetRow(2, -m.GetRow(2));

        m.SetRow(0, 0.5f * (m.GetRow(0) + m.GetRow(3)));
        m.SetRow(1, 0.5f * (m.GetRow(1) + m.GetRow(3)));
        m.SetRow(2, 0.5f * (m.GetRow(2) + m.GetRow(3)));
        return m;
    }
}
