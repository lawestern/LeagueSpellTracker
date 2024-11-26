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
using System.Windows.Media.Effects;

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
        private DispatcherTimer _inGameTimer;
        private int _inGameSeconds = 0;
        private DispatcherTimer inGameTimerDispatcher;  // クラスレベルで変数を定義


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
            
            _inGameTimer = new DispatcherTimer();
            _inGameTimer.Interval = TimeSpan.FromSeconds(1);
            _inGameTimer.Tick += InGameTimer_Tick;
            
            // タイマーの初期化
            inGameTimerDispatcher = new DispatcherTimer();
            inGameTimerDispatcher.Interval = TimeSpan.FromSeconds(1);
            inGameTimerDispatcher.Tick += InGameTimer_Tick;
            
            // 初期状態は停止中なので赤色に設定
            inGameTimer.Foreground = Brushes.Red;
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

                // 保存した位置に戻
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
                FontFamily = new FontFamily("Consolas"),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Effect = new DropShadowEffect
                {
                    ShadowDepth = 1,
                    Direction = 320,
                    Color = Colors.Black,
                    Opacity = 0.5,
                    BlurRadius = 2
                }
            };
            
            button.Content = timerText;
            StartFlashTimer(button, adjustedCooldown);
        }

        private void BtnFlash_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Released)  // ホイールクリクのみ
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
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Effect = new DropShadowEffect
                    {
                        ShadowDepth = 1,
                        Direction = 320,
                        Color = Colors.Black,
                        Opacity = 0.5,
                        BlurRadius = 2
                    }
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
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Effect = new DropShadowEffect
                    {
                        ShadowDepth = 1,
                        Direction = 320,
                        Color = Colors.Black,
                        Opacity = 0.5,
                        BlurRadius = 2
                    }
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
                flashTimers.Remove(button);
            }

            TextBlock timerText = new TextBlock
            {
                Text = FormatTime(cooldown),
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Effect = new DropShadowEffect
                {
                    ShadowDepth = 1,
                    Direction = 320,
                    Color = Colors.Black,
                    Opacity = 0.5,
                    BlurRadius = 2
                }
            };
            
            button.Content = timerText;

            DispatcherTimer timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            
            int remainingTime = cooldown;
            timer.Tick += (sender, e) =>
            {
                remainingTime--;
                if (remainingTime < 0)
                {
                    timer.Stop();
                    button.Content = null;
                    ImageBrush imageBrush = (ImageBrush)button.Background;
                    imageBrush.Opacity = 1.0;
                    flashTimers.Remove(button);
                }
                else
                {
                    timerText.Text = FormatTime(remainingTime);
                }
            };

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

        private void InGameTimer_Tick(object sender, EventArgs e)
        {
            _inGameSeconds++;
            UpdateInGameTimerDisplay();
        }

        private void UpdateInGameTimerDisplay()
        {
            int minutes = _inGameSeconds / 60;
            int seconds = _inGameSeconds % 60;
            inGameTimer.Text = $"{minutes}:{seconds:D2}";
        }

        private void InGameTimer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (inGameTimerDispatcher.IsEnabled)
            {
                inGameTimerDispatcher.Stop();  // タイマーが動いている場合は停止
                inGameTimer.Foreground = Brushes.Red;  // 停止中は赤色
            }
            else
            {
                inGameTimerDispatcher.Start(); // タイマーが停止している場合は開始
                inGameTimer.Foreground = Brushes.White;  // 動作中は白色
            }
            e.Handled = true;
        }

        private void InGameTimer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                _inGameTimer.Stop();
                _inGameSeconds = 0;
                UpdateInGameTimerDisplay();
            }
        }

        private void InGameTimer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            int adjustment = e.Delta > 0 ? 5 : -5;
            _inGameSeconds = Math.Max(0, _inGameSeconds + adjustment);
            UpdateInGameTimerDisplay();
        }

        private void UpdateTimerDisplay(Button button)
        {
            if (!(button.Content is TextBlock timerText)) return;
            
            string currentText = timerText.Text;
            int remainingSeconds;
            
            if (currentText.Contains(":"))
            {
                string[] parts = currentText.Split(':');
                remainingSeconds = (int.Parse(parts[0].Trim()) * 60) + int.Parse(parts[1].Trim());
            }
            else
            {
                remainingSeconds = int.Parse(currentText.Trim());
            }
            
            if (remainingSeconds > 0)
            {
                remainingSeconds--;
                timerText.Text = FormatTime(remainingSeconds);
            }
        }
    }
} 