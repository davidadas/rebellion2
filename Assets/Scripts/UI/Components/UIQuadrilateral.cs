using System;
using UnityEngine;

/// <summary>
/// Describes a clockwise convex surface and its projective mapping from a unit rectangle.
/// </summary>
[Serializable]
public struct UIQuadrilateral
{
    public static readonly UIQuadrilateral Unit = new UIQuadrilateral(
        Vector2.zero,
        Vector2.up,
        Vector2.one,
        Vector2.right
    );

    [SerializeField]
    private Vector2 bottomLeft;

    [SerializeField]
    private Vector2 topLeft;

    [SerializeField]
    private Vector2 topRight;

    [SerializeField]
    private Vector2 bottomRight;

    public Vector2 this[int index] =>
        index switch
        {
            0 => bottomLeft,
            1 => topLeft,
            2 => topRight,
            3 => bottomRight,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

    public Rect Bounds =>
        Rect.MinMaxRect(
            Mathf.Min(bottomLeft.x, topLeft.x, topRight.x, bottomRight.x),
            Mathf.Min(bottomLeft.y, topLeft.y, topRight.y, bottomRight.y),
            Mathf.Max(bottomLeft.x, topLeft.x, topRight.x, bottomRight.x),
            Mathf.Max(bottomLeft.y, topLeft.y, topRight.y, bottomRight.y)
        );

    public bool IsValid
    {
        get
        {
            for (int index = 0; index < 4; index++)
            {
                Vector2 point = this[index];
                if (
                    float.IsNaN(point.x)
                    || float.IsNaN(point.y)
                    || float.IsInfinity(point.x)
                    || float.IsInfinity(point.y)
                    || Cross(this[(index + 1) % 4] - point, this[(index + 2) % 4] - point) >= 0
                )
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Creates a surface with corners ordered bottom left, top left, top right, bottom right.
    /// </summary>
    /// <param name="bottomLeft">The bottom-left corner.</param>
    /// <param name="topLeft">The top-left corner.</param>
    /// <param name="topRight">The top-right corner.</param>
    /// <param name="bottomRight">The bottom-right corner.</param>
    public UIQuadrilateral(
        Vector2 bottomLeft,
        Vector2 topLeft,
        Vector2 topRight,
        Vector2 bottomRight
    )
    {
        this.bottomLeft = bottomLeft;
        this.topLeft = topLeft;
        this.topRight = topRight;
        this.bottomRight = bottomRight;
    }

    /// <summary>
    /// Returns a surface with one corner replaced.
    /// </summary>
    /// <param name="index">The clockwise corner index.</param>
    /// <param name="point">The replacement position.</param>
    /// <returns>The edited surface.</returns>
    public UIQuadrilateral WithCorner(int index, Vector2 point)
    {
        _ = this[index];
        return new UIQuadrilateral(
            index == 0 ? point : bottomLeft,
            index == 1 ? point : topLeft,
            index == 2 ? point : topRight,
            index == 3 ? point : bottomRight
        );
    }

    /// <summary>
    /// Tests a point against all four edges, including points on the boundary.
    /// </summary>
    /// <param name="point">The point in the same coordinates as the corners.</param>
    /// <returns>Whether the point belongs to this convex surface.</returns>
    public bool Contains(Vector2 point)
    {
        if (
            !IsValid
            || float.IsNaN(point.x)
            || float.IsNaN(point.y)
            || float.IsInfinity(point.x)
            || float.IsInfinity(point.y)
        )
            return false;
        for (int index = 0; index < 4; index++)
            if (Cross(this[(index + 1) % 4] - this[index], point - this[index]) > 0)
                return false;
        return true;
    }

    /// <summary>
    /// Projects each corner through a homogeneous transform.
    /// </summary>
    /// <param name="matrix">The projective transform.</param>
    /// <returns>The transformed surface.</returns>
    public UIQuadrilateral Transform(Matrix4x4 matrix)
    {
        return new UIQuadrilateral(
            matrix.MultiplyPoint(bottomLeft),
            matrix.MultiplyPoint(topLeft),
            matrix.MultiplyPoint(topRight),
            matrix.MultiplyPoint(bottomRight)
        );
    }

    /// <summary>
    /// Computes the homography from a unit rectangle to this surface.
    /// </summary>
    /// <returns>The projective matrix with homogeneous division in its fourth row.</returns>
    public Matrix4x4 ToMatrix()
    {
        if (!IsValid)
            throw new InvalidOperationException(
                "A perspective surface must be convex and nonzero."
            );
        Vector2 first = bottomRight - topRight;
        Vector2 second = topLeft - topRight;
        Vector2 difference = bottomLeft - bottomRight + topRight - topLeft;
        float denominator = Cross(first, second);
        float horizontal = Cross(difference, second) / denominator;
        float vertical = Cross(first, difference) / denominator;
        Matrix4x4 matrix = Matrix4x4.identity;
        matrix.m00 = bottomRight.x - bottomLeft.x + horizontal * bottomRight.x;
        matrix.m01 = topLeft.x - bottomLeft.x + vertical * topLeft.x;
        matrix.m03 = bottomLeft.x;
        matrix.m10 = bottomRight.y - bottomLeft.y + horizontal * bottomRight.y;
        matrix.m11 = topLeft.y - bottomLeft.y + vertical * topLeft.y;
        matrix.m13 = bottomLeft.y;
        matrix.m30 = horizontal;
        matrix.m31 = vertical;
        return matrix;
    }

    /// <summary>
    /// Computes the signed area spanned by two vectors.
    /// </summary>
    /// <param name="first">The first vector.</param>
    /// <param name="second">The second vector.</param>
    /// <returns>The signed two-dimensional cross product.</returns>
    private static float Cross(Vector2 first, Vector2 second)
    {
        return first.x * second.y - first.y * second.x;
    }
}
