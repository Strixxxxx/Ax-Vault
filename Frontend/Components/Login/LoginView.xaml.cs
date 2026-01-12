using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

namespace Frontend.Components.Login
{
    public partial class LoginView : ContentView
    {
        public event EventHandler<Login.LoginSuccessEventArgs>? LoginSuccessful;
        public event EventHandler? RegisterRequested;

        public LoginView()
        {
            InitializeComponent();
            this.Loaded += OnLoginViewLoaded;
            LoginComponent.LoginCompleted += (s, args) => LoginSuccessful?.Invoke(s, args);
            LoginComponent.RegisterRequested += (s, e) => RegisterRequested?.Invoke(this, EventArgs.Empty);
        }

        public void ResetForm()
        {
            LoginComponent.ResetForm();
        }

        private async void OnLoginViewLoaded(object? sender, EventArgs e)
        {
            await AnimateIntro();
        }

        private async Task AnimateIntro()
        {
            // Cancel any existing animations to avoid conflicts
            this.AbortAnimation("DesktopIntro");
            this.AbortAnimation("MobileIntro");

            bool isDesktop = this.Width > 700;

            // Set the image height programmatically
            BrandingImage.HeightRequest = isDesktop ? 220 : 110;

            // Set initial visual states
            ContentArea.Opacity = 0;
            BrandingPositioningGrid.TranslationX = 0;
            BrandingContainer.Opacity = 1;
    
            if (isDesktop)
            {
                // --- DESKTOP ANIMATION ---
                VisualStateManager.GoToState(MainGrid, "Wide");
                Divider.Opacity = 0;
        
                // Let the layout settle to get correct dimensions
                await Task.Delay(50);

                // Calculate the width of the left column (3* portion)
                // Total grid width minus divider width, then calculate the 3/5 portion for left column
                double dividerWidth = 2; // From WidthRequest in XAML
                double gridWidth = this.Width;
                double leftColumnWidth = (gridWidth - dividerWidth) * (3.0 / 5.0);
                
                // Calculate centers
                double screenCenterX = gridWidth / 2.0;
                double leftColumnCenterX = leftColumnWidth / 2.0;
                
                // The translation needed to move from screen center to left column center
                double finalTranslationX = leftColumnCenterX - screenCenterX;

                // Wait for the 3-second intro period
                await Task.Delay(3000);

                // Animate everything to its final state
                var desktopAnimation = new Animation();

                // Animate BrandingPositioningGrid moving from center to left column center
                desktopAnimation.Add(0, 1, new Animation(v => BrandingPositioningGrid.TranslationX = v, 0, finalTranslationX, Easing.CubicInOut));

                // Stagger the fade-in of the divider and the login form
                desktopAnimation.Add(0.2, 0.7, new Animation(v => Divider.Opacity = v, 0, 1, Easing.SinIn));
                desktopAnimation.Add(0.4, 1.0, new Animation(v => ContentArea.Opacity = v, 0, 1, Easing.SinIn));

                // Commit the animation with a duration of 1.8 seconds
                desktopAnimation.Commit(this, "DesktopIntro", 16, 1800);
            }
            else
            {
                // --- MOBILE ANIMATION (Unchanged) ---
                VisualStateManager.GoToState(MainGrid, "Narrow");
                ContentArea.TranslationY = this.Height;

                await Task.Delay(3000);

                var mobileAnimation = new Animation();
        
                // Fade out the branding
                mobileAnimation.Add(0, 0.5, new Animation(v => BrandingContainer.Opacity = v, 1, 0));
        
                // Move the content area up and fade it in
                mobileAnimation.Add(0.3, 1, new Animation(v => ContentArea.TranslationY = v, this.Height, 0, Easing.CubicOut));
                mobileAnimation.Add(0.3, 1, new Animation(v => ContentArea.Opacity = v, 0, 1));

                mobileAnimation.Commit(this, "MobileIntro", 16, 1600, null, (v, c) => {
                    BrandingContainer.IsVisible = false;
                });
            }
        }
    }
}