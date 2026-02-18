using Frontend.Components.Toasts;
using System.Collections.Concurrent;
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Frontend.Services
{
    public static class ToastService
    {
        private static Grid? _toastContainer;
        private static readonly ConcurrentQueue<ToastNotification> _toastQueue = new ConcurrentQueue<ToastNotification>();
        private static bool _isShowingToast = false;

        public static void Initialize(Grid toastContainer)
        {
            _toastContainer = toastContainer ?? throw new ArgumentNullException(nameof(toastContainer));
        }

        public static void ShowToast(string message, ToastType type = ToastType.Info, int durationMillis = 3000)
        {
            if (_toastContainer == null)
            {
                Console.WriteLine("ToastService not initialized. Call Initialize(toastContainer) first.");
                return;
            }

            var toast = new ToastNotification
            {
                Message = message,
                ToastType = type,
                DurationMillis = durationMillis, // Assign duration here
                HorizontalOptions = LayoutOptions.End, // Align to the right
                VerticalOptions = LayoutOptions.Start,  // Align to the top
                Margin = new Thickness(0, 10, 10, 0) // Margin from top and right
            };

            _toastQueue.Enqueue(toast);
            TryShowNextToast();
        }

        private static async void TryShowNextToast()
        {
            if (_isShowingToast || _toastContainer == null) return;

            if (_toastQueue.TryDequeue(out var toast))
            {
                _isShowingToast = true;

                // Ensure UI updates are on the main thread
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    // Add toast to the container
                    _toastContainer.Children.Add(toast);

                    // Animate position: slide in from right top (initial position will be off-screen top-right)
                    toast.TranslationY = -100; // Start off-screen above
                    await toast.TranslateToAsync(0, 0, 250, Easing.SpringOut); // Slide down into view
                    
                    // Show for duration
                    await toast.Show(toast.Message, toast.ToastType, toast.DurationMillis); // Use assigned duration

                    // Animate position: slide out to right top
                    await toast.TranslateToAsync(0, -100, 250, Easing.SpringIn); // Slide up out of view

                    // Remove toast from container
                    _toastContainer.Children.Remove(toast);

                    _isShowingToast = false;
                    TryShowNextToast(); // Try to show next toast in queue
                });
            }
        }
    }
}