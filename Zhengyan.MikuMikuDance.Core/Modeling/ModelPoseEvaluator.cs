using System.Numerics;
using Zhengyan.MikuMikuDance.Core.Animation;

namespace Zhengyan.MikuMikuDance.Core.Modeling;

public static class ModelPoseEvaluator
{
    public static ModelPose Evaluate(MmdModel model, MotionSample sample)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(sample);
        return Evaluate(model, sample.Bones);
    }

    public static ModelPose Evaluate(MmdModel model, IReadOnlyDictionary<string, BonePoseSample> boneSamples)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(boneSamples);
        var states = CreateStates(model, boneSamples);
        var pose = BuildPose(model, states);
        if (model.Bones.Any(bone => bone.Ik is not null))
        {
            SolveIk(model, states);
            pose = BuildPose(model, states);
        }

        return pose;
    }

    private static MutableBonePose[] CreateStates(MmdModel model, IReadOnlyDictionary<string, BonePoseSample> boneSamples)
    {
        var states = new MutableBonePose[model.Bones.Count];
        for (var i = 0; i < states.Length; i++)
        {
            var bone = model.Bones[i];
            boneSamples.TryGetValue(bone.Name, out var sample);
            states[i] = new MutableBonePose(
                sample?.Translation ?? Vector3.Zero,
                sample?.Orientation ?? Quaternion.Identity);
        }

        return states;
    }

    private static ModelPose BuildPose(MmdModel model, IReadOnlyList<MutableBonePose> states)
    {
        var bones = new BonePose[model.Bones.Count];
        var evaluated = new bool[bones.Length];
        for (var i = 0; i < bones.Length; i++)
        {
            EvaluateBone(model, states, bones, evaluated, i);
        }

        return new ModelPose(bones);
    }

    private static BonePose EvaluateBone(
        MmdModel model,
        IReadOnlyList<MutableBonePose> states,
        BonePose[] bones,
        bool[] evaluated,
        int index)
    {
        if (evaluated[index])
        {
            return bones[index];
        }

        var bone = model.Bones[index];
        var state = states[index];
        var translation = state.Translation;
        var orientation = state.Orientation;
        var local = Matrix4x4.CreateFromQuaternion(orientation) * Matrix4x4.CreateTranslation(bone.Origin + translation);
        var world = local;
        if (bone.ParentBoneIndex >= 0 && bone.ParentBoneIndex < model.Bones.Count)
        {
            var parent = EvaluateBone(model, states, bones, evaluated, bone.ParentBoneIndex);
            world = Matrix4x4.CreateTranslation(-parent.Origin) * local * parent.WorldTransform;
        }

        var skinning = Matrix4x4.CreateTranslation(-bone.Origin) * world;
        bones[index] = new BonePose(
            bone.Name,
            bone.ParentBoneIndex,
            bone.Origin,
            translation,
            orientation,
            local,
            world,
            skinning);
        evaluated[index] = true;
        return bones[index];
    }

    private static void SolveIk(MmdModel model, MutableBonePose[] states)
    {
        for (var targetIndex = 0; targetIndex < model.Bones.Count; targetIndex++)
        {
            var targetBone = model.Bones[targetIndex];
            if (targetBone.Ik is null)
            {
                continue;
            }

            SolveIkConstraint(model, states, targetIndex, targetBone.Ik);
        }
    }

    private static void SolveIkConstraint(MmdModel model, MutableBonePose[] states, int targetIndex, IkConstraint constraint)
    {
        if (!IsValidBoneIndex(model, constraint.EffectorBoneIndex) || constraint.Links.Count == 0)
        {
            return;
        }

        var iterations = Math.Max(1, constraint.IterationCount);
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            foreach (var link in constraint.Links)
            {
                if (!IsValidBoneIndex(model, link.BoneIndex))
                {
                    continue;
                }

                var pose = BuildPose(model, states);
                var targetPosition = pose.Bones[targetIndex].WorldTransform.Translation;
                var effectorPosition = pose.Bones[constraint.EffectorBoneIndex].WorldTransform.Translation;
                var linkPose = pose.Bones[link.BoneIndex];
                var linkPosition = linkPose.WorldTransform.Translation;
                var toEffector = effectorPosition - linkPosition;
                var toTarget = targetPosition - linkPosition;
                if (toEffector.LengthSquared() < 1e-8f || toTarget.LengthSquared() < 1e-8f)
                {
                    continue;
                }

                var rotation = CreateRotationBetween(Vector3.Normalize(toEffector), Vector3.Normalize(toTarget));
                rotation = LimitRotation(rotation, constraint.AngleLimit);
                if (rotation == Quaternion.Identity)
                {
                    continue;
                }

                var linkWorldRotation = ExtractRotation(linkPose.WorldTransform);
                var parentWorldRotation = Quaternion.Identity;
                if (linkPose.ParentBoneIndex >= 0 && linkPose.ParentBoneIndex < pose.Bones.Count)
                {
                    parentWorldRotation = ExtractRotation(pose.Bones[linkPose.ParentBoneIndex].WorldTransform);
                }

                var newWorldRotation = Quaternion.Normalize(linkWorldRotation * rotation);
                var localRotation = Quaternion.Normalize(newWorldRotation * Quaternion.Inverse(parentWorldRotation));
                states[link.BoneIndex].Orientation = ClampLinkRotation(localRotation, link);
            }
        }
    }

    private static bool IsValidBoneIndex(MmdModel model, int index)
    {
        return index >= 0 && index < model.Bones.Count;
    }

    private static Quaternion CreateRotationBetween(Vector3 from, Vector3 to)
    {
        var dot = Math.Clamp(Vector3.Dot(from, to), -1f, 1f);
        if (dot > 0.9999f)
        {
            return Quaternion.Identity;
        }

        if (dot < -0.9999f)
        {
            var axis = Vector3.Cross(from, Vector3.UnitX);
            if (axis.LengthSquared() < 1e-8f)
            {
                axis = Vector3.Cross(from, Vector3.UnitY);
            }

            return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.PI);
        }

        var rotationAxis = Vector3.Normalize(Vector3.Cross(from, to));
        var angle = MathF.Acos(dot);
        return Quaternion.CreateFromAxisAngle(rotationAxis, angle);
    }

    private static Quaternion LimitRotation(Quaternion rotation, float angleLimit)
    {
        if (angleLimit <= 0)
        {
            return rotation;
        }

        rotation = Quaternion.Normalize(rotation);
        var angle = 2f * MathF.Acos(Math.Clamp(rotation.W, -1f, 1f));
        if (angle <= angleLimit)
        {
            return rotation;
        }

        return Quaternion.Normalize(Quaternion.Slerp(Quaternion.Identity, rotation, angleLimit / angle));
    }

    private static Quaternion ClampLinkRotation(Quaternion rotation, IkLink link)
    {
        if (!link.AngleLimitEnabled)
        {
            return rotation;
        }

        var euler = ToPitchYawRoll(rotation);
        euler = new Vector3(
            Math.Clamp(euler.X, link.LowerLimit.X, link.UpperLimit.X),
            Math.Clamp(euler.Y, link.LowerLimit.Y, link.UpperLimit.Y),
            Math.Clamp(euler.Z, link.LowerLimit.Z, link.UpperLimit.Z));
        return Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(euler.Y, euler.X, euler.Z));
    }

    private static Vector3 ToPitchYawRoll(Quaternion rotation)
    {
        rotation = Quaternion.Normalize(rotation);

        var sinPitch = 2f * ((rotation.W * rotation.X) - (rotation.Y * rotation.Z));
        var pitch = MathF.Asin(Math.Clamp(sinPitch, -1f, 1f));

        var sinYaw = 2f * ((rotation.W * rotation.Y) + (rotation.Z * rotation.X));
        var cosYaw = 1f - (2f * ((rotation.X * rotation.X) + (rotation.Y * rotation.Y)));
        var yaw = MathF.Atan2(sinYaw, cosYaw);

        var sinRoll = 2f * ((rotation.W * rotation.Z) + (rotation.X * rotation.Y));
        var cosRoll = 1f - (2f * ((rotation.X * rotation.X) + (rotation.Z * rotation.Z)));
        var roll = MathF.Atan2(sinRoll, cosRoll);

        return new Vector3(pitch, yaw, roll);
    }

    private static Quaternion ExtractRotation(Matrix4x4 matrix)
    {
        return Matrix4x4.Decompose(matrix, out _, out var rotation, out _)
            ? Quaternion.Normalize(rotation)
            : Quaternion.Identity;
    }

    private sealed record MutableBonePose(Vector3 Translation, Quaternion Orientation)
    {
        public Vector3 Translation { get; set; } = Translation;

        public Quaternion Orientation { get; set; } = Orientation;
    }
}
