using System.Windows;
using System.Windows.Controls;

namespace PostGisTools.Core
{
    // Attached-property helper to allow MVVM binding to PasswordBox.Password.
    public static class PasswordBoxAssistant
    {
        public static readonly DependencyProperty BoundPasswordProperty =
            DependencyProperty.RegisterAttached(
                "BoundPassword",
                typeof(string),
                typeof(PasswordBoxAssistant),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

        public static readonly DependencyProperty BindPasswordProperty =
            DependencyProperty.RegisterAttached(
                "BindPassword",
                typeof(bool),
                typeof(PasswordBoxAssistant),
                new PropertyMetadata(false, OnBindPasswordChanged));

        private static readonly DependencyProperty UpdatingPasswordProperty =
            DependencyProperty.RegisterAttached(
                "UpdatingPassword",
                typeof(bool),
                typeof(PasswordBoxAssistant),
                new PropertyMetadata(false));

        public static void SetBoundPassword(DependencyObject d, string value) => d.SetValue(BoundPasswordProperty, value);
        public static string GetBoundPassword(DependencyObject d) => (string)d.GetValue(BoundPasswordProperty);

        public static void SetBindPassword(DependencyObject d, bool value) => d.SetValue(BindPasswordProperty, value);
        public static bool GetBindPassword(DependencyObject d) => (bool)d.GetValue(BindPasswordProperty);

        private static void SetUpdatingPassword(DependencyObject d, bool value) => d.SetValue(UpdatingPasswordProperty, value);
        private static bool GetUpdatingPassword(DependencyObject d) => (bool)d.GetValue(UpdatingPasswordProperty);

        private static void OnBindPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not PasswordBox pb) return;

            if ((bool)e.OldValue)
                pb.PasswordChanged -= HandlePasswordChanged;

            if ((bool)e.NewValue)
                pb.PasswordChanged += HandlePasswordChanged;
        }

        private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not PasswordBox pb) return;
            if (!GetBindPassword(pb)) return;
            if (GetUpdatingPassword(pb)) return;

            pb.Password = e.NewValue as string ?? string.Empty;
        }

        private static void HandlePasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is not PasswordBox pb) return;

            SetUpdatingPassword(pb, true);
            SetBoundPassword(pb, pb.Password);
            SetUpdatingPassword(pb, false);
        }
    }
}
