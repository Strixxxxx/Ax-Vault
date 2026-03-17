namespace Frontend.Components.ForgotPassword.Steps
{
    public partial class ChoiceStep : ContentView
    {
        public event EventHandler? OtpSelected;
        public event EventHandler? VaultSelected;
        public event EventHandler? BackRequested;

        public ChoiceStep() { InitializeComponent(); }

        private void OnOtpSelected(object sender, EventArgs e)
        {
            if (LoadingIndicator.IsRunning) return;
            OtpSelected?.Invoke(this, EventArgs.Empty);
        }

        private void OnVaultSelected(object sender, EventArgs e)
        {
            if (LoadingIndicator.IsRunning) return;
            VaultSelected?.Invoke(this, EventArgs.Empty);
        }

        private void OnBackTapped(object sender, EventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);

        public void SetLoading(bool loading)
        {
            LoadingIndicator.IsRunning = loading;
            LoadingIndicator.IsVisible = loading;
        }

        public void Reset() { SetLoading(false); }
    }
}
