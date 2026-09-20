using System.Windows;

namespace TS4AnimationBrowser.App;

public partial class MainWindow
{
    private const double CameraStepRadians = Math.PI / 12.0;

    private void CameraOrbitLeft_Click(object sender, RoutedEventArgs e)
    {
        _cameraYaw -= CameraStepRadians;
        UpdateCamera();
    }

    private void CameraOrbitRight_Click(object sender, RoutedEventArgs e)
    {
        _cameraYaw += CameraStepRadians;
        UpdateCamera();
    }

    private void CameraTiltUp_Click(object sender, RoutedEventArgs e)
    {
        _cameraPitch = Math.Clamp(_cameraPitch + CameraStepRadians, -1.35, 1.35);
        UpdateCamera();
    }

    private void CameraTiltDown_Click(object sender, RoutedEventArgs e)
    {
        _cameraPitch = Math.Clamp(_cameraPitch - CameraStepRadians, -1.35, 1.35);
        UpdateCamera();
    }

    private void CameraFront_Click(object sender, RoutedEventArgs e) => ApplyFrontViewerCamera();

    private void CameraZoomIn_Click(object sender, RoutedEventArgs e)
    {
        _cameraDistance = Math.Clamp(_cameraDistance * 0.82, 0.25, 100.0);
        UpdateCamera();
    }

    private void CameraZoomOut_Click(object sender, RoutedEventArgs e)
    {
        _cameraDistance = Math.Clamp(_cameraDistance * 1.22, 0.25, 100.0);
        UpdateCamera();
    }
}
