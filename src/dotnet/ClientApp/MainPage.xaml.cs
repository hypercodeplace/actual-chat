using Microsoft.AspNetCore.Components.WebView;

namespace ActualChat.ClientApp;

public partial class MainPage : ContentPage
{
    private readonly string[] _allowedUrls;
    public MainPage(ClientAppSettings settings)
    {
        _allowedUrls = new[]{
            settings.BaseUri,
            "https://accounts.google.com/",
            "https://login.live.com/",
            "https://login.microsoftonline.com/",
        };
        InitializeComponent();
    }

    private void BlazorWebView_ExternalNavigationStarting(object sender, ExternalLinkNavigationEventArgs e)
    {

        if (_allowedUrls.Any(x => e.Uri.AbsoluteUri.StartsWith(x, StringComparison.OrdinalIgnoreCase))) {
            Debug.WriteLine($"Allowed external navigation to {e.Uri}");
            e.ExternalLinkNavigationPolicy = ExternalLinkNavigationPolicy.InsecureOpenInWebView;
        }
        else {
            Debug.WriteLine($"External navigation to {e.Uri}");
        }
    }
}
