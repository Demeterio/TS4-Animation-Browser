using System.Windows.Media;
using System.Windows.Media.Media3D;
using NumericsVector3 = System.Numerics.Vector3;
using WpfQuaternion = System.Windows.Media.Media3D.Quaternion;

namespace TS4AnimationBrowser.App;

public readonly record struct PreviewBounds(Point3D Min, Point3D Max)
{
    public Point3D Center => new(
        (Min.X + Max.X) * 0.5,
        (Min.Y + Max.Y) * 0.5,
        (Min.Z + Max.Z) * 0.5);

    public double Radius
    {
        get
        {
            var dx = Max.X - Min.X;
            var dy = Max.Y - Min.Y;
            var dz = Max.Z - Min.Z;
            return Math.Max(0.1, Math.Sqrt(dx * dx + dy * dy + dz * dz) * 0.5);
        }
    }
}

public static class PreviewGeometry
{
    public static GeometryModel3D CreateOctahedralBone(Color color)
    {
        var mesh = new MeshGeometry3D
        {
            Positions = new Point3DCollection
            {
                new(0, -0.5, 0),
                new(0, 0.5, 0),
                new(1, 0, 0),
                new(-1, 0, 0),
                new(0, 0, 1),
                new(0, 0, -1)
            },
            TriangleIndices = new Int32Collection
            {
                0,2,4, 0,4,3, 0,3,5, 0,5,2,
                1,4,2, 1,3,4, 1,5,3, 1,2,5
            }
        };
        return CreateModel(mesh, color);
    }

    public static GeometryModel3D CreateCylinder(int sides, Color color)
    {
        var positions = new Point3DCollection();
        var triangles = new Int32Collection();

        for (var ring = 0; ring < 2; ring++)
        {
            var y = ring == 0 ? -0.5 : 0.5;
            for (var side = 0; side < sides; side++)
            {
                var angle = side * Math.PI * 2 / sides;
                positions.Add(new Point3D(Math.Cos(angle), y, Math.Sin(angle)));
            }
        }

        for (var side = 0; side < sides; side++)
        {
            var next = (side + 1) % sides;
            triangles.Add(side); triangles.Add(next); triangles.Add(sides + next);
            triangles.Add(side); triangles.Add(sides + next); triangles.Add(sides + side);
        }

        return CreateModel(new MeshGeometry3D { Positions = positions, TriangleIndices = triangles }, color);
    }

    public static GeometryModel3D CreateSphere(double radius, Color color, bool bright = false)
    {
        const int latitudes = 8;
        const int longitudes = 12;
        var positions = new Point3DCollection();
        var triangles = new Int32Collection();

        for (var latitude = 0; latitude <= latitudes; latitude++)
        {
            var theta = Math.PI * latitude / latitudes;
            var y = Math.Cos(theta) * radius;
            var ringRadius = Math.Sin(theta) * radius;
            for (var longitude = 0; longitude <= longitudes; longitude++)
            {
                var phi = Math.PI * 2 * longitude / longitudes;
                positions.Add(new Point3D(Math.Cos(phi) * ringRadius, y, Math.Sin(phi) * ringRadius));
            }
        }

        var stride = longitudes + 1;
        for (var latitude = 0; latitude < latitudes; latitude++)
        {
            for (var longitude = 0; longitude < longitudes; longitude++)
            {
                var current = latitude * stride + longitude;
                var next = current + stride;
                triangles.Add(current); triangles.Add(next); triangles.Add(current + 1);
                triangles.Add(current + 1); triangles.Add(next); triangles.Add(next + 1);
            }
        }

        return CreateModel(new MeshGeometry3D { Positions = positions, TriangleIndices = triangles }, color, bright);
    }

    public static GeometryModel3D CreateUnitBox(Color color)
    {
        var mesh = new MeshGeometry3D
        {
            Positions = new Point3DCollection
            {
                new(-0.5,-0.5,-0.5), new(0.5,-0.5,-0.5), new(0.5,0.5,-0.5), new(-0.5,0.5,-0.5),
                new(-0.5,-0.5,0.5), new(0.5,-0.5,0.5), new(0.5,0.5,0.5), new(-0.5,0.5,0.5)
            },
            TriangleIndices = new Int32Collection
            {
                0,2,1, 0,3,2, 4,5,6, 4,6,7,
                0,1,5, 0,5,4, 2,3,7, 2,7,6,
                0,4,7, 0,7,3, 1,2,6, 1,6,5
            }
        };
        return CreateModel(mesh, color);
    }

    public static GeometryModel3D CreateBox(Point3D center, Vector3D size, Color color)
    {
        var model = CreateUnitBox(color);
        var transform = new Transform3DGroup();
        transform.Children.Add(new ScaleTransform3D(size.X, size.Y, size.Z));
        transform.Children.Add(new TranslateTransform3D(center.X, center.Y, center.Z));
        model.Transform = transform;
        return model;
    }

    public static Transform3D CreateSegmentTransform(
        NumericsVector3 from,
        NumericsVector3 to,
        double width,
        double minimumLength,
        double maximumLength)
    {
        var direction = new Vector3D(to.X - from.X, to.Y - from.Y, to.Z - from.Z);
        var length = direction.Length;
        if (!double.IsFinite(length) || length < minimumLength || length > maximumLength)
            return new ScaleTransform3D(0, 0, 0);

        var rotation = CreateRotation(new Vector3D(0, 1, 0), direction);
        var midpoint = new Point3D(
            (from.X + to.X) * 0.5,
            (from.Y + to.Y) * 0.5,
            (from.Z + to.Z) * 0.5);

        var transforms = new Transform3DGroup();
        transforms.Children.Add(new ScaleTransform3D(width, length, width));
        transforms.Children.Add(new RotateTransform3D(rotation));
        transforms.Children.Add(new TranslateTransform3D(midpoint.X, midpoint.Y, midpoint.Z));
        return transforms;
    }

    public static Transform3D CreateFootTransform(
        NumericsVector3 foot,
        NumericsVector3 toe,
        double width,
        double thickness,
        double extension)
    {
        var direction = new Vector3D(toe.X - foot.X, toe.Y - foot.Y, toe.Z - foot.Z);
        var length = direction.Length;
        if (!double.IsFinite(length) || length < 0.005)
            return new ScaleTransform3D(0, 0, 0);

        direction.Normalize();
        var rotation = CreateRotation(new Vector3D(0, 0, 1), direction);
        var totalLength = length + extension;
        var center = new Point3D(
            foot.X + direction.X * totalLength * 0.5,
            foot.Y + direction.Y * totalLength * 0.5,
            foot.Z + direction.Z * totalLength * 0.5);

        var transforms = new Transform3DGroup();
        transforms.Children.Add(new ScaleTransform3D(width, thickness, totalLength));
        transforms.Children.Add(new RotateTransform3D(rotation));
        transforms.Children.Add(new TranslateTransform3D(center.X, center.Y, center.Z));
        return transforms;
    }

    private static QuaternionRotation3D CreateRotation(Vector3D from, Vector3D to)
    {
        if (from.LengthSquared < 0.000001 || to.LengthSquared < 0.000001)
            return new QuaternionRotation3D(WpfQuaternion.Identity);

        from.Normalize();
        to.Normalize();
        var dot = Math.Clamp(Vector3D.DotProduct(from, to), -1.0, 1.0);
        var angle = Math.Acos(dot) * 180.0 / Math.PI;
        var axis = Vector3D.CrossProduct(from, to);

        if (axis.LengthSquared < 0.000001)
        {
            if (dot >= 0)
                return new QuaternionRotation3D(WpfQuaternion.Identity);

            var fallback = Math.Abs(from.Y) < 0.9
                ? Vector3D.CrossProduct(from, new Vector3D(0, 1, 0))
                : Vector3D.CrossProduct(from, new Vector3D(1, 0, 0));
            fallback.Normalize();
            return new QuaternionRotation3D(new WpfQuaternion(fallback, 180));
        }

        axis.Normalize();
        return new QuaternionRotation3D(new WpfQuaternion(axis, angle));
    }

    private static GeometryModel3D CreateModel(MeshGeometry3D mesh, Color color, bool bright = false)
    {
        Material material;
        if (bright)
        {
            var group = new MaterialGroup();
            group.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
            group.Children.Add(new EmissiveMaterial(new SolidColorBrush(color)));
            material = group;
        }
        else
        {
            material = new DiffuseMaterial(new SolidColorBrush(color));
        }

        return new GeometryModel3D(mesh, material) { BackMaterial = material };
    }
}
