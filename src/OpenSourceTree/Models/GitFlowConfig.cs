namespace OpenSourceTree.Models;

public sealed record GitFlowConfig(
    string Master,
    string Develop,
    string FeaturePrefix,
    string ReleasePrefix,
    string HotfixPrefix,
    string VersionTagPrefix,
    bool IsInitialized);
