using Microsoft.Web.WebView2.Core;
using WebView2Control = Microsoft.UI.Xaml.Controls.WebView2;
namespace ActualChat.ClientApp;



public partial class MauiBlazorWebViewHandler
{
    private record JsMessage(string type, string url);
    /// <inheritdoc />
    protected override WebView2Control CreatePlatformView()
    {
        var webview = new WebView2Control();
        var baseUri = _settings.BaseUri;
        if (!_settings.BaseUri.EndsWith('/'))
            baseUri += '/';
        webview.EnsureCoreWebView2Async().AsTask().ContinueWith((t, state) => {
            var ctrl = (WebView2Control)state!;
            ctrl.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/98.0.4758.141 Safari/537.36";

            ctrl.NavigationStarting += (WebView2Control sender, CoreWebView2NavigationStartingEventArgs args) => {

            };

            ctrl.CoreWebView2.NavigationStarting += (CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args) => {

            };

            ctrl.WebMessageReceived += static (WebView2Control sender, CoreWebView2WebMessageReceivedEventArgs args) => {
                var json = args.TryGetWebMessageAsString();
                if (!string.IsNullOrWhiteSpace(json) && json.Length > 10 && json[0] == '{') {
                    try {
                        var msg = System.Text.Json.JsonSerializer.Deserialize<JsMessage>(json);
                        if (msg == null)
                            return;
                        switch (msg.type) {
                            case "_navigateTo":
                                sender.Source = new Uri(msg.url);
                                Debug.WriteLine($"_navigateTo: {msg.url}");
                                break;
                            default:
                                throw new InvalidOperationException($"Unknown message type: {msg.type}");
                        }
                    }
                    catch (Exception ex) {
                        Debug.WriteLine(ex.ToString());
                    }
                }
            };

#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
            ctrl.CoreWebView2.DOMContentLoaded += async (_, __) => {
                try {
                    await webview.CoreWebView2.ExecuteScriptAsync($"window['_baseURI'] = '{baseUri}'; audio.OpusMediaRecorder.origin='{baseUri}'; " +
                        "window.navigator.userAgentData = [{\"brand\": \"Chromium\", \"version\": \"98\"},{\"brand\": \" Not A;Brand\",\"version\": \"99\"}]; ");
                }
                catch (Exception ex) {
                    Debug.WriteLine(ex.ToString());
                }
            };
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates
            ctrl.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            ctrl.CoreWebView2.WebResourceRequested += (CoreWebView2 _, CoreWebView2WebResourceRequestedEventArgs args) => {
                using var deferral = args.GetDeferral();
                /// wss:// can't be rewrited, so we should change SignalR script on the fly
                /// <see href="https://github.com/MicrosoftEdge/WebView2Feedback/issues/172" />
                /// <see href="https://github.com/MicrosoftEdge/WebView2Feedback/issues/685" />
                var uri = args.Request.Uri;
                if (uri.StartsWith("https://0.0.0.0/api/", StringComparison.Ordinal)) {
                    args.Request.Uri = uri.Replace("https://0.0.0.0/", baseUri, StringComparison.Ordinal);
                    args.Request.Headers.SetHeader("Origin", baseUri);
                    args.Request.Headers.SetHeader("Referer", baseUri);
                    Debug.WriteLine($"webview.WebResourceRequested: rewrited to {args.Request.Uri}");
                }
                // workaround of 'This browser or app may not be secure.'
                else if (uri.StartsWith("https://accounts.google.com", StringComparison.Ordinal)) {
                    args.Request.Headers.SetHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/98.0.4758.141 Safari/537.36");
                    args.Request.Headers.SetHeader("sec-ch-ua", "\"Chromium\";v=\"98\", \" Not A;Brand\";v=\"99\"");
                }
                else {
                    Debug.WriteLine($"webview.WebResourceRequested: {uri}");
                }
            };


#if DEBUG
            ctrl.CoreWebView2.Settings.AreDevToolsEnabled = true;
#else
            ctrl.CoreWebView2.Settings.AreDevToolsEnabled = false;
#endif
        }, webview, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.FromCurrentSynchronizationContext());

        return webview;
    }
}
