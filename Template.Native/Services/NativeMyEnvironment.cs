namespace Template.Native.Services;

/// <summary>
/// MAUI-specific environment detection
/// </summary>
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "Must be public for XAML binding in MAUI projects")]
public class NativeMyEnvironment : MyEnvironment
{
    public override bool IsNative() => true;
    public override bool IsWeb() => false;

    public override string GetEnvironment()
    {
#if DEBUG
        return Dev;
#else
        return Prod;
#endif
    }
}
