﻿namespace Cartridge.Mobile;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();

		// Handle WebView navigation events
		CartridgeWebView.Navigating += OnWebViewNavigating;
		CartridgeWebView.Navigated += OnWebViewNavigated;
	}

	private void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
	{
		// Show loading indicator when navigation starts
		LoadingIndicator.IsVisible = true;
		LoadingIndicator.IsRunning = true;
	}

	private void OnWebViewNavigated(object? sender, WebNavigatedEventArgs e)
	{
		// Hide loading indicator when navigation completes
		LoadingIndicator.IsVisible = false;
		LoadingIndicator.IsRunning = false;

		// Handle navigation result
		if (e.Result != WebNavigationResult.Success)
		{
			DisplayAlert("Error", $"Failed to load page: {e.Result}", "OK");
		}
	}
}
