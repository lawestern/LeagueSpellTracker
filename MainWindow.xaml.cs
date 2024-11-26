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
using System.Windows.Documents;

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
        private readonly Dictionary<string, TimerData> _timerData;
        private int _gameTime;

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

            _timerData = new Dictionary<string, TimerData>
            {
                { "InGame", new TimerData("InGame") },
                { "Top", new TimerData("Top") },
                { "Jungle", new TimerData("Jungle") },
                { "Mid", new TimerData("Mid") },
                { "Bot", new TimerData("Bot") },
                { "Support", new TimerData("Support") }
            };
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
            string lane = GetLaneFromButton(button);
            
            // 現在の状態に基づいて開始時間を取得
            int startCooldown = GetFlashCooldown(button);
            // 10秒を引いた時間からスタート
            int adjustedCooldown = Math.Max(0, startCooldown - 10);
            
            ImageBrush imageBrush = (ImageBrush)button.Background;
            imageBrush.Opacity = 0.2;

            // TimerDataの更新
            var timer = _timerData[lane];
            timer.RemainingSeconds = adjustedCooldown;
            timer.IsActive = true;
            timer.StartTime = DateTime.Now;

            // UIの更新
            TextBlock timerText = new TextBlock
            {
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
            UpdateTimerDisplay(button, lane);
            StartFlashTimer(button, lane, adjustedCooldown);
        }

        private void BtnFlash_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Released)
            {
                Button button = (Button)sender;
                string lane = GetLaneFromButton(button);
                
                // タイマーデータをリセット
                var timer = _timerData[lane];
                timer.IsActive = false;
                timer.RemainingSeconds = 0;
                timer.StartTime = null;

                // UIをリセット
                ImageBrush imageBrush = (ImageBrush)button.Background;
                imageBrush.Opacity = 1.0;
                button.Content = null;

                // タイマーが存在する場合は停止して削除
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
            string lane = GetLaneFromButton(button);
            var timer = _timerData[lane];

            int adjustment = e.Delta > 0 ? 5 : -5;
            timer.RemainingSeconds = Math.Max(0, timer.RemainingSeconds + adjustment);
            
            if (timer.RemainingSeconds > 0 && !timer.IsActive)
            {
                timer.IsActive = true;
                timer.StartTime = DateTime.Now;
            }

            UpdateTimerDisplay(button, lane);
        }

        private string GetLaneFromButton(Button button)
        {
            if (button == btnTopFlash) return "Top";
            if (button == btnJugFlash) return "Jungle";
            if (button == btnMidFlash) return "Mid";
            if (button == btnBotFlash) return "Bot";
            if (button == btnSupFlash) return "Support";
            return string.Empty;
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
            StartFlashTimerWithAdjustment((Button)sender, 30);
        }

        private void Btn60_Click(object sender, RoutedEventArgs e)
        {
            StartFlashTimerWithAdjustment((Button)sender, 60);
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

        private void StartFlashTimer(Button button, string lane, int cooldown)
        {
            if (flashTimers.ContainsKey(button))
            {
                flashTimers[button].Stop();
                flashTimers.Remove(button);
            }

            var timer = _timerData[lane];
            timer.RemainingSeconds = cooldown;

            DispatcherTimer dispatcherTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            
            dispatcherTimer.Tick += (sender, e) =>
            {
                timer.RemainingSeconds--;
                if (timer.RemainingSeconds < 0)
                {
                    dispatcherTimer.Stop();
                    button.Content = null;
                    ImageBrush imageBrush = (ImageBrush)button.Background;
                    imageBrush.Opacity = 1.0;
                    flashTimers.Remove(button);
                    timer.IsActive = false;
                    timer.StartTime = null;
                }
                else
                {
                    UpdateTimerDisplay(button, lane);
                }
            };

            flashTimers[button] = dispatcherTimer;
            dispatcherTimer.Start();
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
                    int remainingSeconds = GetRemainingSeconds(currentText);
                    UpdateTimerDisplay(timer.Key, GetLaneFromButton(timer.Key));  // remainingSecondsではなくレーン名を渡す
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
            _gameTime++;
            var timer = _timerData["InGame"];
            timer.RemainingSeconds = _gameTime;
            timer.IsActive = true;
            
            int minutes = _gameTime / 60;
            int seconds = _gameTime % 60;
            inGameTimer.Text = $"{minutes}:{seconds:D2}";
            
            // 各レーンのタイマーも更新
            foreach (var lane in new[] { "Top", "Jungle", "Mid", "Bot", "Support" })
            {
                if (_timerData[lane].IsActive)
                {
                    UpdateTimerDisplay(GetButtonFromLane(lane), lane);
                }
            }
        }

        private Button GetButtonFromLane(string lane)
        {
            return lane switch
            {
                "Top" => btnTopFlash,
                "Jungle" => btnJugFlash,
                "Mid" => btnMidFlash,
                "Bot" => btnBotFlash,
                "Support" => btnSupFlash,
                _ => null
            };
        }

        private void UpdateInGameTimerDisplay()
        {
            int minutes = _inGameSeconds / 60;
            int seconds = _inGameSeconds % 60;
            inGameTimer.Text = $"{minutes}:{seconds:D2}";
        }

        private void InGameTimer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var timer = _timerData["InGame"];
            
            if (inGameTimerDispatcher.IsEnabled)
            {
                inGameTimerDispatcher.Stop();
                timer.IsActive = false;
                inGameTimer.Foreground = Brushes.Red;
            }
            else
            {
                inGameTimerDispatcher.Start();
                timer.IsActive = true;
                inGameTimer.Foreground = Brushes.White;
            }
            e.Handled = true;
        }

        private void InGameTimer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                inGameTimerDispatcher.Stop();
                _gameTime = 0;
                var timer = _timerData["InGame"];
                timer.RemainingSeconds = 0;
                timer.IsActive = false;
                inGameTimer.Text = "0:00";
                inGameTimer.Foreground = Brushes.Red;
            }
        }

        private void InGameTimer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            int adjustment = e.Delta > 0 ? 5 : -5;
            _gameTime = Math.Max(0, _gameTime + adjustment);
            
            var timer = _timerData["InGame"];
            timer.RemainingSeconds = _gameTime;
            
            int minutes = _gameTime / 60;
            int seconds = _gameTime % 60;
            inGameTimer.Text = $"{minutes}:{seconds:D2}";
            
            // 他のアクティブなタイマーの表示も更新
            UpdateAllFlashTimers();
        }

        private void UpdateTimerDisplay(Button button, string lane)
        {
            var timer = _timerData[lane];
            if (!timer.IsActive) return;

            // Contentがnullの場合は新しいTextBlockを作成
            if (button.Content == null)
            {
                button.Content = new TextBlock
                {
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
            }

            TextBlock timerText = (TextBlock)button.Content;
            string mainTime = FormatTime(timer.RemainingSeconds);
            
            if (_config.ShowIngameTime && _timerData["InGame"].IsActive)
            {
                int flashAvailableTime = _gameTime + timer.RemainingSeconds;
                flashAvailableTime = (flashAvailableTime / 10) * 10;
                int minutes = flashAvailableTime / 60;
                int seconds = flashAvailableTime % 60;
                
                timerText.Inlines.Clear();
                timerText.Inlines.Add(new Run(mainTime));
                timerText.Inlines.Add(new Run($"\n({minutes}:{seconds:D2})")
                {
                    FontSize = timerText.FontSize - 2,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 192, 203))
                });
            }
            else
            {
                timerText.Inlines.Clear();
                timerText.Inlines.Add(new Run(mainTime));
            }
        }

        private void ShowIngameTime_Changed(object sender, RoutedEventArgs e)
        {
            _config.ShowIngameTime = showIngameTimeCheckBox.IsChecked ?? false;
            _config.Save();
            
            // 現在実行中のタイマーの表示を更新
            UpdateAllFlashTimers();
        }

        private void UpdateAllFlashTimers()
        {
            foreach (var timer in flashTimers)
            {
                if (timer.Key.Content is TextBlock timerText)
                {
                    string currentText = timerText.Text;
                    int remainingSeconds = GetRemainingSeconds(currentText);
                    UpdateTimerDisplay(timer.Key, GetLaneFromButton(timer.Key));  // remainingSecondsではなくレーン名を渡す
                }
            }
        }

        private int GetRemainingSeconds(string timeText)
        {
            // 空文字列や null チェック
            if (string.IsNullOrEmpty(timeText))
                return 0;

            // 改行で分割して最初の行（メインの時間）を取得
            string[] lines = timeText.Split('\n');
            string mainTime = lines[0].Trim();
            
            // かっこや余分な空白を除去
            if (mainTime.Contains("("))
            {
                mainTime = mainTime.Split('(')[0].Trim();
            }
            
            // MM:SS形式かどうかをチェック
            if (mainTime.Contains(":"))
            {
                string[] parts = mainTime.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int minutes) && int.TryParse(parts[1], out int seconds))
                {
                    return (minutes * 60) + seconds;
                }
            }
            
            // 単純な秒数形式の場合
            if (int.TryParse(mainTime, out int totalSeconds))
            {
                return totalSeconds;
            }
            
            // パースに失敗した場合は0を返す
            return 0;
        }

        private void StartFlashTimerWithAdjustment(Button timeButton, int adjustment)
        {
            Button flashButton = GetFlashButtonForTimeButton(timeButton);
            
            if (flashButton != null)
            {
                string lane = GetLaneFromButton(flashButton);
                int startCooldown = GetFlashCooldown(flashButton);
                int adjustedCooldown = Math.Max(0, startCooldown - adjustment);
                
                ImageBrush imageBrush = (ImageBrush)flashButton.Background;
                imageBrush.Opacity = 0.2;

                // TimerDataの更新
                var timer = _timerData[lane];
                timer.RemainingSeconds = adjustedCooldown;
                timer.IsActive = true;
                timer.StartTime = DateTime.Now;

                TextBlock timerText = new TextBlock
                {
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
                UpdateTimerDisplay(flashButton, lane);
                StartFlashTimer(flashButton, lane, adjustedCooldown);
            }
        }

        private void BtnQuickCopy_Click(object sender, RoutedEventArgs e)
        {
            var activeTimers = _timerData
                .Where(kvp => kvp.Key != "InGame" && kvp.Value.IsActive)
                .OrderBy(kvp => _gameTime + kvp.Value.RemainingSeconds)  // フラッシュアップ時間でソート
                .ToList();

            if (!activeTimers.Any() || !_timerData["InGame"].IsActive)
                return;

            var copyTexts = new List<string>();

            foreach (var timer in activeTimers)
            {
                int flashAvailableTime = _gameTime + timer.Value.RemainingSeconds;
                flashAvailableTime = (flashAvailableTime / 10) * 10;  // 10秒単位で切り下げ
                int minutes = flashAvailableTime / 60;
                int seconds = flashAvailableTime % 60;
                
                copyTexts.Add($"{timer.Key} {minutes:D2}:{seconds:D2} f");
            }

            string finalText = string.Join(" / ", copyTexts);
            try
            {
                System.Windows.Clipboard.SetText(finalText);
            }
            catch (Exception)
            {
                // クリップボードへのアクセスが失敗した場合は何もしない
            }
        }
    }
} 