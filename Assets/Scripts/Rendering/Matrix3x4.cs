using UnityEngine;

public struct Matrix3x4
{
    public Vector3 column0, column1, column2, column3;

    public static implicit operator Matrix3x4(Matrix4x4 matrix)
    {
        return new Matrix3x4
        {
            column0 = matrix.GetColumn(0),
            column1 = matrix.GetColumn(1),
            column2 = matrix.GetColumn(2),
            column3 = matrix.GetColumn(3),
        };
    }
}
