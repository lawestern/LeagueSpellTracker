using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Controls.Primitives;

namespace LeagueSpellTracker
{
    public partial class MainWindow : Window
    {
        private ScaleTransform _windowScale;
        private const double BASE_WIDTH = 160;
        private const double BASE_HEIGHT = 450;
        private readonly Dictionary<string, (double Width, double Height)> _baseElementSizes;
        private Config _config;
        private Dictionary<Button, DispatcherTimer> flashTimers = new Dictionary<Button, DispatcherTimer>();
        private bool _isLoadingConfig = false;


        public MainWindow()
        {
            InitializeComponent();
            
            // ViewBoxの初期サイズを設定
            mainViewbox.Width = BASE_WIDTH;
            mainViewbox.Height = BASE_HEIGHT;
            
            // ウィンドウの初期サイズを設定
            this.Width = BASE_WIDTH;
            this.Height = BASE_HEIGHT;
            
            this.Loaded += Window_Loaded;
            
            _baseElementSizes = new Dictionary<string, (double, double)>
            {
                { "FlashButton", (64, 64) },
                { "CooldownButton", (48, 28) },
                { "LaneIcon", (32, 24) },
                { "HeaderButton", (20, 20) }
            };
            
            InitializeWindowScale();
            
            // DataContextを設定
            _config = Config.Load();
            this.DataContext = _config;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoadingConfig = true;
            
            // スケールの設定を適用
            scaleSlider.Value = _config.Scale;
            if (_config.Scale != 1.0)
            {
                UpdateWindowScale(_config.Scale);
            }
            
            // ウィンドウ位置を設定
            this.Left = _config.WindowLeft;
            this.Top = _config.WindowTop;
            
            _isLoadingConfig = false;
        }

        private void InitializeWindowScale()
        {
            _windowScale = new ScaleTransform(1, 1);
        }

        private void UpdateWindowScale(double scale)
        {
            if (this.IsLoaded && LayoutRoot != null && mainViewbox != null)
            {
                // 現在のウィンドウの位置を保存
                double left = this.Left;
                double top = this.Top;

                // ViewBoxのサイズを更新
                double scaledWidth = BASE_WIDTH * scale;
                double scaledHeight = BASE_HEIGHT * scale;
                
                mainViewbox.Width = scaledWidth;
                mainViewbox.Height = scaledHeight;
                
                // ウィンドウサイズを更新
                this.Width = scaledWidth;
                this.Height = scaledHeight;

                // 保存した位置に戻す
                this.Left = left;
                this.Top = top;
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!settingsPopup.IsOpen)
            {
                Point buttonPosition = btnSettings.TranslatePoint(new Point(0, 0), this);
                
                settingsPopup.PlacementTarget = btnSettings;
                settingsPopup.Placement = PlacementMode.Custom;
                settingsPopup.CustomPopupPlacementCallback = (popupSize, targetSize, offset) =>
                {
                    // ポップアップをボタンの上に配置（Y座標からポップアップの高さを引く）
                    return new[] { new CustomPopupPlacement(
                        new Point(buttonPosition.X, buttonPosition.Y - popupSize.Height), 
                        PopupPrimaryAxis.None) };
                };
            }
            
            settingsPopup.IsOpen = !settingsPopup.IsOpen;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnFlash_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            
            // 現在の状態に基づいて開始時間を取得
            int startCooldown = GetFlashCooldown(button);
            // 10秒を引いた時間からスタート
            int adjustedCooldown = Math.Max(0, startCooldown - 10);
            
            ImageBrush imageBrush = (ImageBrush)button.Background;
            imageBrush.Opacity = 0.2;

            TextBlock timerText = new TextBlock
            {
                Text = FormatTime(adjustedCooldown),
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 16
            };
            
            button.Content = timerText;
            StartFlashTimer(button, adjustedCooldown);
        }

        private void BtnFlash_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Released)  // ホイールクリックのみ
            {
                Button button = (Button)sender;
                ImageBrush imageBrush = (ImageBrush)button.Background;
                imageBrush.Opacity = 1.0;
                
                // タイマーテキストを削除
                button.Content = null;
                
                // タイマーが存在する場合は停止
                if (flashTimers.ContainsKey(button))
                {
                    flashTimers[button].Stop();
                    flashTimers.Remove(button);
                }
            }
        }

        private void BtnFlash_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Button button = (Button)sender;
            if (button.Content is TextBlock timerText)
            {
                // 現在の秒数を取得
                int currentSeconds;
                if (timerText.Text.Contains(":"))
                {
                    string[] parts = timerText.Text.Split(':');
                    currentSeconds = (int.Parse(parts[0])) * 60 + int.Parse(parts[1]);
                }
                else
                {
                    currentSeconds = int.Parse(timerText.Text);
                }

                // ホイール1回転で5秒調整（上で増加、下で減少）
                int adjustment = e.Delta > 0 ? 5 : -5;
                int newSeconds = Math.Max(0, currentSeconds + adjustment);

                // タイマーを更新
                if (flashTimers.ContainsKey(button))
                {
                    flashTimers[button].Stop();
                    StartFlashTimer(button, newSeconds);
                }
            }
        }

        private Button GetFlashButtonForTimeButton(Button timeButton)
        {
            StackPanel stackPanel = (StackPanel)timeButton.Parent;
            Grid parentGrid = (Grid)stackPanel.Parent;
            int row = Grid.GetRow(stackPanel);
            
            // 同じ行のフラッシュボタンを探す
            foreach (UIElement element in parentGrid.Children)
            {
                if (element is Button button && 
                    Grid.GetRow(element) == row && 
                    Grid.GetColumn(element) == 1)  // フラッシュボタンは常にColumn 1にある
                {
                    return button;
                }
            }
            return null;
        }

        private void Btn30_Click(object sender, RoutedEventArgs e)
        {
            Button timeButton = (Button)sender;
            Button flashButton = GetFlashButtonForTimeButton(timeButton);
            
            if (flashButton != null)
            {
                // 現在の状態に基づいて開始時間を取得
                int startCooldown = GetFlashCooldown(flashButton);
                // 30秒を引いた時間からスタート
                int adjustedCooldown = Math.Max(0, startCooldown - 30);
                
                ImageBrush imageBrush = (ImageBrush)flashButton.Background;
                imageBrush.Opacity = 0.2;

                TextBlock timerText = new TextBlock
                {
                    Text = FormatTime(adjustedCooldown),
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 16
                };
                
                flashButton.Content = timerText;
                StartFlashTimer(flashButton, adjustedCooldown);
            }
        }

        private void Btn60_Click(object sender, RoutedEventArgs e)
        {
            Button timeButton = (Button)sender;
            Button flashButton = GetFlashButtonForTimeButton(timeButton);
            
            if (flashButton != null)
            {
                // 現在の状態に基づて開始時間を取得
                int startCooldown = GetFlashCooldown(flashButton);
                // 60秒を引いた時間からスタート
                int adjustedCooldown = Math.Max(0, startCooldown - 60);
                
                ImageBrush imageBrush = (ImageBrush)flashButton.Background;
                imageBrush.Opacity = 0.2;

                TextBlock timerText = new TextBlock
                {
                    Text = FormatTime(adjustedCooldown),
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 16
                };
                
                flashButton.Content = timerText;
                StartFlashTimer(flashButton, adjustedCooldown);
            }
        }

        private void BtnBoot_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            Image image = (Image)button.Content;
            bool isActive = button.Tag != null && button.Tag.ToString() == "active";
            
            image.Opacity = isActive ? 0.2 : 1.0;
            button.Tag = isActive ? null : "active";
        }

        private void BtnIns_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            Image image = (Image)button.Content;
            bool isActive = button.Tag != null && button.Tag.ToString() == "active";
            
            image.Opacity = isActive ? 0.2 : 1.0;
            button.Tag = isActive ? null : "active";
        }

        private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.IsLoaded && !_isLoadingConfig)  // 設定読み込み中は除外
            {
                _config.Scale = e.NewValue;
                UpdateWindowScale(e.NewValue);
            }
        }

        private void Viewbox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            _config.WindowLeft = this.Left;
            _config.WindowTop = this.Top;
            _config.Scale = scaleSlider.Value;
            _config.Save();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _config.WindowLeft = this.Left;
            _config.WindowTop = this.Top;
            _config.Scale = scaleSlider.Value;
            _config.Save();
        }

        private void CloseSettingsPopup_Click(object sender, RoutedEventArgs e)
        {
            settingsPopup.IsOpen = false;
        }

        private int GetFlashCooldown(Button flashButton)
        {
            // 親のGridを取得
            Grid parentGrid = (Grid)flashButton.Parent;
            
            // Grid内の同じ行にあるStackPanelを取得
            int row = Grid.GetRow(flashButton);
            var stackPanels = parentGrid.Children.Cast<UIElement>()
                .Where(x => Grid.GetRow(x) == row && x is StackPanel)
                .Cast<StackPanel>();
            
            // Column=3のStackPanelを取得（ブーツとCosmicボタンを含むパネル）
            StackPanel parentPanel = stackPanels.FirstOrDefault(x => Grid.GetColumn(x) == 3);
            
            if (parentPanel == null) return 300;
            
            Button bootButton = (Button)parentPanel.Children[0];
            Button insightButton = (Button)parentPanel.Children[1];
            
            bool isBootActive = bootButton.Tag != null && bootButton.Tag.ToString() == "active";
            bool isInsightActive = insightButton.Tag != null && insightButton.Tag.ToString() == "active";
            
            if (isBootActive && isInsightActive)
                return 234;  // 両方アクティブ
            else if (isBootActive)
                return 272;  // ブーツのみ
            else if (isInsightActive)
                return 254;  // Cosmic Insightのみ
            
            return 300;  // 基本クールダウン
        }

        private bool IsBootActive(Button flashButton)
        {
            Grid parentGrid = (Grid)flashButton.Parent;
            StackPanel parentPanel = (StackPanel)parentGrid.Children[3];
            Button bootButton = (Button)parentPanel.Children[0];
            return bootButton.Tag != null && bootButton.Tag.ToString() == "active";
        }

        private bool IsInsightActive(Button flashButton)
        {
            Grid parentGrid = (Grid)flashButton.Parent;
            StackPanel parentPanel = (StackPanel)parentGrid.Children[3];
            Button insightButton = (Button)parentPanel.Children[1];
            return insightButton.Tag != null && insightButton.Tag.ToString() == "active";
        }

        private void StartFlashTimer(Button button, int cooldown)
        {
            if (flashTimers.ContainsKey(button))
            {
                flashTimers[button].Stop();
            }

            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            
            int remainingTime = cooldown;
            timer.Tick += (sender, e) =>
            {
                remainingTime--;
                if (remainingTime <= 0)
                {
                    timer.Stop();
                    button.Content = null;
                    ImageBrush imageBrush = (ImageBrush)button.Background;
                    imageBrush.Opacity = 1.0;
                    flashTimers.Remove(button);
                }
                else
                {
                    ((TextBlock)button.Content).Text = FormatTime(remainingTime);
                }
            };

            // 初期表示を設定
            TextBlock timerText = (TextBlock)button.Content;
            timerText.Text = FormatTime(remainingTime);

            flashTimers[button] = timer;
            timer.Start();
        }

        private void MinuteFormat_Changed(object sender, RoutedEventArgs e)
        {
            _config.UseMinuteFormat = minuteFormatCheckBox.IsChecked ?? false;
            _config.Save();
            
            // 現在実行中のタイマーの表示を更新
            foreach (var timer in flashTimers)
            {
                if (timer.Key.Content is TextBlock timerText)
                {
                    string currentText = timerText.Text;
                    int seconds;
                    
                    if (currentText.Contains(":"))
                    {
                        // 分：秒形式から秒数に変換
                        string[] parts = currentText.Split(':');
                        seconds = (int.Parse(parts[0]) * 60) + int.Parse(parts[1]);
                    }
                    else
                    {
                        seconds = int.Parse(currentText);
                    }
                    
                    timerText.Text = FormatTime(seconds);
                }
            }
        }

        private string FormatTime(int totalSeconds)
        {
            if (!_config.UseMinuteFormat)
                return totalSeconds.ToString();
            
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return $"{minutes}:{seconds:D2}";
        }
    }
} 