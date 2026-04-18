namespace Template.Native.Services;

/// <summary>
/// MAUI-specific environment detection
/// </summary>
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
