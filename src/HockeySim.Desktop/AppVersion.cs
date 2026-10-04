using System.Reflection;

namespace HockeySim.Desktop;

/// <summary>
/// The running release version, which is also the engine version that bounds the
/// reproducibility of saves. Release builds take it from the Git tag; source builds
/// report the development version set in Directory.Build.props.
/// </summary>
public static class AppVersion
{
    public static string Current { get; } =
        typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
        ?? throw new InvalidOperationException("The application assembly has no informational version.");
}