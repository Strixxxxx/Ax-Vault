using Microsoft.Maui.Controls;
using Frontend.Components.ForgotPassword.Steps;

namespace Frontend.Components.ForgotPassword
{
    public partial class ForgotPasswordComponent : ContentView
    {
        // State shared between steps
        internal long AccountId { get; set; }
        internal string SelectedMethod { get; set; } = string.Empty; // "OTP" or "VAULT"

        public static event EventHandler? BackToLoginRequested;

        // Step instances
        private UsernameStep _usernameStep = null!;
        private ChoiceStep _choiceStep = null!;
        private OtpStep _otpStep = null!;
        private VaultStep _vaultStep = null!;
        private ResetStep _resetStep = null!;
        private SuccessStep _successStep = null!;

        public ForgotPasswordComponent()
        {
            InitializeComponent();
            BuildSteps();
        }

        private void BuildSteps()
        {
            _usernameStep = new UsernameStep();
            _choiceStep = new ChoiceStep();
            _otpStep = new OtpStep();
            _vaultStep = new VaultStep();
            _resetStep = new ResetStep();
            _successStep = new SuccessStep();

            UsernameStepView.Content = _usernameStep;
            ChoiceStepView.Content = _choiceStep;
            OtpStepView.Content = _otpStep;
            VaultStepView.Content = _vaultStep;
            ResetStepView.Content = _resetStep;
            SuccessStepView.Content = _successStep;

            // Wire events
            _usernameStep.Proceed += (s, accountId) => { AccountId = accountId; ShowStep("choice"); };
            _usernameStep.BackRequested += (s, e) => RaiseBackToLogin();

            _choiceStep.OtpSelected += async (s, e) => { SelectedMethod = "OTP"; await SendOtpAndShowStep(); };
            _choiceStep.VaultSelected += (s, e) => { SelectedMethod = "VAULT"; ShowStep("vault"); };
            _choiceStep.BackRequested += (s, e) => ShowStep("username");

            _otpStep.Proceed += (s, e) => ShowStep("reset");
            _otpStep.BackRequested += (s, e) => ShowStep("choice");

            _vaultStep.Proceed += (s, e) => ShowStep("reset");
            _vaultStep.BackRequested += (s, e) => ShowStep("choice");

            _resetStep.Proceed += (s, e) => ShowStep("success");
            _resetStep.BackRequested += (s, e) => ShowStep("choice");

            _successStep.Done += (s, e) => RaiseBackToLogin();
        }

        private async Task SendOtpAndShowStep()
        {
            _choiceStep.SetLoading(true);
            var maskedEmail = await _otpStep.RequestOtp(AccountId);
            _choiceStep.SetLoading(false);
            if (maskedEmail != null)
            {
                _otpStep.SetAccountInfo(AccountId, maskedEmail);
                ShowStep("otp");
            }
        }

        public void Reset()
        {
            AccountId = 0;
            SelectedMethod = string.Empty;
            _usernameStep.Reset();
            _choiceStep.Reset();
            _otpStep.Reset();
            _vaultStep.Reset();
            _resetStep.Reset();
            ShowStep("username");
        }

        private void ShowStep(string step)
        {
            UsernameStepView.IsVisible = step == "username";
            ChoiceStepView.IsVisible   = step == "choice";
            OtpStepView.IsVisible      = step == "otp";
            VaultStepView.IsVisible    = step == "vault";
            ResetStepView.IsVisible    = step == "reset";
            SuccessStepView.IsVisible  = step == "success";

            // Pass state to steps that need it
            if (step == "vault") _vaultStep.SetAccountId(AccountId);
            if (step == "reset") _resetStep.SetAccountId(AccountId);

            // Start countdown when success is displayed
            if (step == "success") _successStep.StartCountdown();
            else _successStep.StopCountdown();
        }

        private static void RaiseBackToLogin()
        {
            BackToLoginRequested?.Invoke(null, EventArgs.Empty);
        }
    }
}
