namespace Template.Native;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "Must be public for XAML binding in MAUI projects")]
public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new MainPage()) { Title = "Template.Native" };
	}
}
