namespace Frontend.Components.ForgotPassword.Steps
{
    public partial class SuccessStep : ContentView
    {
        public event EventHandler? Done;

        private IDispatcherTimer? _timer;
        private int _countdown = 5;

        public SuccessStep() { InitializeComponent(); }

        protected override void OnParentSet()
        {
            base.OnParentSet();
        }

        /// <summary>
        /// Called when this step becomes visible to start the countdown.
        /// </summary>
        public void StartCountdown()
        {
            _countdown = 5;
            CountdownLabel.Text = $"Redirecting in {_countdown} seconds...";

            _timer = Application.Current!.Dispatcher.CreateTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) =>
            {
                _countdown--;
                CountdownLabel.Text = $"Redirecting in {_countdown} seconds...";
                if (_countdown <= 0)
                {
                    _timer.Stop();
                    Done?.Invoke(this, EventArgs.Empty);
                }
            };
            _timer.Start();
        }

        public void StopCountdown()
        {
            _timer?.Stop();
            _timer = null;
        }
    }
}
