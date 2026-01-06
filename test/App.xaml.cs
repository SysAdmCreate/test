using System;

namespace test;

public partial class App : Application
{
	public static IServiceProvider? Services { get; private set; }

	public App(IServiceProvider services)
	{
		InitializeComponent();
		Services = services;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}
