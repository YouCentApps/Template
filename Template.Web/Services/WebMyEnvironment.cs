namespace Template.Web.Services;

/// <summary>
/// Web-specific environment detection
/// </summary>
internal sealed class WebMyEnvironment : MyEnvironment
{
    public override bool IsNative() => false;
    public override bool IsWeb() => true;

    public override string GetEnvironment()
    {
#if DEBUG
        return Dev;
#else
        return Prod;
#endif
    }
}
