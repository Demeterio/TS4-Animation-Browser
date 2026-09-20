using System.Windows;
using System.Windows.Media.Media3D;

namespace TS4AnimationBrowser.App;

public partial class MainWindow
{
    private const double ResetCameraYawOffset = -Math.PI / 4.0;
    private const double ResetCameraPitch = 0.22;
    private const double FrontCameraPitch = 0.04;
    private const double HorizontalFrameMargin = 1.32;
    private const double VerticalFrameMargin = 1.08;

    internal void FramePreview(PreviewBounds bounds)
        => FrameCameraToBounds(bounds, GetPreferredFrontYaw() + ResetCameraYawOffset, ResetCameraPitch);

    private void ResetCameraFacingAxes_Click(object sender, RoutedEventArgs e)
    {
        if (GetVfxFramingBounds() is { } vfxBounds)
        {
            FrameCameraToBounds(vfxBounds, GetPreferredFrontYaw() + ResetCameraYawOffset, ResetCameraPitch);
            return;
        }

        if (_skeletonPreview.FramingBounds is { } actionBounds)
        {
            FrameCameraToBounds(actionBounds, _skeletonPreview.PreferredFrontYaw + ResetCameraYawOffset, ResetCameraPitch);
            return;
        }

        if (_skeletonPreview.CurrentBounds is { } currentBounds)
        {
            FrameCameraToBounds(currentBounds, _skeletonPreview.PreferredFrontYaw + ResetCameraYawOffset, ResetCameraPitch);
            return;
        }

        ApplyDefaultViewerCamera();
    }

    internal void ApplyDefaultViewerCamera()
    {
        _cameraTarget = new Point3D(0, 0.9, 0);
        _cameraYaw = GetPreferredFrontYaw() + ResetCameraYawOffset;
        _cameraPitch = ResetCameraPitch;
        _cameraDistance = 5.5;
        ViewportCamera.FieldOfView = 42;
        UpdateCamera();
    }

    internal void ApplyFrontViewerCamera()
    {
        if (GetVfxFramingBounds() is { } vfxBounds)
        {
            FrameCameraToBounds(vfxBounds, GetPreferredFrontYaw(), FrontCameraPitch);
            return;
        }

        var frontYaw = _skeletonPreview.PreferredFrontYaw;
        if (_skeletonPreview.FramingBounds is { } actionBounds)
        {
            FrameCameraToBounds(actionBounds, frontYaw, FrontCameraPitch);
            return;
        }

        if (_skeletonPreview.CurrentBounds is { } currentBounds)
        {
            FrameCameraToBounds(currentBounds, frontYaw, FrontCameraPitch);
            return;
        }

        _cameraYaw = frontYaw;
        _cameraPitch = FrontCameraPitch;
        UpdateCamera();
    }

    private double GetPreferredFrontYaw()
    {
        if (_vfxParticlePreview?.HasEffect == true)
            return _vfxParticlePreview.PreferredFrontYaw;
        if (_vfxModelPreview?.HasEffect == true)
            return _vfxModelPreview.PreferredFrontYaw;
        return _skeletonPreview.PreferredFrontYaw;
    }

    private PreviewBounds? GetVfxFramingBounds()
    {
        PreviewBounds? bounds = null;
        if (_vfxParticlePreview?.HasEffect == true && _vfxParticlePreview.FramingBounds is { } spriteBounds)
            bounds = spriteBounds;
        if (_vfxModelPreview?.HasEffect == true && _vfxModelPreview.FramingBounds is { } modelBounds)
            bounds = bounds is null ? modelBounds : UnionBounds(bounds.Value, modelBounds);
        return bounds;
    }

    private static PreviewBounds UnionBounds(PreviewBounds a, PreviewBounds b)
        => new(
            new Point3D(
                Math.Min(a.Min.X, b.Min.X),
                Math.Min(a.Min.Y, b.Min.Y),
                Math.Min(a.Min.Z, b.Min.Z)),
            new Point3D(
                Math.Max(a.Max.X, b.Max.X),
                Math.Max(a.Max.Y, b.Max.Y),
                Math.Max(a.Max.Z, b.Max.Z)));

    private void FrameCameraToBounds(PreviewBounds bounds, double yaw, double pitch)
    {
        _cameraTarget = bounds.Center;
        _cameraYaw = yaw;
        _cameraPitch = pitch;
        ViewportCamera.FieldOfView = 42;

        var viewportWidth = Math.Max(1.0, AnimationViewport.ActualWidth);
        var viewportHeight = Math.Max(1.0, AnimationViewport.ActualHeight);
        var aspect = viewportWidth / viewportHeight;

        var horizontalHalfFov = ViewportCamera.FieldOfView * Math.PI / 360.0;
        var verticalHalfFov = Math.Atan(Math.Tan(horizontalHalfFov) / Math.Max(0.1, aspect));
        var tanHorizontal = Math.Max(0.01, Math.Tan(horizontalHalfFov));
        var tanVertical = Math.Max(0.01, Math.Tan(verticalHalfFov));

        var cosPitch = Math.Cos(pitch);
        var cameraOffsetDirection = new Vector3D(
            Math.Sin(yaw) * cosPitch,
            Math.Sin(pitch),
            Math.Cos(yaw) * cosPitch);
        cameraOffsetDirection.Normalize();

        var forward = -cameraOffsetDirection;
        var right = Vector3D.CrossProduct(forward, new Vector3D(0, 1, 0));
        if (right.LengthSquared < 0.000001)
            right = new Vector3D(1, 0, 0);
        else
            right.Normalize();

        var up = Vector3D.CrossProduct(right, forward);
        if (up.LengthSquared < 0.000001)
            up = new Vector3D(0, 1, 0);
        else
            up.Normalize();

        var requiredDistance = 0.0;
        foreach (var corner in GetBoundsCorners(bounds))
        {
            var relative = corner - bounds.Center;
            var depthOffset = Vector3D.DotProduct(relative, forward);
            var horizontalExtent = Math.Abs(Vector3D.DotProduct(relative, right));
            var verticalExtent = Math.Abs(Vector3D.DotProduct(relative, up));

            var horizontalDistance = horizontalExtent * HorizontalFrameMargin / tanHorizontal - depthOffset;
            var verticalDistance = verticalExtent * VerticalFrameMargin / tanVertical - depthOffset;
            var positiveDepthDistance = -depthOffset + 0.05;

            requiredDistance = Math.Max(requiredDistance, horizontalDistance);
            requiredDistance = Math.Max(requiredDistance, verticalDistance);
            requiredDistance = Math.Max(requiredDistance, positiveDepthDistance);
        }

        _cameraDistance = Math.Clamp(requiredDistance, 0.35, 70.0);
        UpdateCamera();
    }

    private static IEnumerable<Point3D> GetBoundsCorners(PreviewBounds bounds)
    {
        yield return new Point3D(bounds.Min.X, bounds.Min.Y, bounds.Min.Z);
        yield return new Point3D(bounds.Min.X, bounds.Min.Y, bounds.Max.Z);
        yield return new Point3D(bounds.Min.X, bounds.Max.Y, bounds.Min.Z);
        yield return new Point3D(bounds.Min.X, bounds.Max.Y, bounds.Max.Z);
        yield return new Point3D(bounds.Max.X, bounds.Min.Y, bounds.Min.Z);
        yield return new Point3D(bounds.Max.X, bounds.Min.Y, bounds.Max.Z);
        yield return new Point3D(bounds.Max.X, bounds.Max.Y, bounds.Min.Z);
        yield return new Point3D(bounds.Max.X, bounds.Max.Y, bounds.Max.Z);
    }
}
