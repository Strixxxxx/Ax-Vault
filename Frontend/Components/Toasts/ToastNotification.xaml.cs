using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

namespace Frontend.Components.Toasts
{
    public partial class ToastNotification : ContentView
    {
        public static readonly BindableProperty MessageProperty =
            BindableProperty.Create(nameof(Message), typeof(string), typeof(ToastNotification), string.Empty);

        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public static readonly BindableProperty ToastTypeProperty =
            BindableProperty.Create(nameof(ToastType), typeof(ToastType), typeof(ToastNotification), ToastType.Info, propertyChanged: OnToastTypeChanged);

        public ToastType ToastType
        {
            get => (ToastType)GetValue(ToastTypeProperty);
            set => SetValue(ToastTypeProperty, value);
        }

        public int DurationMillis { get; set; } = 3000; // New property for duration

        private static void OnToastTypeChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is ToastNotification toast)
            {
                toast.UpdateToastStyle((ToastType)newValue);
            }
        }

        public ToastNotification()
        {
            InitializeComponent();
            IsVisible = false; // Initially hidden
        }

        private void UpdateToastStyle(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success:
                    ((Border)Content).BackgroundColor = Color.FromArgb("#00e5ff"); // Neon Blue
                    MessageLabel.TextColor = Colors.White;
                    break;
                case ToastType.Error:
                    ((Border)Content).BackgroundColor = Color.FromArgb("#ff073a"); // Neon Red
                    MessageLabel.TextColor = Colors.White;
                    break;
                case ToastType.Info:
                default:
                    ((Border)Content).BackgroundColor = Color.FromArgb("#00e5ff"); // Neon Blue
                    MessageLabel.TextColor = Colors.Black;
                    break;
            }
        }

        public async Task Show(string message, ToastType type = ToastType.Info, int durationMillis = 3000)
        {
            Message = message;
            ToastType = type;
            IsVisible = true;
            Opacity = 0;

            await this.FadeToAsync(1, 250); // Fade in

            await Task.Delay(durationMillis);

            await this.FadeToAsync(0, 250); // Fade out
            IsVisible = false;
        }
    }

    public enum ToastType
    {
        Info,
        Success,
        Error
    }
}