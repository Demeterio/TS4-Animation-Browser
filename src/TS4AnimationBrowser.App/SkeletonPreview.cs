using System.Numerics;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using TS4AnimationBrowser.Core.ViewerV2;

namespace TS4AnimationBrowser.App;

public sealed class SkeletonPreview
{
    private const string RootBoneName = "b__ROOT__";
    private const string RootBindBoneName = "b__ROOT_bind__";
    private const double DefaultFrontYaw = Math.PI;
    private const double HeadHeightRatio = 0.175;
    private const double HeadWidthRatio = 0.145;
    private const double HeadDepthRatio = 0.14;
    private const double FootJointRadius = 0.014;
    private const double ToeJointRadius = 0.009;

    private readonly ModelVisual3D _visual = new();
    private readonly Model3DGroup _group = new();
    private readonly Dictionary<int, GeometryModel3D> _links = [];
    private readonly Dictionary<int, GeometryModel3D> _joints = [];
    private readonly Dictionary<int, GeometryModel3D> _eyes = [];
    private readonly Dictionary<int, int> _displayParents = [];
    private readonly HashSet<int> _visibleBones = [];
    private readonly HashSet<int> _eyeBoneIndices = [];
    private readonly HashSet<int> _muzzleBoneIndices = [];
    private readonly HashSet<int> _animalGroundBoneIndices = [];
    private readonly Matrix4x4 _viewerSpaceTransform = Matrix4x4.CreateRotationY(MathF.PI);

    private StudioRigV2? _rig;
    private StudioClipV2? _clip;
    private StudioPoseEvaluatorV2? _evaluator;
    private bool _rawObjectMode;
    private bool _animalRig;
    private bool _horseRig;
    private bool _placementResolved;
    private bool _headSizeResolved;
    private Vector3 _anchorPosition;
    private float _floorOffsetY;
    private double _headScaleX;
    private double _headScaleY;
    private double _headScaleZ;
    private double _bodyVisualScale = 1.0;
    private double _animalBodyHeight = 0.85;
    private int _rootBoneIndex = -1;
    private int _rootBindBoneIndex = -1;
    private int _anchorBoneIndex = -1;
    private int _headBoneIndex = -1;
    private int _pelvisBoneIndex = -1;
    private int _leftPelvisLegIndex = -1;
    private int _rightPelvisLegIndex = -1;
    private int _petSpineBridgeIndex = -1;
    private GeometryModel3D? _headModel;
    private GeometryModel3D? _muzzleModel;
    private GeometryModel3D? _pelvisBridge;
    private GeometryModel3D? _petSpineBridge;

    public SkeletonPreview(Viewport3D viewport)
    {
        _visual.Content = _group;
        viewport.Children.Add(_visual);
    }

    public PreviewBounds? CurrentBounds { get; private set; }
    public PreviewBounds? FramingBounds { get; private set; }
    public double PreferredFrontYaw { get; private set; } = DefaultFrontYaw;

    public void Clear()
    {
        _rig = null;
        _clip = null;
        _evaluator = null;
        _rawObjectMode = false;
        _animalRig = false;
        _horseRig = false;
        _links.Clear();
        _joints.Clear();
        _eyes.Clear();
        _displayParents.Clear();
        _visibleBones.Clear();
        _eyeBoneIndices.Clear();
        _muzzleBoneIndices.Clear();
        _animalGroundBoneIndices.Clear();
        _group.Children.Clear();
        _placementResolved = false;
        _headSizeResolved = false;
        _anchorPosition = Vector3.Zero;
        _floorOffsetY = 0;
        _headScaleX = 0;
        _headScaleY = 0;
        _headScaleZ = 0;
        _bodyVisualScale = 1.0;
        _animalBodyHeight = 0.85;
        _rootBoneIndex = -1;
        _rootBindBoneIndex = -1;
        _anchorBoneIndex = -1;
        _headBoneIndex = -1;
        _pelvisBoneIndex = -1;
        _leftPelvisLegIndex = -1;
        _rightPelvisLegIndex = -1;
        _petSpineBridgeIndex = -1;
        _headModel = null;
        _muzzleModel = null;
        _pelvisBridge = null;
        _petSpineBridge = null;
        PreferredFrontYaw = DefaultFrontYaw;
        CurrentBounds = null;
        FramingBounds = null;
    }

    public void Load(
        StudioRigV2 rig,
        StudioClipV2? clip = null,
        ResourceVisualCategory category = ResourceVisualCategory.Unknown)
    {
        Clear();
        _rig = rig;
        _clip = clip;
        _rawObjectMode = category == ResourceVisualCategory.ObjectAnimation;
        _animalRig = !_rawObjectMode && IsAnimalRig(rig);
        _horseRig = _animalRig && IsHorseRig(rig);
        if (clip is null)
            return;

        _evaluator = new StudioPoseEvaluatorV2(
            rig,
            clip,
            _rawObjectMode ? StudioPoseEvaluationModeV2.Raw : StudioPoseEvaluationModeV2.BodyPreview);

        CollectDisplayBones(rig);
        ResolveBodyVisualScale(rig);
        _anchorBoneIndex = _rootBindBoneIndex >= 0 ? _rootBindBoneIndex : _rootBoneIndex;
        ResolvePelvisLegs();
        CreateSkeletonGeometry(rig);
        ResolvePetSpineBridge();
        ResolvePlacement();
        Update(0);
        ResolvePreferredFrontYaw(BuildViewerPositions(0));
        FramingBounds = ComputeAnimationBounds() ?? CurrentBounds;
    }

    public void Update(double timeSeconds)
    {
        if (_rig is null || _evaluator is null)
            return;

        var positions = BuildViewerPositions(timeSeconds);
        if (!_rawObjectMode)
            ResolveHeadSize(positions);

        foreach (var pair in _joints)
        {
            var position = positions[pair.Key];
            pair.Value.Transform = new TranslateTransform3D(position.X, position.Y, position.Z);
        }

        foreach (var pair in _links)
        {
            if (!_displayParents.TryGetValue(pair.Key, out var parent))
                continue;

            var width = _rawObjectMode
                ? 0.005
                : GetLinkWidth(_rig.Bones[pair.Key].Name.ToLowerInvariant());
            if (_animalRig && !_rawObjectMode)
                width *= _bodyVisualScale;

            pair.Value.Transform = PreviewGeometry.CreateSegmentTransform(
                positions[parent], positions[pair.Key], width, 0.0001, 100);
        }

        if (!_rawObjectMode)
        {
            UpdatePelvisBridge(positions);
            UpdatePetSpineBridge(positions);
            UpdateHead(positions);
            UpdateEyes(positions);
            UpdateMuzzle(positions);
        }

        CurrentBounds = CalculateBounds(positions);
    }

    private void CollectDisplayBones(StudioRigV2 rig)
    {
        foreach (var bone in rig.Bones)
        {
            var name = bone.Name.ToLowerInvariant();
            if (bone.Name.Equals(RootBoneName, StringComparison.OrdinalIgnoreCase))
                _rootBoneIndex = bone.Index;
            if (bone.Name.Equals(RootBindBoneName, StringComparison.OrdinalIgnoreCase))
                _rootBindBoneIndex = bone.Index;

            if (!_rawObjectMode && IsEyeBone(name))
            {
                _eyeBoneIndices.Add(bone.Index);
                continue;
            }

            if (_animalRig && IsMuzzleLandmark(name))
                _muzzleBoneIndices.Add(bone.Index);

            if (_rawObjectMode)
            {
                _visibleBones.Add(bone.Index);
                continue;
            }

            if (!_animalRig)
            {
                if (IsVisibleHumanBone(name))
                    _visibleBones.Add(bone.Index);
            }
            else if (IsAnimalEndpoint(name))
            {
                AddAnimalPathToRoot(rig, bone.Index);
                AddAnimalTerminalChildren(rig, bone.Index);
            }

            if (IsMainHeadBone(name))
                _headBoneIndex = bone.Index;
            if (name.Contains("pelvis"))
            {
                if (!_animalRig || (_pelvisBoneIndex < 0 && IsMainAnimalPelvisBone(name)))
                    _pelvisBoneIndex = bone.Index;
            }
        }

        if (_animalRig)
        {
            foreach (var bone in rig.Bones)
            {
                var name = bone.Name.ToLowerInvariant();
                if (IsAnimalCoreBone(name))
                    AddAnimalPathToRoot(rig, bone.Index);
            }
        }
    }

    private void AddAnimalPathToRoot(StudioRigV2 rig, int start)
    {
        var current = start;
        var guard = 0;
        while (current >= 0 && current < rig.Bones.Count && guard++ <= rig.Bones.Count)
        {
            var name = rig.Bones[current].Name.ToLowerInvariant();
            if (!IsRootLike(name) && !IsAnimalNoiseBone(name) && !IsFacialControlBone(name))
                _visibleBones.Add(current);

            var parent = rig.Bones[current].ParentIndex;
            if (parent < 0 || parent == current)
                break;
            current = parent;
        }
    }

    private void AddAnimalTerminalChildren(StudioRigV2 rig, int parentIndex)
    {
        var parentName = rig.Bones[parentIndex].Name.ToLowerInvariant();
        if (!IsAnimalPawOrFoot(parentName))
            return;

        var pending = new Stack<int>();
        var visited = new HashSet<int>();
        pending.Push(parentIndex);

        while (pending.Count > 0 && visited.Count <= rig.Bones.Count)
        {
            var current = pending.Pop();
            if (current < 0 || current >= rig.Bones.Count || !visited.Add(current))
                continue;

            var currentName = rig.Bones[current].Name.ToLowerInvariant();
            if (current != parentIndex)
            {
                if (IsRootLike(currentName)
                    || IsAnimalNoiseBone(currentName)
                    || IsFacialControlBone(currentName)
                    || StudioRigBoneSemanticsV2.IsSlot(currentName)
                    || StudioRigBoneSemanticsV2.IsIk(currentName))
                {
                    continue;
                }

                _visibleBones.Add(current);
            }

            _animalGroundBoneIndices.Add(current);

            foreach (var child in rig.Bones.Where(bone => bone.ParentIndex == current))
            {
                var childName = child.Name.ToLowerInvariant();
                if (IsRootLike(childName)
                    || IsAnimalNoiseBone(childName)
                    || IsFacialControlBone(childName)
                    || StudioRigBoneSemanticsV2.IsSlot(childName)
                    || StudioRigBoneSemanticsV2.IsIk(childName))
                {
                    continue;
                }

                pending.Push(child.Index);
            }
        }
    }

    private void ResolveBodyVisualScale(StudioRigV2 rig)
    {
        if (!_animalRig || _evaluator is null || _visibleBones.Count == 0)
            return;

        var bind = TransformToViewer(_evaluator.EvaluateBindPose().WorldPositions);
        var bodyPoints = _visibleBones
            .Where(index => index >= 0 && index < bind.Length && IsFinite(bind[index]))
            .Where(index =>
            {
                var name = rig.Bones[index].Name.ToLowerInvariant();
                return !name.Contains("tail") && !IsFingerBone(name);
            })
            .Select(index => bind[index])
            .ToArray();
        if (bodyPoints.Length < 3)
            return;

        var height = bodyPoints.Max(point => point.Y) - bodyPoints.Min(point => point.Y);
        if (!float.IsFinite(height) || height <= 0.05f)
            return;

        _animalBodyHeight = height;

        var scale = Math.Pow(Math.Max(0.15, height) / 0.85, 0.42);
        if (_horseRig)
            scale *= 1.08;
        _bodyVisualScale = Math.Clamp(scale, 0.62, 1.45);
    }

    private void CreateSkeletonGeometry(StudioRigV2 rig)
    {
        Vector3[]? animalBind = null;
        if (_animalRig && !_rawObjectMode && _evaluator is not null)
            animalBind = TransformToViewer(_evaluator.EvaluateBindPose().WorldPositions);

        foreach (var index in _visibleBones)
        {
            var bone = rig.Bones[index];
            var name = bone.Name.ToLowerInvariant();
            if (index != _headBoneIndex)
            {
                var radius = _rawObjectMode ? 0.009 : GetJointRadius(name);
                if (_animalRig && !_rawObjectMode)
                    radius *= _bodyVisualScale;

                var joint = PreviewGeometry.CreateSphere(
                    radius,
                    _rawObjectMode ? Color.FromRgb(105, 130, 148) : Color.FromRgb(95, 120, 140));
                _joints[index] = joint;
                _group.Children.Add(joint);
            }

            var parent = bone.ParentIndex;
            if (!_rawObjectMode)
            {
                while (parent >= 0 && parent < rig.Bones.Count && !_visibleBones.Contains(parent))
                    parent = rig.Bones[parent].ParentIndex;
            }

            if (_animalRig
                && parent >= 0 && parent < rig.Bones.Count && parent != index
                && !IsSafeAnimalDisplayLink(rig, index, parent, animalBind))
            {
                parent = -1;
            }

            if (parent >= 0 && parent < rig.Bones.Count && parent != index)
            {
                _displayParents[index] = parent;
                var link = PreviewGeometry.CreateCylinder(
                    8,
                    _rawObjectMode ? Color.FromRgb(88, 109, 124) : Color.FromRgb(75, 95, 112));
                _links[index] = link;
                _group.Children.Add(link);
            }
        }

        if (_rawObjectMode)
            return;

        foreach (var eyeIndex in _eyeBoneIndices)
        {
            var eye = PreviewGeometry.CreateSphere(1, Colors.White, bright: true);
            _eyes[eyeIndex] = eye;
            _group.Children.Add(eye);
        }

        if (_leftPelvisLegIndex >= 0 && _rightPelvisLegIndex >= 0)
        {
            _pelvisBridge = PreviewGeometry.CreateCylinder(8, Color.FromRgb(75, 95, 112));
            _group.Children.Add(_pelvisBridge);
        }

        if (_headBoneIndex >= 0)
        {
            _headModel = PreviewGeometry.CreateSphere(1, Color.FromRgb(92, 111, 126));
            _group.Children.Add(_headModel);
        }

        if (_animalRig && _headBoneIndex >= 0 && _muzzleBoneIndices.Count > 0)
        {
            _muzzleModel = PreviewGeometry.CreateCylinder(12, Color.FromRgb(102, 119, 132));
            _group.Children.Add(_muzzleModel);
        }
    }

    private bool IsSafeAnimalDisplayLink(
        StudioRigV2 rig,
        int childIndex,
        int parentIndex,
        Vector3[]? bindPositions)
    {
        if (bindPositions is null
            || childIndex < 0 || childIndex >= bindPositions.Length
            || parentIndex < 0 || parentIndex >= bindPositions.Length
            || !IsFinite(bindPositions[childIndex])
            || !IsFinite(bindPositions[parentIndex]))
        {
            return true;
        }

        var distance = Vector3.Distance(bindPositions[childIndex], bindPositions[parentIndex]);
        if (!float.IsFinite(distance))
            return false;

        var childName = rig.Bones[childIndex].Name.ToLowerInvariant();
        var parentName = rig.Bones[parentIndex].Name.ToLowerInvariant();
        var maximum = Math.Max(0.10, _animalBodyHeight * 0.58);
        if (IsAnimalPawOrFoot(childName) && IsAnimalCoreBone(parentName))
            maximum = Math.Max(0.08, _animalBodyHeight * 0.42);

        return distance <= maximum;
    }

    private void ResolvePelvisLegs()
    {
        if (_rig is null || _pelvisBoneIndex < 0 || _animalRig)
            return;

        var thighs = _visibleBones
            .Where(index => _rig.Bones[index].Name.Contains("thigh", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (thighs.Length < 2)
            return;

        _leftPelvisLegIndex = thighs.FirstOrDefault(index => IsLeftSideName(_rig.Bones[index].Name), -1);
        _rightPelvisLegIndex = thighs.FirstOrDefault(index => IsRightSideName(_rig.Bones[index].Name), -1);
        if (_leftPelvisLegIndex < 0 || _rightPelvisLegIndex < 0)
        {
            _leftPelvisLegIndex = thighs[0];
            _rightPelvisLegIndex = thighs[1];
        }
    }

    private void ResolvePetSpineBridge()
    {
        if (!_animalRig || _rig is null || _evaluator is null || _pelvisBoneIndex < 0)
            return;

        var torso = _visibleBones
            .Where(index => index != _pelvisBoneIndex && IsAnimalSpineBone(_rig.Bones[index].Name.ToLowerInvariant()))
            .ToArray();
        if (torso.Length == 0)
            return;

        if (_displayParents.TryGetValue(_pelvisBoneIndex, out var parent) && torso.Contains(parent))
            return;
        if (torso.Any(index => _displayParents.TryGetValue(index, out var p) && p == _pelvisBoneIndex))
            return;

        var bind = TransformToViewer(_evaluator.EvaluateBindPose().WorldPositions);
        if (_pelvisBoneIndex >= bind.Length || !IsFinite(bind[_pelvisBoneIndex]))
            return;

        var candidate = torso
            .Where(index => index >= 0 && index < bind.Length && IsFinite(bind[index]))
            .OrderBy(index => Vector3.Distance(bind[_pelvisBoneIndex], bind[index]))
            .FirstOrDefault(-1);
        if (candidate < 0)
            return;

        var bridgeLength = Vector3.Distance(bind[_pelvisBoneIndex], bind[candidate]);
        var maximumBridgeLength = Math.Max(0.08, _animalBodyHeight * 0.45);
        if (!float.IsFinite(bridgeLength) || bridgeLength > maximumBridgeLength)
            return;

        _petSpineBridgeIndex = candidate;
        _petSpineBridge = PreviewGeometry.CreateCylinder(8, Color.FromRgb(75, 95, 112));
        _group.Children.Add(_petSpineBridge);
    }

    private void UpdatePelvisBridge(Vector3[] positions)
    {
        if (_pelvisBridge is null
            || _leftPelvisLegIndex < 0 || _leftPelvisLegIndex >= positions.Length
            || _rightPelvisLegIndex < 0 || _rightPelvisLegIndex >= positions.Length)
            return;

        _pelvisBridge.Transform = PreviewGeometry.CreateSegmentTransform(
            positions[_leftPelvisLegIndex], positions[_rightPelvisLegIndex], 0.009, 0.0001, 2.0);
    }

    private void UpdatePetSpineBridge(Vector3[] positions)
    {
        if (_petSpineBridge is null
            || _pelvisBoneIndex < 0 || _pelvisBoneIndex >= positions.Length
            || _petSpineBridgeIndex < 0 || _petSpineBridgeIndex >= positions.Length)
            return;

        _petSpineBridge.Transform = PreviewGeometry.CreateSegmentTransform(
            positions[_pelvisBoneIndex],
            positions[_petSpineBridgeIndex],
            0.009 * _bodyVisualScale,
            0.0001,
            2.0);
    }

    private void ResolvePlacement()
    {
        if (_placementResolved || _rig is null || _evaluator is null)
            return;
        if (_rawObjectMode)
        {
            _placementResolved = true;
            return;
        }

        var rawAtStart = BuildRawViewerPositions(0);
        if (_anchorBoneIndex >= 0 && _anchorBoneIndex < rawAtStart.Length && IsFinite(rawAtStart[_anchorBoneIndex]))
            _anchorPosition = rawAtStart[_anchorBoneIndex];

        var lowest = FindLowestGroundPoint(rawAtStart);
        var duration = _clip?.DurationSeconds ?? 0;
        if (duration > 0.0001)
        {
            var sampleCount = Math.Clamp((int)Math.Ceiling(duration / 0.12) + 1, 12, 48);
            for (var sample = 0; sample < sampleCount; sample++)
            {
                var time = duration * sample / (sampleCount - 1.0);
                lowest = Math.Min(lowest, FindLowestGroundPoint(BuildRawViewerPositions(time)));
            }
        }

        if (float.IsFinite(lowest))
            _floorOffsetY = lowest;
        _placementResolved = true;
    }

    private float FindLowestGroundPoint(Vector3[] positions)
    {
        if (_rig is null)
            return float.PositiveInfinity;

        var preferred = _animalRig && _animalGroundBoneIndices.Count > 0
            ? _animalGroundBoneIndices.Where(_visibleBones.Contains).ToArray()
            : _visibleBones
                .Where(index =>
                {
                    var name = _rig.Bones[index].Name.ToLowerInvariant();
                    return IsMainFootBone(name) || IsMainToeBone(name) || IsAnimalGroundBone(name);
                })
                .ToArray();

        var candidates = preferred.Length > 0 ? preferred : _visibleBones.ToArray();
        var lowest = float.PositiveInfinity;
        foreach (var index in candidates)
        {
            if (index < 0 || index >= positions.Length || !IsFinite(positions[index]))
                continue;

            var radius = GetJointRadius(_rig.Bones[index].Name.ToLowerInvariant());
            if (_animalRig)
                radius *= _bodyVisualScale;
            lowest = Math.Min(lowest, positions[index].Y - (float)radius);
        }
        return lowest;
    }

    private Vector3[] BuildViewerPositions(double timeSeconds)
    {
        var positions = BuildRawViewerPositions(timeSeconds);
        if (!_placementResolved || _rawObjectMode)
            return positions;

        var anchorDelta = Vector3.Zero;
        if (_anchorBoneIndex >= 0 && _anchorBoneIndex < positions.Length && IsFinite(positions[_anchorBoneIndex]))
            anchorDelta = positions[_anchorBoneIndex] - _anchorPosition;

        for (var index = 0; index < positions.Length; index++)
        {
            positions[index].X -= anchorDelta.X;
            positions[index].Z -= anchorDelta.Z;
            positions[index].Y -= _floorOffsetY;
        }
        return positions;
    }

    private Vector3[] BuildRawViewerPositions(double timeSeconds)
        => TransformToViewer(_evaluator!.Evaluate(timeSeconds).WorldPositions);

    private Vector3[] TransformToViewer(IReadOnlyList<Vector3> source)
    {
        var positions = new Vector3[source.Count];
        for (var index = 0; index < positions.Length; index++)
            positions[index] = Vector3.Transform(source[index], _viewerSpaceTransform);
        return positions;
    }

    private void ResolveHeadSize(Vector3[] positions)
    {
        if (_headSizeResolved || _headBoneIndex < 0 || _rig is null)
            return;

        var eyePositions = _eyeBoneIndices
            .Where(index => index >= 0 && index < positions.Length && IsFinite(positions[index]))
            .Select(index => positions[index])
            .ToArray();
        var eyeSpan = ResolveEyeSpan(eyePositions);

        var headLink = 0f;
        var headParent = _rig.Bones[_headBoneIndex].ParentIndex;
        if (headParent >= 0 && headParent < positions.Length && IsFinite(positions[headParent]))
        {
            var value = Vector3.Distance(positions[_headBoneIndex], positions[headParent]);
            if (float.IsFinite(value))
                headLink = value;
        }

        if (_animalRig)
        {
            var localReference = eyeSpan > 0.005f ? eyeSpan : headLink * 0.68f;
            if (localReference <= 0.005f)
                localReference = (float)(_animalBodyHeight * 0.09);

            if (_horseRig)
            {
                _headScaleX = Math.Max(localReference * 0.95, _animalBodyHeight * 0.050);
                _headScaleY = Math.Max(localReference * 1.12, _animalBodyHeight * 0.060);
                _headScaleZ = Math.Max(localReference * 1.20, _animalBodyHeight * 0.068);
            }
            else
            {
                _headScaleX = Math.Max(localReference * 1.00, _animalBodyHeight * 0.052);
                _headScaleY = Math.Max(localReference * 1.14, _animalBodyHeight * 0.058);
                _headScaleZ = Math.Max(localReference * 0.96, _animalBodyHeight * 0.050);
            }

            _headSizeResolved = true;
            return;
        }

        var body = _visibleBones
            .Where(index => index != _headBoneIndex)
            .Select(index => positions[index])
            .Where(IsFinite)
            .ToArray();
        if (body.Length == 0)
            return;

        var bodyHeight = body.Max(value => value.Y) - body.Min(value => value.Y);
        if (!float.IsFinite(bodyHeight) || bodyHeight <= 0.05f)
            return;

        var humanScaleX = bodyHeight * HeadWidthRatio * 0.5;
        var humanScaleY = bodyHeight * HeadHeightRatio * 0.5;
        var humanScaleZ = bodyHeight * HeadDepthRatio * 0.5;

        if (eyeSpan > 0.005f && eyeSpan < bodyHeight * 0.65f)
        {
            humanScaleX = Math.Max(humanScaleX, eyeSpan * 1.15);
            humanScaleY = Math.Max(humanScaleY, eyeSpan * 1.35);
            humanScaleZ = Math.Max(humanScaleZ, eyeSpan * 1.05);
        }

        if (headLink > 0.005f && headLink < bodyHeight * 0.8f)
        {
            humanScaleX = Math.Max(humanScaleX, headLink * 0.85);
            humanScaleY = Math.Max(humanScaleY, headLink * 1.05);
            humanScaleZ = Math.Max(humanScaleZ, headLink * 0.85);
        }

        _headScaleX = humanScaleX;
        _headScaleY = humanScaleY;
        _headScaleZ = humanScaleZ;
        _headSizeResolved = true;
    }

    private static float ResolveEyeSpan(IReadOnlyList<Vector3> eyePositions)
    {
        if (eyePositions.Count < 2)
            return 0;

        var eyeSpan = 0f;
        for (var first = 0; first < eyePositions.Count - 1; first++)
            for (var second = first + 1; second < eyePositions.Count; second++)
                eyeSpan = Math.Max(eyeSpan, Vector3.Distance(eyePositions[first], eyePositions[second]));
        return float.IsFinite(eyeSpan) ? eyeSpan : 0;
    }

    private Vector3 GetHeadCenter(Vector3[] positions)
    {
        var head = positions[_headBoneIndex];
        return _animalRig ? head : new Vector3(head.X, head.Y + (float)(_headScaleY * 0.24), head.Z);
    }

    private void UpdateHead(Vector3[] positions)
    {
        if (_headModel is null || _headBoneIndex < 0 || !_headSizeResolved)
            return;
        var center = GetHeadCenter(positions);
        var transforms = new Transform3DGroup();
        transforms.Children.Add(new ScaleTransform3D(_headScaleX, _headScaleY, _headScaleZ));
        transforms.Children.Add(new TranslateTransform3D(center.X, center.Y, center.Z));
        _headModel.Transform = transforms;
    }

    private void UpdateEyes(Vector3[] positions)
    {
        if (_headBoneIndex < 0 || !_headSizeResolved || _eyes.Count == 0)
            return;

        var center = GetHeadCenter(positions);
        var rx = Math.Max(0.001, _headScaleX);
        var ry = Math.Max(0.001, _headScaleY);
        var rz = Math.Max(0.001, _headScaleZ);
        var eyeRadius = _animalRig
            ? Math.Clamp(Math.Min(rx, ry) * (_horseRig ? 0.055 : 0.070), 0.0025, _horseRig ? 0.009 : 0.0085)
            : Math.Max(0.009, Math.Min(rx, ry) * 0.10);

        foreach (var pair in _eyes)
        {
            var raw = positions[pair.Key];
            var direction = raw - center;
            var denominator = Math.Sqrt(
                direction.X * direction.X / (rx * rx)
                + direction.Y * direction.Y / (ry * ry)
                + direction.Z * direction.Z / (rz * rz));

            var eyePosition = raw;
            if (double.IsFinite(denominator) && denominator >= 0.0001)
            {
                var surfaceFactor = _animalRig ? 0.92 : 0.96;
                eyePosition = center + direction * (float)(surfaceFactor / denominator);
            }

            var transforms = new Transform3DGroup();
            transforms.Children.Add(new ScaleTransform3D(eyeRadius, eyeRadius, eyeRadius));
            transforms.Children.Add(new TranslateTransform3D(eyePosition.X, eyePosition.Y, eyePosition.Z));
            pair.Value.Transform = transforms;
        }
    }

    private void UpdateMuzzle(Vector3[] positions)
    {
        if (_muzzleModel is null || _headBoneIndex < 0 || !_headSizeResolved)
            return;

        var muzzlePoints = _muzzleBoneIndices
            .Where(index => index >= 0 && index < positions.Length && IsFinite(positions[index]))
            .Select(index => positions[index])
            .ToArray();
        if (muzzlePoints.Length == 0)
        {
            _muzzleModel.Transform = new ScaleTransform3D(0, 0, 0);
            return;
        }

        var landmark = new Vector3(
            muzzlePoints.Average(value => value.X),
            muzzlePoints.Average(value => value.Y),
            muzzlePoints.Average(value => value.Z));
        var headCenter = GetHeadCenter(positions);
        var direction = landmark - headCenter;
        var length = direction.Length();
        if (!float.IsFinite(length) || length < 0.005f)
        {
            _muzzleModel.Transform = new ScaleTransform3D(0, 0, 0);
            return;
        }

        direction /= length;

        var rx = Math.Max(0.001, _headScaleX);
        var ry = Math.Max(0.001, _headScaleY);
        var rz = Math.Max(0.001, _headScaleZ);
        var surfaceDenominator = Math.Sqrt(
            direction.X * direction.X / (rx * rx)
            + direction.Y * direction.Y / (ry * ry)
            + direction.Z * direction.Z / (rz * rz));
        if (!double.IsFinite(surfaceDenominator) || surfaceDenominator < 0.0001)
        {
            _muzzleModel.Transform = new ScaleTransform3D(0, 0, 0);
            return;
        }

        var surfaceDistance = 1.0 / surfaceDenominator;
        var startDistance = surfaceDistance * 0.90;
        var availableLength = length - startDistance;
        if (!double.IsFinite(availableLength) || availableLength <= 0.003)
        {
            _muzzleModel.Transform = new ScaleTransform3D(0, 0, 0);
            return;
        }

        var maximumLength = _horseRig
            ? Math.Max(0.015, Math.Min(_headScaleZ * 0.72, _animalBodyHeight * 0.10))
            : Math.Max(0.010, Math.Min(_headScaleZ * 0.58, _animalBodyHeight * 0.075));
        var visibleLength = Math.Min(availableLength, maximumLength);
        var start = headCenter + direction * (float)startDistance;
        var end = start + direction * (float)visibleLength;
        var widthFactor = _horseRig ? 0.09 : 0.10;
        var width = Math.Clamp(
            Math.Min(_headScaleX, _headScaleY) * widthFactor,
            0.003,
            _horseRig ? 0.014 : 0.012);

        _muzzleModel.Transform = PreviewGeometry.CreateSegmentTransform(
            start, end, width, 0.0025, Math.Max(0.03, visibleLength * 1.5));
    }

    private void ResolvePreferredFrontYaw(Vector3[] positions)
    {
        if (_rawObjectMode || _headBoneIndex < 0 || _headBoneIndex >= positions.Length)
            return;

        var landmarkIndices = _animalRig && _muzzleBoneIndices.Count > 0 ? _muzzleBoneIndices : _eyeBoneIndices;
        var landmarks = landmarkIndices
            .Where(index => index >= 0 && index < positions.Length && IsFinite(positions[index]))
            .Select(index => positions[index])
            .ToArray();
        if (landmarks.Length == 0)
            return;

        var average = new Vector3(
            landmarks.Average(value => value.X),
            landmarks.Average(value => value.Y),
            landmarks.Average(value => value.Z));
        var direction = average - positions[_headBoneIndex];
        direction.Y = 0;
        if (!IsFinite(direction) || direction.LengthSquared() < 0.000025f)
            return;
        PreferredFrontYaw = Math.Atan2(direction.X, direction.Z);
    }

    private PreviewBounds? ComputeAnimationBounds()
    {
        if (_clip is null || _evaluator is null)
            return CurrentBounds;
        var duration = _clip.DurationSeconds;
        if (duration <= 0.0001)
            return CurrentBounds;

        var sampleCount = Math.Clamp((int)Math.Ceiling(duration / 0.10) + 1, 12, 64);
        PreviewBounds? combined = null;
        for (var sample = 0; sample < sampleCount; sample++)
        {
            var time = duration * sample / (sampleCount - 1.0);
            var bounds = CalculateBounds(BuildViewerPositions(time));
            if (bounds is null)
                continue;
            combined = combined is null ? bounds : Union(combined.Value, bounds.Value);
        }
        return combined;
    }

    private PreviewBounds? CalculateBounds(Vector3[] positions)
    {
        var visible = _visibleBones.Select(index => positions[index]).Where(IsFinite).ToList();
        if (visible.Count == 0)
            return null;

        var minX = visible.Min(value => value.X);
        var minY = visible.Min(value => value.Y);
        var minZ = visible.Min(value => value.Z);
        var maxX = visible.Max(value => value.X);
        var maxY = visible.Max(value => value.Y);
        var maxZ = visible.Max(value => value.Z);
        if (!_rawObjectMode && _headBoneIndex >= 0 && _headSizeResolved)
        {
            var center = GetHeadCenter(positions);
            minX = (float)Math.Min(minX, center.X - _headScaleX);
            maxX = (float)Math.Max(maxX, center.X + _headScaleX);
            minY = (float)Math.Min(minY, center.Y - _headScaleY);
            maxY = (float)Math.Max(maxY, center.Y + _headScaleY);
            minZ = (float)Math.Min(minZ, center.Z - _headScaleZ);
            maxZ = (float)Math.Max(maxZ, center.Z + _headScaleZ);
        }
        return new PreviewBounds(new Point3D(minX, minY, minZ), new Point3D(maxX, maxY, maxZ));
    }

    private static PreviewBounds Union(PreviewBounds a, PreviewBounds b)
        => new(
            new Point3D(Math.Min(a.Min.X, b.Min.X), Math.Min(a.Min.Y, b.Min.Y), Math.Min(a.Min.Z, b.Min.Z)),
            new Point3D(Math.Max(a.Max.X, b.Max.X), Math.Max(a.Max.Y, b.Max.Y), Math.Max(a.Max.Z, b.Max.Z)));

    private static bool IsVisibleHumanBone(string value)
    {
        if (ContainsAny(value,
                "slot", "exportpole", "iktarget", "worldroot", "subroot", "facial", "tongue",
                "twist", "compress", "driver"))
            return false;

        return value.Contains("pelvis")
            || value.Contains("spine")
            || value.Contains("neck")
            || IsMainHeadBone(value)
            || value.Contains("clavicle")
            || value.Contains("upperarm")
            || value.Contains("forearm")
            || IsMainHandBone(value)
            || IsFingerBone(value)
            || value.Contains("thigh")
            || value.Contains("calf")
            || value.Contains("shin")
            || IsMainFootBone(value)
            || IsMainToeBone(value);
    }

    private static bool IsAnimalEndpoint(string value)
        => IsMainHeadBone(value)
            || IsAnimalPawOrFoot(value)
            || value.Contains("tail");

    private static bool IsAnimalCoreBone(string value)
        => value.Contains("pelvis")
            || value.Contains("spine")
            || value.Contains("torso")
            || value.Contains("neck")
            || IsMainHeadBone(value);

    private static bool IsAnimalSpineBone(string value)
        => value.Contains("spine") || value.Contains("back") || value.Contains("chest") || value.Contains("torso");

    private static bool IsAnimalPawOrFoot(string value)
        => ContainsAny(value, "paw", "hoof", "hand", "foot", "toe")
            && !ContainsAny(value, "slot", "target", "offset");

    private static bool IsMainAnimalPelvisBone(string value)
        => value.Contains("pelvis")
            && !ContainsAny(value,
                "target", "offset", "slot", "ik", "pole", "driver", "control", "ctrl", "root", "world");

    private static bool IsAnimalNoiseBone(string value)
        => ContainsAny(value,
            "rib", "breast", "belly", "stomach", "rump", "ulna", "radius", "bicep",
            "quadricep", "backcalf", "compress", "twist", "driver", "exportpole", "iktarget",
            "slot", "target", "worldroot", "subroot", "chest", "shoulder", "hip");

    private static bool IsRootLike(string value)
        => value.Contains("root") || value.Contains("world");

    private static double GetJointRadius(string value)
    {
        if (IsFingerBone(value))
            return 0.0065;
        if (IsMainToeBone(value) || IsAnimalGroundBone(value))
            return ToeJointRadius;
        if (value.Contains("forearm") || value.Contains("lowerarm"))
            return 0.0155;
        if (IsMainHandBone(value))
            return 0.011;
        if (IsMainFootBone(value))
            return FootJointRadius;
        if (value.Contains("tail"))
            return 0.008;
        return 0.014;
    }

    private static double GetLinkWidth(string value)
    {
        if (IsFingerBone(value))
            return 0.0035;
        if (IsMainToeBone(value) || value.Contains("tail"))
            return 0.006;
        return 0.009;
    }

    private static bool IsFinite(Vector3 value)
        => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static bool IsMainHeadBone(string value)
        => value.Contains("head")
            && !ContainsAny(value, "eye", "lid", "brow", "cheek", "lip", "mouth", "jaw", "nose", "ear", "tongue", "face");

    private static bool IsEyeBone(string value)
        => value.Contains("eye") && !ContainsAny(value, "lid", "brow", "target", "slot");

    private static bool IsFacialControlBone(string value)
        => ContainsAny(value, "eye", "lid", "brow", "cheek", "lip", "mouth", "jaw", "nose", "ear", "tongue", "face");

    private static bool IsMuzzleLandmark(string value)
        => !StudioRigBoneSemanticsV2.IsSlot(value)
            && !StudioRigBoneSemanticsV2.IsIk(value)
            && (value.Contains("muzzle") || value.Contains("snout") || value.Contains("nose"));

    private static bool IsMainHandBone(string value)
        => value.Contains("hand") && !ContainsAny(value, "target", "slot", "prop");

    private static bool IsMainFootBone(string value)
        => value.Contains("foot") && !ContainsAny(value, "target", "slot", "offset");

    private static bool IsMainToeBone(string value)
        => value.Contains("toe") && !ContainsAny(value, "target", "slot", "offset", "tip");

    private static bool IsAnimalGroundBone(string value)
        => ContainsAny(value, "paw", "hoof") && !ContainsAny(value, "target", "slot", "offset");

    private static bool IsFingerBone(string value)
        => ContainsAny(value, "thumb", "index", "mid0", "mid1", "mid2", "mid3", "middle", "ring", "pinky", "finger");

    private static bool IsAnimalRig(StudioRigV2 rig)
    {
        var names = rig.Bones.Select(bone => bone.Name.ToLowerInvariant()).ToArray();
        return names.Any(value => ContainsAny(value, "paw", "hoof", "muzzle", "snout", "tail"))
            && names.Any(value => ContainsAny(value, "spine", "pelvis", "chest", "torso", "back"));
    }

    private static bool IsHorseRig(StudioRigV2 rig)
        => rig.Bones.Any(bone => bone.Name.Contains("hoof", StringComparison.OrdinalIgnoreCase));

    private static bool IsLeftSideName(string value)
    {
        var lower = value.ToLowerInvariant();
        return ContainsAny(lower, "__l_", "_l_", "left");
    }

    private static bool IsRightSideName(string value)
    {
        var lower = value.ToLowerInvariant();
        return ContainsAny(lower, "__r_", "_r_", "right");
    }

    private static bool ContainsAny(string value, params string[] values)
        => values.Any(value.Contains);
}
