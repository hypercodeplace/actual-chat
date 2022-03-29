namespace ActualChat.ClientApp
{
    public partial class App : Application
    {
        public App(ClientAppSettings settings)
        {
            InitializeComponent();
            MainPage = new MainPage(settings);
        }
    }
}
