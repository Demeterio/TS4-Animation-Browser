namespace TS4AnimationBrowser.Core.Animation;

// Values are the ChannelType byte used by The Sims 4 S3CLIP codec.
public enum ClipChannelType : byte
{
    Unknown = 0,
    F1 = 1,
    F2 = 2,
    F3 = 3,
    F4 = 4,
    F1Normalized = 5,
    F2Normalized = 6,
    F3Normalized = 7,
    F4Normalized = 8,
    F1Zero = 9,
    F2Zero = 10,
    F3Zero = 11,
    F4Zero = 12,
    F1One = 13,
    F2One = 14,
    F3One = 15,
    F4One = 16,
    F4QuaternionIdentity = 17,
    F3HighPrecisionNormalized = 18,
    F4HighPrecisionNormalizedQuaternion = 19,
    F4SuperHighPrecisionQuaternion = 20,
    F3HighPrecisionNormalizedQuaternion = 21
}

public enum ClipSubTarget : byte
{
    Unknown = 0,
    Translation = 1,
    Orientation = 2,
    Scale = 3,
    TranslationX = 4,
    TranslationY = 5,
    TranslationZ = 6,
    OrientationX = 7,
    OrientationY = 8,
    OrientationZ = 9,
    OrientationW = 10,
    ScaleX = 11,
    ScaleY = 12,
    ScaleZ = 13,
    IkTargetWeightWorld = 14,
    IkTargetWeight1 = 15,
    IkTargetWeight2 = 16,
    IkTargetWeight3 = 17,
    IkTargetWeight4 = 18,
    IkTargetWeight5 = 19,
    IkTargetWeight6 = 20,
    IkTargetWeight7 = 21,
    IkTargetWeight8 = 22,
    IkTargetWeight9 = 23,
    IkTargetWeight10 = 24,
    IkTargetOffsetTranslationWorld = 25,
    IkTargetOffsetOrientationWorld = 26
}
